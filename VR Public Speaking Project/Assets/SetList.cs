using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class SetList : MonoBehaviour
{
    private const string DefaultVrSetsPath = "/api/vr/sets";
    private const string DiscoveryRequestMessage = "stage-notes-discovery";
    private const string DiscoveryResponsePrefix = "stage-notes-discovery-response:";

    [System.Serializable]
    private class VrSetResponse
    {
        public int schemaVersion;
        public string activeSetId;
        public VrSet[] sets;
    }

    [System.Serializable]
    private class VrSet
    {
        public string id;
        public string name;
        public long lastUpdated;
        public long lastSentToVr;
        public VrCard[] cards;
    }

    [System.Serializable]
    private class VrCard
    {
        public string frontRichText;
        public string backRichText;
        public string frontPlainText;
        public string backPlainText;
        public int frontFontSize;
        public int backFontSize;
    }

    public List<Dictionary<string, string>> demoSet = new List<Dictionary<string, string>>()
    {
        new Dictionary<string, string>()
        {
            {"front", "What is the capital of France?"},
            {"back", "Paris"}
        },
        new Dictionary<string, string>()
        {
            {"front", "What is 2 + 2?"},
            {"back", "4"}
        },
        new Dictionary<string, string>()
        {
            {"front", "What is the largest planet in our solar system?"},
            {"back", "Jupiter"}
        }
    };

    public bool loadRemoteSetsOnStart = true;
    public bool useDemoSetWhenRemoteUnavailable = true;
    public bool autoApplyActiveRemoteSet = true;
    public bool pollForRemoteUpdates = true;
    public float remoteRefreshIntervalSeconds = 5f;
    public bool autoDiscoverCompanionOnLan = true;
    public int companionDiscoveryPort = 41234;
    public float companionDiscoveryTimeoutSeconds = 2f;
    public int companionServerPort = 3000;
    public string companionVrSetsUrl = "http://127.0.0.1:3000/api/vr/sets";
    public int requestTimeoutSeconds = 5;

    public Dictionary<string, List<Dictionary<string, string>>> sets = new Dictionary<string, List<Dictionary<string, string>>>();

    public Transform setCardList;
    public GameObject setCardPrefab;
    public FlashCard flashCardTarget;
    public FlashCardPreview flashCardPreview;

    private bool hasLoadedRemoteSets;
    private bool isFetchingRemoteSets;
    private bool hasWarnedAboutLoopbackUrl;
    private string lastRemoteSignature = string.Empty;
    private string discoveredCompanionBaseUrl = string.Empty;
    private Coroutine pollingCoroutine;

    public void GetSetData()
    {
        if (isFetchingRemoteSets)
        {
            return;
        }

        StartCoroutine(FetchSetData());
    }

    private IEnumerator FetchSetData()
    {
        isFetchingRemoteSets = true;

        string requestUrl = GetDirectRequestUrl();
        if (string.IsNullOrEmpty(requestUrl) && autoDiscoverCompanionOnLan)
        {
            yield return ResolveCompanionBaseUrl();

            if (!string.IsNullOrEmpty(discoveredCompanionBaseUrl))
            {
                requestUrl = BuildVrSetsUrl(discoveredCompanionBaseUrl);
            }
        }

        if (string.IsNullOrEmpty(requestUrl))
        {
            Debug.LogWarning("No reachable StageNotes URL is configured for VR set sync.");
            HandleRemoteUnavailable();
            isFetchingRemoteSets = false;
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(requestUrl))
        {
            request.timeout = requestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Failed to fetch VR sets from {requestUrl}: {request.error}");
                ClearDiscoveredUrlIfUsed(requestUrl);
                HandleRemoteUnavailable();
                isFetchingRemoteSets = false;
                yield break;
            }

            VrSetResponse response = JsonUtility.FromJson<VrSetResponse>(request.downloadHandler.text);

            if (response == null || response.sets == null)
            {
                Debug.LogWarning("The companion server returned an invalid VR sets payload.");
                HandleRemoteUnavailable();
                isFetchingRemoteSets = false;
                yield break;
            }

            if (response.sets.Length == 0)
            {
                Debug.Log("No published VR sets were returned by the companion server.");
                ApplyNoPublishedSets();
                isFetchingRemoteSets = false;
                yield break;
            }

            ApplyRemoteSets(response);
        }

        isFetchingRemoteSets = false;
    }

    private void ApplyRemoteSets(VrSetResponse response)
    {
        string nextSignature = BuildResponseSignature(response);
        if (hasLoadedRemoteSets && nextSignature == lastRemoteSignature)
        {
            return;
        }

        sets.Clear();
        hasLoadedRemoteSets = true;
        lastRemoteSignature = nextSignature;

        List<Dictionary<string, string>> activeSetCards = null;
        string activeSetName = string.Empty;

        foreach (VrSet remoteSet in response.sets)
        {
            List<Dictionary<string, string>> convertedCards = ConvertCards(remoteSet.cards);
            string setName = GetUniqueSetName(remoteSet.name);
            sets.Add(setName, convertedCards);

            if (remoteSet.id == response.activeSetId)
            {
                activeSetCards = CloneCards(convertedCards);
                activeSetName = setName;
            }
        }

        UpdateSetList();

        if (autoApplyActiveRemoteSet && activeSetCards != null)
        {
            ApplySetToSession(activeSetName, activeSetCards);
        }
    }

    private List<Dictionary<string, string>> ConvertCards(VrCard[] remoteCards)
    {
        List<Dictionary<string, string>> convertedCards = new List<Dictionary<string, string>>();

        if (remoteCards == null || remoteCards.Length == 0)
        {
            convertedCards.Add(CreateCard(string.Empty, string.Empty));
            return convertedCards;
        }

        foreach (VrCard remoteCard in remoteCards)
        {
            string front = !string.IsNullOrEmpty(remoteCard.frontRichText)
                ? remoteCard.frontRichText
                : remoteCard.frontPlainText ?? string.Empty;
            string back = !string.IsNullOrEmpty(remoteCard.backRichText)
                ? remoteCard.backRichText
                : remoteCard.backPlainText ?? string.Empty;

            convertedCards.Add(CreateCard(front, back));
        }

        return convertedCards;
    }

    private Dictionary<string, string> CreateCard(string front, string back)
    {
        return new Dictionary<string, string>()
        {
            {"front", front},
            {"back", back}
        };
    }

    private string GetUniqueSetName(string proposedName)
    {
        string normalizedName = string.IsNullOrWhiteSpace(proposedName)
            ? "Untitled Set"
            : proposedName.Trim();

        if (!sets.ContainsKey(normalizedName))
        {
            return normalizedName;
        }

        int suffix = 2;
        string candidate = normalizedName;

        while (sets.ContainsKey(candidate))
        {
            candidate = $"{normalizedName} ({suffix})";
            suffix += 1;
        }

        return candidate;
    }

    private void ClearSetList()
    {
        if (setCardList == null)
        {
            return;
        }

        for (int childIndex = setCardList.childCount - 1; childIndex >= 0; childIndex--)
        {
            Destroy(setCardList.GetChild(childIndex).gameObject);
        }
    }

    private void LoadDemoSet()
    {
        hasLoadedRemoteSets = false;
        lastRemoteSignature = string.Empty;
        sets.Clear();
        sets.Add("Demo Set", CloneCards(demoSet));
        UpdateSetList();
    }

    public void UpdateSetList()
    {
        ClearSetList();

        if (setCardPrefab == null || setCardList == null)
        {
            return;
        }

        foreach (string setName in sets.Keys)
        {
            GameObject setCard = Instantiate(setCardPrefab, setCardList);
            SetButton setButton = setCard.GetComponent<SetButton>();

            setButton.UpdateSetButton(setName, sets[setName]);
        }
    }

    void Start()
    {
        if (loadRemoteSetsOnStart)
        {
            GetSetData();

            if (pollForRemoteUpdates)
            {
                pollingCoroutine = StartCoroutine(PollRemoteSets());
            }

            return;
        }

        LoadDemoSet();
    }

    void OnDisable()
    {
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
    }

    void OnValidate()
    {
        requestTimeoutSeconds = Mathf.Max(1, requestTimeoutSeconds);
        remoteRefreshIntervalSeconds = Mathf.Max(1f, remoteRefreshIntervalSeconds);
        companionDiscoveryTimeoutSeconds = Mathf.Max(0.5f, companionDiscoveryTimeoutSeconds);
        companionDiscoveryPort = Mathf.Clamp(companionDiscoveryPort, 1, 65535);
        companionServerPort = Mathf.Clamp(companionServerPort, 1, 65535);
    }

    void Update()
    {
    }

    private IEnumerator PollRemoteSets()
    {
        WaitForSeconds wait = new WaitForSeconds(remoteRefreshIntervalSeconds);

        while (enabled && gameObject.activeInHierarchy)
        {
            yield return wait;

            if (!isFetchingRemoteSets)
            {
                yield return FetchSetData();
            }
        }
    }

    private void ApplyNoPublishedSets()
    {
        sets.Clear();
        hasLoadedRemoteSets = true;
        lastRemoteSignature = "empty";
        UpdateSetList();
    }

    private void HandleRemoteUnavailable()
    {
        if (hasLoadedRemoteSets)
        {
            return;
        }

        if (useDemoSetWhenRemoteUnavailable)
        {
            LoadDemoSet();
        }
    }

    private string GetDirectRequestUrl()
    {
        string directUrl = string.IsNullOrWhiteSpace(companionVrSetsUrl)
            ? string.Empty
            : companionVrSetsUrl.Trim();

        if (string.IsNullOrEmpty(directUrl))
        {
            return string.Empty;
        }

#if UNITY_EDITOR
        return directUrl;
#else
        if (IsLoopbackUrl(directUrl))
        {
            if (!hasWarnedAboutLoopbackUrl)
            {
                Debug.LogWarning("The VR app is configured with a loopback StageNotes URL. Switching to LAN discovery for same-Wi-Fi sync.");
                hasWarnedAboutLoopbackUrl = true;
            }

            return string.Empty;
        }

        return directUrl;
#endif
    }

    private bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsedUri))
        {
            return false;
        }

        return parsedUri.IsLoopback;
    }

    private string BuildVrSetsUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        return $"{baseUrl.TrimEnd('/')}{DefaultVrSetsPath}";
    }

    private IEnumerator ResolveCompanionBaseUrl()
    {
        if (!string.IsNullOrEmpty(discoveredCompanionBaseUrl))
        {
            yield break;
        }

        Task<CompanionDiscoveryResult> discoveryTask = Task.Run(DiscoverCompanionBaseUrl);

        while (!discoveryTask.IsCompleted)
        {
            yield return null;
        }

        if (discoveryTask.IsFaulted || discoveryTask.Result == null)
        {
            Debug.LogWarning("LAN discovery for the StageNotes server failed.");
            yield break;
        }

        if (!string.IsNullOrEmpty(discoveryTask.Result.error))
        {
            Debug.LogWarning(discoveryTask.Result.error);
            yield break;
        }

        discoveredCompanionBaseUrl = discoveryTask.Result.baseUrl;
        if (!string.IsNullOrEmpty(discoveredCompanionBaseUrl))
        {
            Debug.Log($"Discovered StageNotes companion at {discoveredCompanionBaseUrl}.");
        }
    }

    private CompanionDiscoveryResult DiscoverCompanionBaseUrl()
    {
        CompanionDiscoveryResult result = new CompanionDiscoveryResult();

        try
        {
            using (UdpClient client = new UdpClient())
            {
                client.EnableBroadcast = true;
                client.Client.ReceiveTimeout = Mathf.RoundToInt(companionDiscoveryTimeoutSeconds * 1000f);
                client.Client.SendTimeout = Mathf.RoundToInt(companionDiscoveryTimeoutSeconds * 1000f);

                byte[] requestBytes = Encoding.UTF8.GetBytes(DiscoveryRequestMessage);
                IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, companionDiscoveryPort);
                client.Send(requestBytes, requestBytes.Length, broadcastEndpoint);

                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] responseBytes = client.Receive(ref remoteEndpoint);
                string responseText = Encoding.UTF8.GetString(responseBytes).Trim();

                if (!responseText.StartsWith(DiscoveryResponsePrefix, StringComparison.Ordinal))
                {
                    result.error = "Received an unexpected LAN discovery response from the network.";
                    return result;
                }

                string portToken = responseText.Substring(DiscoveryResponsePrefix.Length);
                int resolvedPort;
                if (!int.TryParse(portToken, out resolvedPort))
                {
                    resolvedPort = companionServerPort;
                }

                result.baseUrl = $"http://{remoteEndpoint.Address}:{resolvedPort}";
                return result;
            }
        }
        catch (Exception exception)
        {
            result.error = $"Could not discover the StageNotes server on the local network: {exception.Message}";
            return result;
        }
    }

    private void ClearDiscoveredUrlIfUsed(string requestUrl)
    {
        if (string.IsNullOrEmpty(discoveredCompanionBaseUrl))
        {
            return;
        }

        if (requestUrl.StartsWith(discoveredCompanionBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            discoveredCompanionBaseUrl = string.Empty;
        }
    }

    private void ApplySetToSession(string setName, List<Dictionary<string, string>> cards)
    {
        ResolveSyncTargets();

        if (flashCardTarget != null)
        {
            flashCardTarget.cards = CloneCards(cards);
            flashCardTarget.ResetCards();
        }

        if (flashCardPreview != null)
        {
            flashCardPreview.flashcards = CloneCards(cards);

            if (flashCardPreview.gameObject.activeInHierarchy)
            {
                flashCardPreview.UpdateCardPreview();
            }
        }

        Debug.Log($"Applied active StageNotes set \"{setName}\" to the flashcard session.");
    }

    private void ResolveSyncTargets()
    {
        if (flashCardTarget == null)
        {
            flashCardTarget = UnityEngine.Object.FindFirstObjectByType<FlashCard>(FindObjectsInactive.Include);
        }

        if (flashCardPreview == null)
        {
            flashCardPreview = UnityEngine.Object.FindFirstObjectByType<FlashCardPreview>(FindObjectsInactive.Include);
        }
    }

    private List<Dictionary<string, string>> CloneCards(List<Dictionary<string, string>> sourceCards)
    {
        List<Dictionary<string, string>> clonedCards = new List<Dictionary<string, string>>();

        if (sourceCards == null)
        {
            return clonedCards;
        }

        foreach (Dictionary<string, string> sourceCard in sourceCards)
        {
            clonedCards.Add(new Dictionary<string, string>()
            {
                {"front", sourceCard != null && sourceCard.ContainsKey("front") ? sourceCard["front"] : string.Empty},
                {"back", sourceCard != null && sourceCard.ContainsKey("back") ? sourceCard["back"] : string.Empty}
            });
        }

        return clonedCards;
    }

    private string BuildResponseSignature(VrSetResponse response)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(response.activeSetId ?? string.Empty);

        foreach (VrSet set in response.sets)
        {
            builder
                .Append('|')
                .Append(set.id ?? string.Empty)
                .Append(':')
                .Append(set.lastUpdated)
                .Append(':')
                .Append(set.lastSentToVr)
                .Append(':')
                .Append(set.cards != null ? set.cards.Length : 0);
        }

        return builder.ToString();
    }

    private class CompanionDiscoveryResult
    {
        public string baseUrl;
        public string error;
    }
}
