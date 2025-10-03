using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Core;
using System;
using UnityEngine.InputSystem;
using System.Linq;
using System.Threading;

#if UNITY_EDITOR
using Unity.Burst;
#endif

[RequireComponent(typeof(ThespeonEngine))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(TextMeshProUGUI))]
public class AudioCallbackSample : MonoBehaviour
{
    // The input text object to modify
    public TextMeshProUGUI label;
    public Color baseColor = Color.white;
    public Color pastColor = new Color(1f, .85f, .3f);
    public Color currentColor = new Color(1f, 1f, .1f);
    // How many samples to subtract to compensate for a audio stream delay
    public int compensation = 0;
    private ThespeonEngine _engine;
    private AudioSource _audioSource;
    private List<float> _audioData;
    private AudioClip _audioClip;
    private Queue<int> _markerIndices;
    private string[] _words;
    private int _currentWordIndex = -1;
    private int _currentSampleIndex = 0;
    private bool _isFirst = true;

    void Start()
    {
#if UNITY_EDITOR
        if (BurstCompiler.Options.EnableBurstDebug)
        {
            Debug.LogWarning("[Warning] Burst Native Debug Mode Compilation is ON; performance will be slower in Editor when running Thespeon on CPU.");
        }
#endif
        _engine = GetComponent<ThespeonEngine>();
        // Connect callback when audio is received from Thespeon
        _engine.OnAudioReceived += OnAudioPacketReceive;
        // Initialize audio data buffer
        _audioData = new();
        // Create a streaming audio clip for playback
        _audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        // Start streaming audio from the clip
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = _audioClip;
        _audioSource.loop = true;
        _audioSource.Play();
    }

    void Update()
    {
        
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            _words = label.text.Split(" ");
            // Insert a sample request marker in front of each word, signaling that we want the audio sample index for that part of the text
            Func<string, string> addMarker = s => ControlCharacters.AudioSampleRequest + s;
            string markedInputString = string.Join(" ", _words.Select(addMarker));

            ThespeonInput input = new(new List<ThespeonInputSegment>() { new (markedInputString, emotion: Emotion.Interest)});
            _engine.Synthesize(input, sessionID: "AudioCallbackSample");
        }

        if (_audioSource == null || _audioSource.clip == null || _markerIndices == null || _markerIndices.Count == 0) return;
        int targetSample = _markerIndices.Peek();
        long currentAudibleSample = GetPlaybackSampleIndex();
        // Advance highlight if we’ve crossed into a new marker
        if (currentAudibleSample >= targetSample)
        {
            _markerIndices.Dequeue();
            _currentWordIndex++;
            ChangeTextColours();
        }
        
    }

    // Simply add the received data to the audio buffer. 
    void OnAudioPacketReceive(float[] data, PacketMetadata metadata)
    {
        // If first packet, check metadata for the requested marker queue
        if (_isFirst)
        {
            _isFirst = false;
            _markerIndices = metadata.requestedAudioIndices;
        }
        
        lock (_audioData)
        {
            _audioData.AddRange(data);
        }
    }

     // Whenever the Unity audio thread needs data, it calls this function for us to fill the float[] data. 
    void OnAudioRead(float[] data)
    {
        lock (_audioData)
        {
            int currentCopyLength = Mathf.Min(data.Length, _audioData.Count);
            // take slice of buffer
            _audioData.CopyTo(0, data, 0, currentCopyLength);
            _audioData.RemoveRange(0, currentCopyLength);
            if (currentCopyLength < data.Length)
            {
                Array.Fill(data, 0f, currentCopyLength, data.Length - currentCopyLength);
            }
        }
    }
    
    // Triggers before Unity sends audio to be consumed
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_isFirst)
        {
            // Add the number of samples read to our counter 
            Interlocked.Add(ref _currentSampleIndex, data.Length / channels);
        }
    }

    // Tries to compensate for latency due to DSPBuffersize, as well as differences in device sample rate.
    private int GetPlaybackSampleIndex() {
        int dspBuf, numBuf;
        AudioSettings.GetDSPBufferSize(out dspBuf, out numBuf);
        int outputLatencySamples = dspBuf * numBuf;
        long audible = Volatile.Read(ref _currentSampleIndex) - outputLatencySamples - compensation;
        
        double outputSR = AudioSettings.outputSampleRate;
        double inputSR = 44100.0;
        int audibleClipSample = (int)Math.Floor(audible * (inputSR / outputSR));
        return audibleClipSample < 0 ? 0 : audibleClipSample;
    }


    void OnDestroy()
    {
        _engine.OnAudioReceived -= OnAudioPacketReceive;
        if (_audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.clip = null; // Clear the clip to release resources
        }
    }

    void ChangeTextColours()
    {
        if (_words == null || _words.Length == 0) return;

        var text = "";
        for (int i = 0; i < _words.Length; i++)
        {
            if (i > 0) text += " ";

            if (i < _currentWordIndex)
                text += $"<color=#{ColorUtility.ToHtmlStringRGBA(pastColor)}>{_words[i]}</color>";
            else if (i == _currentWordIndex)
                text += $"<b><color=#{ColorUtility.ToHtmlStringRGBA(currentColor)}>{_words[i]}</color></b>";
            else
                text += $"<color=#{ColorUtility.ToHtmlStringRGBA(baseColor)}>{_words[i]}</color>";
        }

        label.text = text;
    }
}
