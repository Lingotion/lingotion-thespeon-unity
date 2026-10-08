using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A minimal character controller demonstrating emotion blending and speed/loudness control.
/// Each segment carries an emotion blend at its start and another at its end, plus start/end speed and
/// loudness. All of those boundary values are keypoints on a single curve spanning the whole line, so the
/// delivery moves smoothly from one emotional state into the next instead of switching abruptly.
/// Press Space to synthesize.
/// </summary>
[RequireComponent(typeof(ThespeonComponent))]
[RequireComponent(typeof(AudioSource))]
public class EmotionBlendingCharacter : MonoBehaviour
{
    [Tooltip("Logs every per-token speed, loudness and emotion blend value fed to the model.")]
    public bool logControlTensors = true;

    [Tooltip("Press B to synthesize the same text with flat controls (speed 1, loudness 1, one emotion) as an A/B reference.")]
    public bool enableFlatReference = true;

    private ThespeonComponent engine;
    private AudioSource audioSource;
    private List<float> audioData;
    private AudioClip audioClip;
    private int receivedSamples;
    private float peakAmplitude;
    private double sumOfSquares;
    private string currentVariant;

    void Start()
    {
        engine = GetComponent<ThespeonComponent>();
        engine.OnAudioReceived += OnAudioPacketReceive;
        engine.OnSynthesisComplete += OnSynthesisComplete;
        audioData = new();
        audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    private const string FirstLine = "I found something in the old mine, ";
    private const string SecondLine = "and I really don't think we should have opened it!";

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Synthesize("keypoints", BuildKeypointSegments());
        }
        else if (enableFlatReference && Keyboard.current.bKey.wasPressedThisFrame)
        {
            Synthesize("flat", BuildFlatSegments());
        }
    }

    /// <summary>
    /// The demo line: speed and loudness sweep across it while the emotion blends from joy through
    /// anticipation into fear. Note that the first segment's end values and the second's start values agree,
    /// so the curves are continuous across the segment boundary - where they differ, the curve jumps.
    /// </summary>
    private static List<ThespeonInputSegment> BuildKeypointSegments()
    {
        return new()
        {
            // Excited about the find: pick up pace and volume towards the comma.
            new(FirstLine,
                startEmotion: new() { { Emotion.Joy, 1f } },
                endEmotion: new() { { Emotion.Joy, 0.5f }, { Emotion.Anticipation, 0.5f } },
                startSpeed: 1.0f, endSpeed: 1.2f,
                startLoudness: 1.0f, endLoudness: 1.2f),

            // Then the doubt sets in: slow down and drop to almost a whisper.
            new(SecondLine,
                startEmotion: new() { { Emotion.Joy, 0.5f }, { Emotion.Anticipation, 0.5f } },
                endEmotion: new() { { Emotion.Fear, 1f } },
                startSpeed: 1.2f, endSpeed: 0.7f,
                startLoudness: 1.2f, endLoudness: 0.6f)
        };
    }

    /// <summary>
    /// The same text with every control pinned flat, as an A/B reference. If this renders to the same
    /// duration and RMS as the keypoint version, the model is not responding to the control inputs.
    /// </summary>
    private static List<ThespeonInputSegment> BuildFlatSegments()
    {
        return new()
        {
            new(FirstLine,
                startEmotion: new() { { Emotion.Grief, 1f } },
                endEmotion: new() { { Emotion.Grief, 1f } }),

            new(SecondLine,
                startEmotion: new() { { Emotion.Grief, 1f } },
                endEmotion: new() { { Emotion.Grief, 1f } })
        };
    }

    private void Synthesize(string variant, List<ThespeonInputSegment> segments)
    {
        currentVariant = variant;
        receivedSamples = 0;
        peakAmplitude = 0f;
        sumOfSquares = 0d;

        ThespeonInput input = new(segments, defaultLanguage: "eng");
        InferenceConfigOverride configOverride = logControlTensors
            ? new InferenceConfigOverride { Verbosity = VerbosityLevel.Debug }
            : null;
        engine.Synthesize(input, sessionID: "EmotionBlendingSession", configOverride: configOverride);
    }

    // Add the received data to the audio buffer, and measure it so the two variants can be compared.
    void OnAudioPacketReceive(string sessionID, float[] data)
    {
        foreach (float sample in data)
        {
            peakAmplitude = Mathf.Max(peakAmplitude, Mathf.Abs(sample));
            sumOfSquares += sample * (double)sample;
        }
        receivedSamples += data.Length;

        lock (audioData)
        {
            audioData.AddRange(data);
        }
    }

    private void OnSynthesisComplete(string sessionID)
    {
        // Duration is the decisive measurement: speed changes the predicted phoneme durations, so a
        // responsive model cannot render both variants to the same length.
        float seconds = receivedSamples / 44100f;
        double rms = receivedSamples > 0 ? Math.Sqrt(sumOfSquares / receivedSamples) : 0d;
        Debug.Log($"[{currentVariant}] {receivedSamples} samples ({seconds:0.000}s), peak={peakAmplitude:0.0000}, rms={rms:0.0000}");
    }

    // Whenever the Unity audio thread needs data, it calls this function for us to fill the float[] data.
    void OnAudioRead(float[] data)
    {
        lock (audioData)
        {
            int currentCopyLength = Mathf.Min(data.Length, audioData.Count);
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
        engine.OnSynthesisComplete -= OnSynthesisComplete;
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}
