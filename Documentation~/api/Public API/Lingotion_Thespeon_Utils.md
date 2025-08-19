# Lingotion.Thespeon.Utils API Documentation

## Class `WavExporter`

Utility class for saving an audio output as a .wav file.
### Methods

#### `void SaveWav(string path, float[] samples, int sampleRate = 44100, int channels = 1)`

Saves an array of audio samples as a .wav file.

**Parameters:**

- `path`: Target file path.
- `samples`: Array of audio data.
- `sampleRate`: Optional sampling rate of audio data.
- `channels`: Optional number of channels of audio data.