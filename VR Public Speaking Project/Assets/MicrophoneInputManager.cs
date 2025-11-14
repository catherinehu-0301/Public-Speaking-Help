using UnityEngine;

public class MicrophoneInputManager : MonoBehaviour
{
    public AudioSource audioSource;

    public int sampleRate = 16000;
    public int sampleWindowSize = 128;

    private string micName = null;
    private AudioClip micClip;
    private float[] sampleBuffer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            Debug.Log($"Microphone found: {Microphone.devices[0]}");
            micName = Microphone.devices[0];
            micClip = Microphone.Start(micName, true, 10, sampleRate);

            audioSource.clip = micClip;
            while (!(Microphone.GetPosition(micName) > 0)) { }

            audioSource.loop = true;
            audioSource.Play();

            sampleBuffer = new float[sampleWindowSize];
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (micClip == null) return;

        int micPosition = Microphone.GetPosition(micName) - sampleWindowSize;
        if (micPosition < 0) return;

        micClip.GetData(sampleBuffer, micPosition);

        float sum = 0f;
        for (int i = 0; i < sampleWindowSize; i++)
        {
            sum += sampleBuffer[i] * sampleBuffer[i];
        }
        float rmsValue = Mathf.Sqrt(sum / sampleWindowSize);

        Debug.Log($"RMS Value: {rmsValue}");
    }

    void OnDisable()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Stop();
            Microphone.End(Microphone.devices[0]);
        }
    }
}
