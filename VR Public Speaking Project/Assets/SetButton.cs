using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SetButton : MonoBehaviour
{
    public TextMeshProUGUI setNameText;
    public List<Dictionary<string, string>> flashcards;
    public GameObject flashcardPreviewPanel;

    public void UpdateSetButton(string setName, List<Dictionary<string, string>> cards)
    {
        setNameText.text = setName;
        flashcards = cards;
    }

    public void OnSetButtonPressed()
    {
        FlashCardPreview preview = flashcardPreviewPanel.GetComponent<FlashCardPreview>();
        preview.flashcards = flashcards;
        flashcardPreviewPanel.SetActive(true);
        preview.UpdateCardPreview();
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
