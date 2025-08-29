using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// A simple character controller that uses the Thespeon engine for real-time voice synthesis.
/// This example demonstrates the basic functionality of the Thespeon engine with some simple input annotations.
/// </summary>
[RequireComponent(typeof(ThespeonEngine))]
[RequireComponent(typeof(AudioSource))]
public class SimpleCharacter : MonoBehaviour
{
    private ThespeonEngine engine;
    private AudioSource audioSource;
    private List<float> audioData = new();
    private AudioClip audioClip;
    public ThespeonCharacterAsset actorAsset;
    void Start()
    {
        engine = GetComponent<ThespeonEngine>();
        // Connect callback when audio is received from Thespeon
        engine.OnAudioReceived += OnAudioPacketReceive;
        engine.OnSynthesisComplete += OnFinalPacketReceived;
        // Initialize audio data buffer
        audioData = new();
        // Create a streaming audio clip for playback
        audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        // Start streaming audio from the clip
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();

        LingotionLogger.CurrentLevel = VerbosityLevel.Warning;
        if (actorAsset == null)
        {
            LingotionLogger.Warning("Assign an actor asset in the Example Character inspector window if you want to use a specific actor. \nYou will find the assets under Assets > Lingotion Thespeon > CharacterAssets.");
            actorAsset = ScriptableObject.CreateInstance<ThespeonCharacterAsset>();
            return;
        }

        engine.TryPreloadActor(actorAsset.actorName, actorAsset.moduleType);
        LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            List<ThespeonInputSegment> segments = new() {
            new("Hi! This is my voice generated in real time!"),
        };
            ThespeonInput input = new(segments, actorAsset.actorName, actorAsset.moduleType);
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

    private void OnFinalPacketReceived(PacketMetadata metadata)
    {
        LingotionLogger.Info($"Synthesis complete for session: {metadata.sessionID}");
        engine.TryUnloadActor(metadata.characterName, metadata.moduleType);
    }

    void OnDestroy()
    {
        engine.OnAudioReceived -= OnAudioPacketReceive;
        engine.OnSynthesisComplete -= OnFinalPacketReceived;
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null; // Clear the clip to release resources
        }
    }
}