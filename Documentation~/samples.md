# **Package Samples**
The Lingotion Thespeon package ships with a set of samples, each a small scene and script that shows one part of the API. They build on each other, so if you are new to Thespeon it is worth going through them roughly in the order listed below.

## Table of Contents
- [**Package Samples**](#package-samples)
  - [Table of Contents](#table-of-contents)
  - [Importing a sample](#importing-a-sample)
  - [Overview](#overview)
  - [Minimal Character](#minimal-character)
  - [Simple Character](#simple-character)
  - [Advanced Character](#advanced-character)
  - [Emotion Blending](#emotion-blending)
  - [Audio Callback](#audio-callback)
  - [Configuration Example](#configuration-example)
  - [Advanced GUI](#advanced-gui)

---
## Importing a sample
1. Make sure the package is installed and that you have imported at least one Character Pack and its Language Pack(s). See [Get Started - Unity](./get-started-unity.md) if you have not done this yet.
2. Open **Window > Package Manager**, select **Lingotion Thespeon** and open the **Samples** tab.
3. Click **Import** next to the sample you want. Unity copies it into your project under:

   ```
   Assets > Samples > Lingotion Thespeon > <version> > <Sample Name>
   ```
4. Open the scene in the sample folder and enter Play Mode.

> [!NOTE]
> The samples use the **Unity Input System** package. Ensure your project has it installed and that **Active Input Handling** in **Edit > Project Settings > Player** is set to **Input System Package (New)** or **Both**.

Every character sample follows the same pattern: a `ThespeonComponent` and an `AudioSource` sit on the same GameObject, the script subscribes to `OnAudioReceived`, and the audio is streamed into a looping `AudioClip` as it arrives. Unless stated otherwise, press **Space**, **Enter** or **S** in Play Mode to synthesize.

## Overview
| Sample | Shows | Read alongside |
|---|---|---|
| [Minimal Character](#minimal-character) | The least code needed to synthesize and play a line | [Get Started - Unity](./get-started-unity.md#run-the-minimal-character-sample) |
| [Simple Character](#simple-character) | Choosing a character with a `ThespeonCharacterAsset`, preloading and unloading it | [Character Control Guide](./character-control.md#the-thespeon-character-asset) |
| [Advanced Character](#advanced-character) | Multiple segments, emotions, language and dialect switching, pauses, custom pronunciation, speed and loudness curves | [Character Control Guide](./character-control.md) |
| [Emotion Blending](#emotion-blending) | Emotion blends and start/end speed and loudness per segment | [Blending emotions and shaping delivery](./character-control.md#blending-emotions-and-shaping-delivery) |
| [Audio Callback](#audio-callback) | Audio sample request markers, used to highlight words as they are spoken | [`ControlCharacters.AudioSampleRequest`](./thespeon-tools.md#controlcharactersaudiosamplerequest) |
| [Configuration Example](#configuration-example) | Tuning backend, frame budget and buffering with `InferenceConfigOverride` | [Configuration and Performance Tuning Manual](./thespeon-configuration.md) |
| [Advanced GUI](#advanced-gui) | An interactive runtime panel for trying out every delivery control | — |

---
## Minimal Character
**Script:** `MinimalCharacter.cs`

The bare minimum: a single `ThespeonInputSegment` with a line of text and nothing else. With no character, emotion or language given, Thespeon picks the first available character and falls back to default values for the rest. This is the sample the [Get Started guide](./get-started-unity.md#run-the-minimal-character-sample) walks you through.

## Simple Character
**Script:** `SimpleCharacter.cs`

Adds a public `characterAsset` field so you can choose which character speaks. Assign one of the `ThespeonCharacterAsset`s found under `Assets > Lingotion Thespeon > CharacterAssets` in the inspector; if you leave it empty the sample warns you and falls back to the default character.

It also shows the character lifecycle: `TryPreloadCharacter` in `Start` so the first line does not pay the loading cost, and `TryUnloadCharacter` from the `OnSynthesisComplete` callback to free the memory again.

## Advanced Character
**Script:** `AdvancedCharacter.cs`

Builds a line out of several segments to show what a single `ThespeonInput` can do:
- a default emotion for the whole input, overridden per segment,
- a change of language or dialect partway through the line,
- `ControlCharacters.Pause` to insert pauses,
- a custom-pronounced segment written in IPA (`isCustomPronounced: true`),
- `speed` and `loudness` `AnimationCurve`s spanning the whole input,
- preloading with `runWarmup: true`.

The language switch needs a character that speaks two languages or dialects. The sample is set up for **Elias Granhammar** (English to Swedish) and **Denel Honeyball** (British to American English) in any module size other than XS; with any other character it still runs but logs a warning and stays in one language.

## Emotion Blending
**Script:** `EmotionBlendingCharacter.cs`

Shows the per-segment delivery controls: an emotion *blend* (several emotions with weights) at the start and at the end of each segment, plus start and end values for speed and loudness. All of these boundary values become keypoints on one curve across the line, so the delivery moves smoothly from one state into the next.

- **Space** synthesizes the blended line.
- **B** synthesizes the same text with flat controls, as an A/B reference (toggle with *Enable Flat Reference*).

After each line the sample logs its duration, peak and RMS so the two variants can be compared. With *Log Control Tensors* enabled it also logs every per-token speed, loudness and emotion value sent to the model.

## Audio Callback
**Script:** `AudioCallbackSample.cs`

Highlights the words of a line in time with the audio. The script puts a `ControlCharacters.AudioSampleRequest` marker in front of every word, receives the sample index of each marker through `OnAudioSampleRequestReceived`, and compares them against the current playback position to advance the highlight.

Change the line with the *Text* field on the component, and the highlight with the colour fields. The text is drawn with UI Toolkit, built in code, using the font from Unity's default runtime theme, so the sample ships no fonts of its own. If the highlight runs ahead of what you hear, increase *Compensation* on the component; the sample already subtracts the DSP buffer latency that Unity reports.

## Configuration Example
**Script:** `Character.cs`

Passes an `InferenceConfigOverride` to both `TryPreloadCharacterCoroutine` and `Synthesize` to control how Thespeon runs: CPU backend, per-frame time budget, target frame time, buffer size, adaptive scheduling and logging verbosity. Every property is commented in the script; the [Configuration and Performance Tuning Manual](./thespeon-configuration.md) explains each one in detail.

The sample also shows waiting for `OnPreloadComplete` before synthesizing.

## Advanced GUI
**Scene:** `Advanced GUI.unity` &nbsp; **Script:** `AdvancedThespeonGUI.cs`

A runtime control panel for experimenting with a character without writing code. Open the scene, enter Play Mode, and use the panel to:
- pick a **character**, **module size**, **compute backend** (Default, CPU or GPU Compute) and **language** — the choices cascade, so only what the selected character supports is offered,
- type the **line** to speak,
- set the **start and end emotions** as blends, and **start and end loudness and speed** (0.5–2.0),
- press **Synthesize**.

While the line plays, a karaoke overlay at the bottom of the screen shows the character's portrait and reveals the line in time with the audio. It uses the same audio sample request markers as the [Audio Callback](#audio-callback) sample.

**Developer mode.** Press **Ctrl+D** (**Cmd+D** on macOS) to split the line into several segments, each with its own text and boundary values, to build a curve with more than two keypoints. Turning developer mode off merges the segments back into one.

> [!TIP]
> To try lines in Edit Mode instead, use the **Audio Test Lab** under the **Characters** tab in the **Thespeon Info Window**. It can also save the result as a `.wav` file.
