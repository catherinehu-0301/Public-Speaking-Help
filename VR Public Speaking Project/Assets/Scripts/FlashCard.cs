using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro; 
using UnityEngine.InputSystem; // New Input System

public class FlashCard : MonoBehaviour
{
    /*
   fruits
 press enter or any button to go to next card  
   food
   
   piano 
    
    */

    public TextMeshProUGUI cardText;

    public List<Dictionary<string, string>> cards = new List<Dictionary<string, string>>(); // TODO: change to Dictionaries for front and back
    //  0       1       2
    // ["asdf", "food", "piano"];
    int currentCardIndex = 0;

    public InputActionReference nextCardAction;
    public InputActionReference previousCardAction;

    void OnEnable()
    {
      nextCardAction.action.performed += OnNextCard;
      nextCardAction.action.Enable();

      previousCardAction.action.performed += OnPreviousCard;
      previousCardAction.action.Enable();
    }

    void OnDisable()
    {
      nextCardAction.action.performed -= OnNextCard;
      nextCardAction.action.Disable();

      previousCardAction.action.performed -= OnPreviousCard;
      previousCardAction.action.Disable();
    }

    public void ResetCards()
    {
      currentCardIndex = 0;
      if (cards.Count > 0)
      {
        cardText.text = cards[currentCardIndex]["front"];
      }
    }

    void OnNextCard(InputAction.CallbackContext context)
    {
      NextCard();
      cardText.text = cards[currentCardIndex]["front"];
    }

    void OnPreviousCard(InputAction.CallbackContext context)
    {
      PreviousCard();
      cardText.text = cards[currentCardIndex]["front"];
    }

    void PreviousCard()
    {
      currentCardIndex--;
      if (currentCardIndex < 0) 
      {
        currentCardIndex = 0;
      }
    }

    void NextCard()
    {
      currentCardIndex++;
      if (currentCardIndex == cards.Count)
      {
        currentCardIndex = cards.Count - 1;
      }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (cards.Count > 0)
        {
          cardText.text = cards[currentCardIndex]["front"];
        }
    }

    // Update is called once per frame
    // void Update()
    // {
    //     // VR controller input Y and B buttons
    //     if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.B))
    //     {
    //       NextCard();
    //       cardText.text = cards[currentCardIndex];
    //     }
    //     if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.A))
    //     {
    //       PreviousCard();
    //       cardText.text = cards[currentCardIndex];
    //     }
    // }
}
