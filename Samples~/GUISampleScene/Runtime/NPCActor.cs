using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Lingotion.Thespeon.ThespeonRunscripts;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ThespeonEngine))]
public class NPCActor : MonoBehaviour
{
    private ThespeonEngine thespeonEngine;
    private AudioSource audioSource; 
    [SerializeField]
    private AudioClip audioClip;
    private List<float> audioData;
    public int packetSize = 1024;
    public float jitterDataLimit = 0.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        thespeonEngine = GetComponent<ThespeonEngine>();
        thespeonEngine.userCallback = OnAudioPacketReceive;
        thespeonEngine.jitterPacketSize = packetSize;
        thespeonEngine.jitterSecondsWaitTime = jitterDataLimit;
        audioData = new List<float>();
        audioSource = GetComponent<AudioSource>();
        audioClip = AudioClip.Create("ThespeonClip", packetSize, 1, 44100, true, OnAudioRead);
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnAudioRead(float[] data)
    {
        lock(audioData)
        {
            int currentCopyLength =  Mathf.Min(data.Length, audioData.Count);
            // take slice of buffer
            audioData.CopyTo(0, data, 0, currentCopyLength);  
            audioData.RemoveRange(0, currentCopyLength);          
            if(currentCopyLength < data.Length)
            { 
                // Debug.Log("ZERO_DATA " + data.Length);
                Array.Fill(data, 0f, currentCopyLength, data.Length - currentCopyLength);
            } else
            {
                
            }
        }
    } 

    void OnAudioPacketReceive(float[] data)
    {
        // Debug.Log("real data before lock " + Time.realtimeSinceStartup);
        lock(audioData)
        {
            
            // Debug.Log("real data " + Time.realtimeSinceStartup);
            audioData.AddRange(data);
        }
        // if(isDone)
        // {
        //   
            // string savePath = Path.Combine(Application.dataPath, "output.wav");
            // WavExporter.SaveWav(savePath, audioData.ToArray());
            // Debug.Log("Saved WAV at: " + savePath);
        // 
        // }
    }
}
