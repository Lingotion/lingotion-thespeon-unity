# **Character Control Guide**

# Table of Contents
- [Overview](#overview)
- [The Thespeon Character Asset](#the-thespeon-character-asset)
- [Run the Simple Character](#run-the-simple-character)
- [Changing emotion and language](#changing-emotion-and-language)
- [Controlling pronunciation](#controlling-pronunciation)
- [Changing Input-Wide Parameters](#changing-input-wide-parameters)
- [Next Steps](#next-steps)

---
# Overview
By now you should have been able to hear the Thespeon Engine generate a sample line after having followed the [Get Started - Unity Guide](./get-started-unity.md). This guide will go into more detail on how to select a specific character, and different ways to change how the character speaks a certain line.

To learn how to control your character we will start with the basic _Simple Character_ sample and make a chosen character say the line "Hi! This is my voice generated in real time!" with a happy emotion in English. 
We will successively build on this until we have something closer to _Advanced Character_ sample.

---
# The Thespeon Character Asset
On import of a character pack, Thespeon will generate a [ScriptableObject](https://docs.unity3d.com/Manual/class-ScriptableObject.html) called a _ThespeonCharacterAsset_, which is a representation of that specific character and module type. These are located in the **Assets > Lingotion Thespeon > CharacterAssets** directory.
> [!IMPORTANT]
> If the directory is missing or no ThespeonCharacterAssets are found, press the **Regenerate Input Assets** button in the Thespeon Info Window.
---
# Run the Simple Character
Firstly we want to pick a ThespeonCharacterAsset. Go to the Game Object Hierachy and select the **Example Character** GameObject. In its Inspector window, find the field for **Character Asset** currently set to "None". Click the icon on the right and select one of the assets available, or drag a ThespeonCharacterAsset from the CharacterAsset directory to the field. 

For this guide we recommend a character that can speak at least two dialects or languages. Both Freemium characters, _Elias Granhammar_ and _Denel Honeyball_, have that capability.

Now you can run the Simple Character scene and press the **Space**, **Enter**, or **S** key to listen to the result.

> [!TIP]
> You can change character without recompiling by assigning a new Character Asset in the inspector window and pressing space once more.
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
This changes the emotion of which the character says that particular segment. Try experimenting with different emotions and text lengths!

One may also change the language or and dialect of the speaker, provided that the character supports it. To see what options your imported character supports, go to the **Characters** tab of the Thespeon Info Window, and select a character in the list to see all installed modules. Expand the target module to see the supported languages and dialects.

![Character Information](./data/character-information.png?raw=true "Character Information")

In this case, we see that the character Denel Honeyball has two available dialects in English, "_GB_" and "_US_" being the specific strings to give to the ThespeonInput.

Go back to the _SimpleCharacter.cs_ script and find the _ThespeonInput_ below the list of segments -- change it from:
```csharp
ThespeonInput input = new(segments, characterAsset.characterName, characterAsset.moduleType, defaultEmotion: Emotion.Joy, defaultLanguage: "eng");
```
to:
```csharp
ThespeonInput input = new(segments, characterAsset.characterName, characterAsset.moduleType, defaultEmotion: Emotion.Joy, defaultLanguage: "eng", defaultDialect: "US");
```
and run the sample again. You should hear a change in dialect according to your changes.

> [!TIP]
> For the full list of available emotions, see the [`Emotion` enum in the API documentation](./api/Lingotion_Thespeon_Core.md#enum-emotion). Language codes follow [ISO 639-3](https://en.wikipedia.org/wiki/ISO_639-3) (e.g. `"eng"` for English, `"swe"` for Swedish). Available languages and dialects for your character are shown in the **Characters** tab of the Thespeon Info Window. 
---
# Controlling pronunciation
A very common case in video games is the pronunciation of something that is not necessarily a normal part of the language the character speaks, such as fictional names or single words from other languages. When given an input text, Thespeon translates it into the [International Phonetic Alphabet (IPA)](https://en.wikipedia.org/wiki/International_Phonetic_Alphabet) for the given language, which may not always produce exactly the pronunciation you want. 

By activating the `isCustomPronounced` flag on a segment, you mark an entire segment to be interpreted as phonetic IPA script -- meaning you can provide your own bypass transcription to control pronunciation. Every character's unique voice and accent will still take priority meaning even if the IPA reflects a certain pronunciation the character will pronounce it as his or her character would with its accent intact. E.g. Elias with his swedish accent will still pronounce a line as if it were read by a swede with an accent.

> [!CAUTION]
> Any non-IPA text in an `isCustomPronounced` segment will be filtered out at synthesis, heavily impacting results.
> 
> Make sure only IPA characters are present in such a segment.


Let's have a little fun with this and try to reenact the Black Speech inscription on The One Ring:
> Ash nazg durbatulûk, ash nazg gimbatul, ash nazg thrakatulûk agh burzum-ishi krimpatul.

In the _SimpleCharacter.cs_ script, replace your segments with the lines below. Note the use of `ControlCharacters.Pause` to insert short pauses into the speech — see the [Thespeon Tools Manual](./thespeon-tools.md#controlcharacters) for details on available control characters.
```csharp
List<ThespeonInputSegment> segments = new() {
    new($"{ControlCharacters.Pause}A wizard gave me a ring which says {ControlCharacters.Pause}", emotion: Emotion.Interest),
    new($"aːʃ naːhh dʊːrbɑɑtʊlʊːk {ControlCharacters.Pause} aːʃ naːhh ɡɪːmbɑːtʊːl {ControlCharacters.Pause} aːʃ naːhh θθrɑːkɑːtʊːlʊːk, ahh bʊʊrzʊʊm ɪʃɪ krɪmpɑtʊːl", isCustomPronounced: true, emotion: Emotion.Serenity)
};
```

Run the sample again, and you should hear the character speak Black Speech.

> [!TIP] 
> Enabling `isCustomPronounced` makes Thespeon bypass some initial steps for that segment, making it slightly more efficient in runtime.
---
# Changing Input-Wide Parameters
The [`ThespeonInput`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoninput) class itself allows you to select defaults for emotion, lanugage and dialect with the optional arguments _defaultEmotion_, _defaultLanguage_ and _defaultDialect_. Whenever a segment does not have a specified parameter, it will fall back on the global default. The default will in turn be selected for you if you do not do so yourself. You may use this field to clean up your code to avoid having to provide the same instructions to several segments.

> [!WARNING]
> Speed and loudness curves are currently not supported and will be ignored during synthesis. This feature will return in a future update.

> [!NOTE]
> The mapping of specific characters includes **all** characters, including non-audible characters and blank spaces. If an output does not seem to match a substring, verify that surrounding special characters are included in the substring. 

# Next Steps
Check out the [Configuration and Performance Tuning Manual](./thespeon-configuration.md) to learn more about how to control Thespeon's resource consumption and performance.

See the [Thespeon Tools Manual](./thespeon-tools.md) for to learn about more features such as mid-sentence callbacks and inserting pauses in a line.

See the [DemoGUI Sample Guide](./using-the-demogui-sample.md) for a walkthrough of how to use the GUI sample to control your character interactively.
