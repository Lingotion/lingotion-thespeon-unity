using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Utils;
using Lingotion.Thespeon.ThespeonRunscripts;
using UnityEngine;

[RequireComponent(typeof(ThespeonEngine))]
[RequireComponent(typeof(AudioSource))]
public class SimpleNarrator : MonoBehaviour
{
    private ThespeonEngine eng;
    private AudioSource audioSource;
    private List<float> audioData;
    private AudioClip audioClip;
    UserModelInput input;
    
    void Start()
    {
        eng = GetComponent<ThespeonEngine>();
        eng.jitterSecondsWaitTime = 1.0f;
        // Create output audio buffer
        audioData = new List<float>();
        // Create a streaming audio clip, which makes the Unity audio thread call OnAudioRead whenever it requests more audio.
        audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Create an input segment with a sample text
            UserSegment testSegment = new UserSegment("Greetings! This is a sample text, and I hope you are glad to hear my voice.", emotion:"Interest");
            
            // Replace ActorName with your actor of choice from your imported actor list in the Lingotion Thespeon Info window.
            // You can also choose your tags such as "quality" or leave it empty for default. Look at the Thespeon Info window for provided tags.
            var desiredTags = new ActorTags("InsertQualityLevelHere");
            input = new UserModelInput("InsertActorNameHere" ,desiredTags, new List<UserSegment>() { testSegment });
            Debug.Log("Input created" + input.ToString());
            // Schedule a Thespeon job with the input, and send the OnAudioPacketReceive as a callback for the audio chunks.
            eng.Synthesize(input, OnAudioPacketReceive);
        }
    }

    // Simply add the received data to the audio buffer. 
    void OnAudioPacketReceive(float[] data)
    {
        lock (audioData)
        {
            audioData.AddRange(data);
        }
    }

    // Whenever the Unity audio thread needs data, it calls this function for us to fill the float[] data. 
    void OnAudioRead(float[] data)
    {
        lock (audioData)
        {
            int currentCopyLength = Mathf.Min(data.Length, audioData.Count);
            // take slice of buffer
            audioData.CopyTo(0, data, 0, currentCopyLength);
            audioData.RemoveRange(0, currentCopyLength);
            if (currentCopyLength < data.Length)
            {
                Array.Fill(data, 0f, currentCopyLength, data.Length - currentCopyLength);
            }
        }
    }
}
