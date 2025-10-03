using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using Unity.Burst;
#endif
/// <summary>
/// A minimal character controller that uses the Thespeon engine for real-time voice synthesis.
/// This example demonstrates the minimal functionality of the Thespeon engine without any additional features.
/// </summary>
[RequireComponent(typeof(ThespeonEngine))]
[RequireComponent(typeof(AudioSource))]
public class MinimalCharacter : MonoBehaviour
{
    private ThespeonEngine engine;
    private AudioSource audioSource;
    private List<float> audioData;
    private AudioClip audioClip;
    void Start()
    {
#if UNITY_EDITOR
        if (BurstCompiler.Options.EnableBurstDebug)
        {
            Debug.LogWarning("[Warning] Burst Native Debug Mode Compilation is ON; performance will be slower in Editor when running Thespeon on CPU.");
        }
#endif
        engine = GetComponent<ThespeonEngine>();
        // Connect callback when audio is received from Thespeon
        engine.OnAudioReceived += OnAudioPacketReceive;
        // Initialize audio data buffer
        audioData = new();
        // Create a streaming audio clip for playback
        audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        // Start streaming audio from the clip
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            ThespeonInput input = new(new List<ThespeonInputSegment>() { new("Hi! This is my voice generated in real time!") });
            engine.Synthesize(input, sessionID: "SampleSynthesisSession");
        }
    }

    // Simply add the received data to the audio buffer. 
    void OnAudioPacketReceive(float[] data, PacketMetadata metadata)
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

    void OnDestroy()
    {
        engine.OnAudioReceived -= OnAudioPacketReceive;
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null; // Clear the clip to release resources
        }
    }

}