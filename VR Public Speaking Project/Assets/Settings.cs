using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Settings : MonoBehaviour
{
    public GameObject audience;
    public Slider crowdDensitySlider;
    public Slider crowdVolumeSlider;
    public AudioSource crowdAudioSource;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        crowdDensitySlider.onValueChanged.AddListener(SetAudience);
        crowdVolumeSlider.onValueChanged.AddListener(SetCrowdVolume);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetAudience(float value)
    {
        // Based off slider set that percent of audience members active
        int audienceCount = audience.transform.childCount;
        int activeCount = Mathf.RoundToInt(audienceCount * value);

        // Randomly activate audience members
        for (int i = 0; i < audienceCount; i++)
        {
            bool shouldActivate = Random.Range(0, audienceCount) < activeCount;
            audience.transform.GetChild(i).gameObject.SetActive(shouldActivate);
        }
    }

    public void SetCrowdVolume(float value)
    {
        crowdAudioSource.volume = value;
    }
}
