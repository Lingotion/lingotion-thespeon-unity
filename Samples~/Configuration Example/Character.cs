using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using Unity.Burst;
#endif

/// <summary>
/// A character controller that demonstrates how to use the inference config override to customize the Thespeon engine's behavior.
/// It includes settings for backend type, target frame time, overshoot margin, max skip layers,
/// and verbosity level.
/// </summary>
[RequireComponent(typeof(ThespeonEngine))]
[RequireComponent(typeof(AudioSource))]
public class Character : MonoBehaviour
{
    private ThespeonEngine engine;
    private AudioSource audioSource;
    private List<float> audioData = new();
    private AudioClip audioClip;
    private InferenceConfigOverride configOverride = new()
    {
        // Use exclusively CPU for inference
        PreferredBackendType = BackendType.CPU,
        // Allow Thespeon 4ms per frame. Larger values mean faster synthesis, but more time consumption per frame.
        // Very small values may cause stuttering if the audio is streamed directly to the AudioClip.
        TargetBudgetTime = 0.004,
        // Set target frame rate to 100 FPS. Thespeon will aim to stay within the target frame time even if it means underutilizing the budget time.
        TargetFrameTime = 0.01,
        // Set the buffer size to 0.3 seconds of audio. This means that Thespeon will wait until 0.3 seconds of data is available before giving Character access to it.
        BufferSeconds = 0.3f,
        // Enable adaptive scheduling, which means that Thespeon will try to adjust its frame time usage over time based on real time metrics. (This is the default behavior)
        UseAdaptiveScheduling = true,
        // Set the threshold of the adaptive algorithm to 150% of the TargetFrameTime. Decreasing this potentially allows for a more stable FPS for high-end devices, especially in combination with a high number of MaxSkipLayers.
        OvershootMargin = 1.5f,
        // Allow Thespeon's adaptive algorithm to insert frame yields up to 25 times per subtask. Increasing this value potentially allows for a more stable FPS, but may introduce audio stutters if set too high.
        MaxSkipLayers = 25,
        // Set how verbose the logging should be during synthesis. In order of increasing verbosity: None, Error, Warning, Info, Debug.
        Verbosity = VerbosityLevel.Warning
    };
    void Start()
    {
#if UNITY_EDITOR
        if (BurstCompiler.Options.EnableBurstDebug)
        {
            Debug.LogWarning("[Warning] Burst Native Debug Mode Compilation is ON; performance will be slower in Editor when running Thespeon on CPU.");
        }
#endif
        engine = GetComponent<ThespeonEngine>();
        engine.OnAudioReceived += OnAudioPacketReceive;

        audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
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
            void handler(bool result)
            {
                if (!result)
                {
                    LingotionLogger.CurrentLevel = VerbosityLevel.Warning;
                    LingotionLogger.Warning("Preload failed, will reattempt.");
                    LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
                }
                engine.Synthesize(input, configOverride: configOverride);
                engine.OnPreloadComplete -= handler;
            }
            engine.OnPreloadComplete += handler;
            StartCoroutine(engine.TryPreloadActorCoroutine(input.ActorName, input.ModuleType, configOverride: configOverride));
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