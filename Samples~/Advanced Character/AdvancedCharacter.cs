using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// An advanced character controller that uses the Thespeon engine for real-time voice synthesis.
/// This example demonstrates how to use advanced features such as custom speed, loudness, and language switching.
/// </summary>
[RequireComponent(typeof(ThespeonEngine))]
[RequireComponent(typeof(AudioSource))]
public class AdvancedNarrator : MonoBehaviour
{
    private ThespeonEngine engine;
    private AudioSource audioSource;
    private List<float> audioData;
    private AudioClip audioClip;
    public ThespeonCharacterAsset actorAsset;
    private AnimationCurve speed;
    private AnimationCurve loudness;

    void Start()
    {
        engine = GetComponent<ThespeonEngine>();
        // Register audio receive callback and final package callback
        engine.OnAudioReceived += OnAudioPacketReceive;
        engine.OnSynthesisComplete += OnFinalPacketReceived;

        audioData = new List<float>();
        audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();


        speed = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 1.5f),
            new Keyframe(0.9f, 0.7f),
            new Keyframe(1f, 0.5f)
        );

        loudness = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.7f),
            new Keyframe(1f, 1f)
        );
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
            string language1="";
            string language2="";
            string dialect1="";
            string dialect2="";
            string secondLine = "Assign another actor if you want to see a change of language in action!";
            Emotion ringEmotion = Emotion.Anger;

            if (actorAsset.actorName == "Elias Granhammar" && actorAsset.moduleType != ModuleType.XS)
            {
                language1 = "eng";
                language2 = "swe";
                dialect1 = "SE";
                dialect2 = "SE";
                secondLine = $"Om du vill kan jag börja prata svenska!{(char)ControlCharacters.Pause}";
                ringEmotion = Emotion.Rage;
            }
            else if (actorAsset.actorName == "Denel Honeyball" && actorAsset.moduleType != ModuleType.XS)
            {
                language1 = "eng";
                language2 = "eng";
                dialect1 = "GB";
                dialect2 = "US";
                secondLine = $"If you would like I can even speak American English!{(char)ControlCharacters.Pause}";
                ringEmotion = Emotion.Serenity;
            }
            else
            {
                LingotionLogger.Warning("For this example, you need an actor which speaks two languages or dialects. Please assign an actor which speaks two languages or dialects in the Example Character inspector window.");
            }
            char pauseChar = (char)ControlCharacters.Pause;
            List<ThespeonInputSegment> segments = new() {
                new("Hi! This is my voice generated in real time!"),
                new(secondLine, language2, dialect2),
                new($"{pauseChar}A wizard gave me a ring which says ", emotion: Emotion.Interest),
                new($"aːʃ naːhh dʊːrbɑɑtʊlʊːk {pauseChar} aːʃ naːhh ɡɪːmbɑːtʊːl {pauseChar} aːʃ naːhh θθrɑːkɑːtʊːlʊːk, ahh bʊʊrzʊʊm ɪʃɪ krɪmpɑtʊːl", isCustomPronounced: true, emotion: ringEmotion)
            };
            ThespeonInput input = new(segments, actorAsset.actorName, actorAsset.moduleType, defaultEmotion: Emotion.Joy, defaultLanguage: language1, defaultDialect: dialect1, speed: speed, loudness: loudness);
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