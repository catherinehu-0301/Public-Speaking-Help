using UnityEngine;
using System.Collections.Generic;

public class FlashCardPreview : MonoBehaviour
{

    public List<Dictionary<string, string>> flashcards = new List<Dictionary<string, string>>()
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

    public FlashCard flashCards;
    public GameObject setList;
    public GameObject settings;

    public GameObject previewCardPrefab;

    public void ConfirmFlashCards()
    {
        flashCards.cards = flashcards;
        flashCards.ResetCards();

        // hide all panels
        setList.SetActive(false);
        settings.SetActive(false);
        this.gameObject.SetActive(false);
    }

    public void UpdateCardPreview()
    {
        foreach(Dictionary<string, string> card in flashcards)
        {
            GameObject previewCard = Instantiate(previewCardPrefab, this.transform);
            previewCard.GetComponent<CardFlip>().frontText = card["front"];
            previewCard.GetComponent<CardFlip>().backText = card["back"];
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
