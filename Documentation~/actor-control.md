# **Actor Control Guide**

# Table of Contents
- [**The Thespeon Character Asset**](#the-thespeon-character-asset)
- [**Run the Simple Character**](#run-the-simple-character)
- [**Changing emotion and language**](#changing-emotion-and-language)
- [**Controlling pronunciation**](#controlling-pronunciation)
- [**Changing Input-Wide Parameters**](#changing-input-wide-parameters)
- [**Next Steps**](#next-steps)

---
# Overview
By now you should have been able to hear the Thespeon Engine generate a sample line after having followed the [Get Started - Unity Guide](./get-started-unity.md). This guide will go into more detail on how to select a specific actor, and different ways to change how the actor speaks a certain line.

To learn how to control your actor we will start with the basic _Simple Character_ sample and make a chosen actor say the line "Hi! This is my voice generated in real time!" with a happy emotion in English. 
We will successively build on this until we have something closer to _Advanced Character_ sample.

---
# The Thespeon Character Asset
On import of an actor pack, Thespeon will generate a [ScriptableObject](https://docs.unity3d.com/Manual/class-ScriptableObject.html) called a _ThespeonCharacterAsset_, which is a representation of that specific actor and module type. These are located in the **Assets > Lingotion Thespeon > CharacterAssets** directory.
> [!IMPORTANT]
> If the directory is missing or no ThespeonCharacterAssets are found, press the **Regenerate Input Assets** button in the Thespeon Info Window.
---
# Run the Simple Character
Firstly we want to pick a ThespeonCharacterAsset. Go to the Game Object Hierachy and select the **Example Character** GameObject. In its Inspector window, find the field for **Actor Asset** currently set to "None". Click the icon on the right and select one of the assets available, or drag a ThespeonCharacterAsset from the CharacterAsset directory to the field. 

For this guide we recommend an actor that can speak at least two dialects or languages. Both Freemium actors, _Elias Granhammar_ and _Denel Honeyball_, have that capability.

Now you can run the Simple Character scene and press the **Space**, **Enter**, or **S** key to listen to the result.

> [!TIP]
> You can change actor without recompiling by assigning a new Actor Asset in the inspector window and pressing space once more.
---
# Changing emotion and language
The _ThespeonInputSegment_ class represents a region of text that should be spoken in certain way, like emotional tone and spoken language. Multiple segments are packaged in a _ThespeonInput_ object, and are seamlessly stitched together to form the full line to the Thespeon Engine for synthesis. 
This enables switching between languages, dialects, and emotional tones as you please when giving instructions to the Thespeon Engine. 

In _SimpleCharacter.cs_, try replacing:
```csharp
List<ThespeonInputSegment> segments = new() { new("Hi! This is my voice generated in real time!") };
```
with:
```csharp
List<ThespeonInputSegment> segments = new() { 
  new("Hi! This is my voice "),
  new("generated in real time!", emotion: Emotion.Anger)
};
```
This changes the emotion of which the actor says that particular segment. Try experimenting with different emotions and text lengths!

One may also change the language or and dialect of the speaker, provided that the actor supports it. To see what options your imported actor supports, go to the **Actors** tab of the Thespeon Info Window, and select an actor in the list to see all installed modules. Expand the target module to see the supported languages and dialects.

![Actor Information](./data/actor-information.png?raw=true "Actor Information")

In this case, we see that the actor Denel Honeyball has two available dialects in English, "_GB_" and "_US_" being the specific strings to give to the ThespeonInput.

Go back to the _SimpleCharacter.cs_ script and find the _ThespeonInput_ below the list of segments -- change it from:
```csharp
ThespeonInput input = new(segments, actorAsset.actorName, actorAsset.moduleType, defaultEmotion: Emotion.Joy, defaultLanguage: "eng");
```
to:
```csharp
ThespeonInput input = new(segments, actorAsset.actorName, actorAsset.moduleType, defaultEmotion: Emotion.Joy, defaultLanguage: "eng", defaultDialect: "US");
```
and run the sample again. You should hear a change in dialect according to your changes.

> [!NOTE]
> If you are using Elias, replace the _defaultLanguage_ parameter value with `defaultLanguage: "swe"` and he will try to say the written line as if it were read in Swedish.
---
# Controlling pronunciation
A very common case in video games is the pronunciation of something that is not necessarily a normal part of the language the actor speaks, such as fictional names or single words from other languages. When given an input text, Thespeon translates it into the [International Phonetic Alphabet (IPA)](https://en.wikipedia.org/wiki/International_Phonetic_Alphabet) for the given language, which may not always produce exactly the pronunciation you want. 

By activating the `isCustomPhonemized` flag on a segment, you mark an entire segment to be interpreted as phonetic IPA script -- meaning you can provide your own bypass transcription to control pronunciation. Every actor's unique voice and accent will still take priority meaning even if the IPA reflects a certain pronunciation the actor will pronounce it as his or her character would with its accent intact. E.g. Elias with his swedish accent will still pronounce a line as if it were read by a swede with an accent.

> [!CAUTION]
> Any non-IPA text in an `isCustomPronounced` segment will be filtered out at synthesis, heavily impacting results.
> 
> Make sure only IPA characters are present in such a segment.


Let's have a little fun with this and try to reenact the Black Speech inscription on The One Ring:
> Ash nazg durbatulûk, ash nazg gimbatul, ash nazg thrakatulûk agh burzum-ishi krimpatul.

In the _SimpleCharacter.cs_ script, replace your segments with the following:
```csharp
List<ThespeonInputSegment> segments = new() {
    new($"{ControlCharacters.Pause}A wizard gave me a ring which says {ControlCharacters.Pause}", emotion: Emotion.Interest),
    new($"aːʃ naːhh dʊːrbɑɑtʊlʊːk {ControlCharacters.Pause} aːʃ naːhh ɡɪːmbɑːtʊːl {ControlCharacters.Pause} aːʃ naːhh θθrɑːkɑːtʊːlʊːk, ahh bʊʊrzʊʊm ɪʃɪ krɪmpɑtʊːl", isCustomPronounced: true, emotion: Emotion.Serenity)
};
```

Run the sample again, and you should hear the actor speak Black Speech.

> [!TIP] 
> Enabling `isCustomPronounced` makes Thespeon bypass some initial steps for that segment, making it slightly more efficient in runtime.
---
# Changing Input-Wide Parameters
The [`ThespeonInput`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoninput) class itself allows you to select defaults for emotion, lanugage and dialect with the optional arguments _defaultEmotion_, _defaultLanguage_ and _defaultDialect_. Whenever a segment does not have a specified parameter, it will fall back on the global default. The default will in turn be selected for you if you do not do so yourself. You may use this field to clean up your code to avoid having to provide the same instructions to several segments.

An unique feature to the Thespeon Input class are the _speed_ and _loudness_ AnimationCurves, controlling the rate of speech and volume over the whole input line. The start of the animation curve applies to the start of the first segment, and the end applies to the end of the last segment. You may create your curves however you like. 

To try this out, replace your input creation with:

```csharp
  var speed = new AnimationCurve(
      new Keyframe(0f, 1f),
      new Keyframe(0.5f, 1.5f),
      new Keyframe(0.9f, 0.7f),
      new Keyframe(1f, 0.5f)
  );

  var loudness = new AnimationCurve(
      new Keyframe(0f, 1f),
      new Keyframe(0.5f, 0.7f),
      new Keyframe(1f, 1f)
  );
List<ThespeonInputSegment> segments = new() {
    new($"{ControlCharacters.Pause}A wizard gave me a ring which says {ControlCharacters.Pause}", emotion: Emotion.Interest),
    new($"aːʃ naːhh dʊːrbɑɑtʊlʊːk {ControlCharacters.Pause} aːʃ naːhh ɡɪːmbɑːtʊːl {ControlCharacters.Pause} aːʃ naːhh θθrɑːkɑːtʊːlʊːk, ahh bʊʊrzʊʊm ɪʃɪ krɪmpɑtʊːl", isCustomPronounced: true, emotion: Emotion.Serenity)
};
  ThespeonInput input = new(segments, actorAsset.actorName, actorAsset.moduleType, defaultEmotion: Emotion.Joy, defaultLanguage: "eng", speed: speed, loudness: loudness);
```
You should hear the speed and loudness of the speech varying as the actor speaks.

> [!NOTE]
> The curves may range beteen 0.5 and 2 for speed and 0.1 and 2 for loudness and will be clamped to fit inside those ranges.

## Controlling the speed and loudness of a specific word or segment
At runtime, the *speed* and *loudness* curves are mapped to each specific character in the ThespeonInput - meaning that if we would like to make a specific region be spoken in a different manner, we have to find the corresponding start and end time relative to the whole input. Using AnimationCurves, we can create different kinds of functions mapping over the whole input. For example, the code below will create a rectangular AnimationCurve in order to modify a single word:

```csharp
string inputText = "Hi! This is my voice generated in real time!";
List<ThespeonInputSegment> segments = new() {
new(inputText),
};
AnimationCurve speed = AnimationCurve.Constant(0, 1, 1);

AnimationCurve loudness = AnimationCurve.Constant(0, 1, 1);

string targetWord = "generated";

// find indices of the specific word
int startIndex = inputText.IndexOf(targetWord);
int endIndex = startIndex + targetWord.Length - 1;

// convert indices to a normalized keyframe time between first and last character
float normStartTime = (float)startIndex / (float)(inputText.Length - 1);
float normEndTime = (float)endIndex / (float)(inputText.Length - 1);

// make the specific word half as slow, and half as loud
Keyframe startKeyFrame = new(normStartTime, 0.5f);
Keyframe endKeyFrame = new(normEndTime, 1f);
// modify tangents so that the keyframes do not affect the speed and loudness of surrounding words
startKeyFrame.inTangent = float.PositiveInfinity;
startKeyFrame.outTangent = 0f;
endKeyFrame.inTangent = float.PositiveInfinity;
endKeyFrame.outTangent = 0f;
// insert keys 
speed.AddKey(startKeyFrame);
speed.AddKey(endKeyFrame);
loudness.AddKey(startKeyFrame);
loudness.AddKey(endKeyFrame);

ThespeonInput input = new(segments, actorAsset.actorName, actorAsset.moduleType, speed: speed, loudness: loudness);
```
> [!NOTE]
> The mapping of specific characters includes **all** characters, including non-audible characters and blank spaces. If an output does not seem to match a substring, verify that surrounding special characters are included in the substring. 

## Next Steps
Check out the [Configuration and Performance Tuning Manual](./thespeon-configuration.md) to learn more about how to control Thespeon's resource consumption and performance.

See the [Thespeon Tools Manual](./thespeon-tools.md) for to learn about more features such as mid-sentence callbacks and inserting pauses in a line.

See the [DemoGUI Sample Guide](./using-the-demogui-sample.md) for a walkthrough of how to use the GUI sample to control your actor interactively.
