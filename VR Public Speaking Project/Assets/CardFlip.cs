using UnityEngine;

public class CardFlip : MonoBehaviour
{
    Transform card;
    public float flipSpeed = 5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void FlipCard()
    {
        // Rotate about the x-axis, 0 is front and 180 is back
        Quaternion targetRotation;
        if (card.rotation.x == 0)
        {
            targetRotation = Quaternion.Euler(180, 0, 0);
        } 
        else 
        {
            targetRotation = Quaternion.Euler(0, 0, 0);
        }

        card.rotation = Quaternion.Slerp(card.rotation, targetRotation, Time.deltaTime * flipSpeed);
    }
}
