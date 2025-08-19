using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using UnityEngine;

/// <summary>
/// NPCActor is responsible for managing the NPC's audio and interaction with the Thespeon engine.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ThespeonEngine))]
public class NPCActor : MonoBehaviour
{
    private ThespeonEngine thespeonEngine;
    private AudioSource audioSource;
    [SerializeField]
    private AudioClip audioClip;
    private List<float> audioData;
    private int packetSize = 1024;
    void Start()
    {
        thespeonEngine = GetComponent<ThespeonEngine>();
        thespeonEngine.OnAudioReceived += OnAudioPacketReceive;

        audioData = new List<float>();
        audioSource = GetComponent<AudioSource>();
        audioClip = AudioClip.Create("ThespeonClip", packetSize, 1, 44100, true, OnAudioRead);
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
    }
    private void OnAudioRead(float[] data)
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
    private void OnAudioPacketReceive(float[] data, PacketMetadata metadata)
    {
        lock (audioData)
        {
            audioData.AddRange(data);
        }
    }

    void OnDestroy()
    {
        thespeonEngine.OnAudioReceived -= OnAudioPacketReceive;
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}
