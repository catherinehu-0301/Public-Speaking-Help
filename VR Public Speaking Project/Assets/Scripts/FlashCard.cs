using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class FlashCard : MonoBehaviour
{
    /*
   fruits
 press enter or any button to go to next card  
   food
   
   piano 
    
    */

    public TextMeshProUGUI cardText;

    public List<string> cards = new List<string>();
    //  0       1       2
    // ["asdf", "food", "piano"];
    int currentCardIndex = 0;

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
        
    }

    // Update is called once per frame
    void Update()
    {
        // VR controller input Y and B buttons
        if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.B))
        {
          NextCard();
          cardText.text = cards[currentCardIndex];
        }
        if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.A))
        {
          PreviousCard();
          cardText.text = cards[currentCardIndex];
        }
    }
}
