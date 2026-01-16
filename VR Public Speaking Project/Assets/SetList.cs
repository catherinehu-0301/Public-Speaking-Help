using UnityEngine;
using System.Collections.Generic;

public class SetList : MonoBehaviour
{
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

    public Dictionary<string, List<Dictionary<string, string>>> sets = new Dictionary<string, List<Dictionary<string, string>>>();

    public Transform setCardList;
    public GameObject setCardPrefab;

    public void GetSetData()
    {
        // Make an http request from the desktop companion to get the set data
    }

    public void UpdateSetList()
    {
        foreach (string setName in sets.Keys)
        {
            GameObject setCard = Instantiate(setCardPrefab, setCardList);
            SetButton setbutton = setCard.GetComponent<SetButton>();

            setbutton.UpdateSetButton(setName, sets[setName]);
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sets.Add("Demo Set", demoSet);
        UpdateSetList();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
