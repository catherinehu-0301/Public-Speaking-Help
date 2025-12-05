using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro; 
using UnityEngine.InputSystem; // New Input System
using UnityEngine.XR;

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

    public Transform flashCardRespawn;


    public System.Action OnSwipeLeft;
    public System.Action OnSwipeRight;

    public float minSwipeDistance = 0.15f;

    private bool _isSwiping = false;
    private Vector3 _initialSwipePosition;


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
    void Update()
    {
        // VR controller hands only
        /* UX description:
          - User holds the card in one hand
          - User swipes left and right with their index finger to go to next/previous card
          - Ambidextrous: either hand can be used to swipe
        */


        // OLD: delete later
        // if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.B))
        // {
        //   NextCard();
        //   cardText.text = cards[currentCardIndex];
        // }
        // if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.A))
        // {
        //   PreviousCard();
        //   cardText.text = cards[currentCardIndex];
        // }
    }



    // Check if we exited a trigger collider
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("FlashCardZone")) {
          // Respawn the flashcard at the position
          this.transform.position = flashCardRespawn.position;
          this.transform.rotation = flashCardRespawn.rotation;

          // Reset valocity too
          Rigidbody rb = this.GetComponent<Rigidbody>();
          rb.linearVelocity = Vector3.zero;
          rb.angularVelocity = Vector3.zero;
        }
    }
}
