// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.
using System.IO;
using UnityEngine;

namespace Lingotion.Thespeon.Utils
{
    /// <summary>
    /// Utility class for saving an audio output as a .wav file.
    /// </summary>
    public static class WavExporter
    {
        /// <summary>
        /// Saves an array of audio samples as a .wav file.
        /// </summary>
        /// <param name="path">Target file path.</param>
        /// <param name="samples">Array of audio data.</param>
        /// <param name="sampleRate">Optional sampling rate of audio data.</param>
        /// <param name="channels">Optional number of channels of audio data.</param>
        public static void SaveWav(string path, float[] samples, int sampleRate = 44100, int channels = 1)
        {
            using var fileStream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(fileStream);
            int sampleCount = samples.Length;
            int byteRate = sampleRate * channels * 2; // 16-bit PCM

            // WAV Header
            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + sampleCount * 2); // File size
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            // Format chunk
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16); // Subchunk1Size (PCM)
            writer.Write((short)1); // Audio format (1 = PCM)
            writer.Write((short)channels); // Number of channels
            writer.Write(sampleRate); // Sample rate
            writer.Write(byteRate); // Byte rate
            writer.Write((short)(channels * 2)); // Block align
            writer.Write((short)16); // Bits per sample

            // Data chunk
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(sampleCount * 2); // Subchunk2Size

            // Convert float samples to 16-bit PCM and write
            foreach (var sample in samples)
            {
                short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                writer.Write(intSample);
            }
        }
    }
}