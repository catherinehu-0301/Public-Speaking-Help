using UnityEngine;
using TMPro;

public class CardFlip : MonoBehaviour
{
    Transform card;
    public float flipSpeed = 5f;

    private bool isFlipped = false;
    private Quaternion targetRotation;

    public TextMeshProUGUI frontText;
    public TextMeshProUGUI backText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        card = this.transform;
        targetRotation = card.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        card.localRotation = Quaternion.Slerp(card.localRotation, targetRotation, Time.deltaTime * flipSpeed);

        if (Quaternion.Angle(card.localRotation, targetRotation) < 0.1f)
        {
            card.localRotation = targetRotation;
        }
    }

    public void FlipCard()
    {
        isFlipped = !isFlipped;
        targetRotation = Quaternion.Euler(isFlipped ? 180f : 0f, 0f, 0f);
    }
}
