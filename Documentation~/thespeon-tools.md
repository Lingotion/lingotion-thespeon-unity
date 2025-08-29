# Thespeon Tools Manual
Aside from Thespeons main generative capabilities, the package also offers a number of tools to help you handle both the input and output of Thespeon in an intuitive way. In this markdown we will list the tools you have at your disposal besides ThespeonEngine itself and their intended use.  

---

## Table of Contents

- [Inputs Namespace](#inputs-namespace)
	- [**ThespeonCharacterAsset**](#thespeoncharacterasset)
	- [**ControlCharacters**](#controlcharacters)
	- [**ThespeonCharacterHelper**](#thespeoncharacterhelper)
- [Utils Namespace](#utils-namespace)
	- [**WavExporter**](#wavexporter)

---

## [Inputs Namespace](./api/Public%20API/Lingotion_Thespeon_Inputs.md)

At the core of the [`Lingotion.Thespeon.Inputs`](./api/Public%20API/Lingotion_Thespeon_Inputs.md) namespace lies the `ThespeonInput` and `ThespeonInputSegment` classes we are familiar with from earlier [guides](./get-started-unity.md), but it also offers the following tools to help you easily create input instances in your code.

### **[ThespeonCharacterAsset](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoncharacterasset)**

The [`ThespeonCharacterAsset`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoncharacterasset) is a ScriptableObject containing an `actorName` and a `ModuleType` and represents a selection of a specific actor and module type to run. The Lingotion Thespeon package will dynamically generate a number of these for you to reflect the actor packs you have imported. You will find an up to date set of these under your `Assets` > `Lingotion` > `CharacterAssets` folder. Each of these holds a valid combination of an `actorName` and a `ModuleType`, see the [SimpleCharacter](../Samples~/Simple%20Character/SimpleCharacter.cs) sample for an example of how to use a `ThespeonCharacterAsset` in your project. You may also create your own assets under `Assets/Create/Lingotion/Character` but with loss of validation.

---

### **[ControlCharacters](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-controlcharacters)**
Thespeon utilizes some special characters for special purposes. These characters are not normally found on a keyboard and as such we have collected the supported characters in the [`ControlCharacters`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-controlcharacters) static class for easier access.

Below is a list of the currently available options:
#### `ControlCharacters.Pause`
The pause character will insert a short pause in the spoken dialogue. Several of these can be chained to achieve a longer pause.
For example, an input with the text `$"Hi!{ControlCharacters.Pause} This is a sample text!"` will cause a pause between the two sentences, making the silence longer than it would be in normal sentence flow without the pause character.

#### `ControlCharacters.AudioSampleRequest`
The audio sample request character will act as a marker through the synthesis process. If the ThespeonInput contains any AudioSampleRequests, then the first packet returned on synthesis will contain a `Queue<int>`, providing the audio sample index that corresponds to the place in the sentence where the character was placed.

For example, if we input the following: `$"Hi!{ControlCharacters.AudioSampleRequest} This is a {ControlCharacters.AudioSampleRequest}sample text!"`, then the queue would contain two ints in the order of leftmost to rightmost in the string. 
This can be used to provide timing-specific callbacks in the middle of a sentence or where a specific word is uttered. See the _Audio Callback Sample_ for an example of how to use this.

--- 
### **[ThespeonCharacterHelper](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoncharacterhelper)**
In some situations handling a number of `ThespeonCharacterAssets` is not suitable - such as when actors are selected during play mode and not in edit mode. To cover these cases the [`ThespeonCharacterHelper`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoncharacterhelper) class provides a couple of `static` methods to help you select the actor you want. 

The first of the methods below returns a list of two-element tuples - one for each currently valid actor-module type combination. If you already know which actor you intend to use then the second will return a list of all ModuleTypes available for that actor.

```csharp
List<(string characterName, ModuleType moduleType)> GetAllCharactersAndModules()
public static List<ModuleType> GetAllModulesForCharacter(string characterName)
```

---
## [Utils Namespace](./api/Public%20API/Lingotion_Thespeon_Utils.md)
The purpose of the [`Lingotion.Thespeon.Utils`](./api/Public%20API/Lingotion_Thespeon_Utils.md) namespace it to provide general utilities that help you make Thespeon fit into your project in the way you want. Below you will find a description of the the set of tools currently available.

### **[WavExporter](./api/Public%20API/Lingotion_Thespeon_Utils.md#class-wavexporter)**
In all of the package samples the audio generated by Thespeon is directly streamed to an active `AudioClip` but there are circumstances where real-time consumption is not preferred. In Edit Mode, you can access the Audio Test Lab via the Thespeon Info Window, typically found under Window > Lingotion > Thespeon Info in the Unity Editor, where you can generate and save .wav files for later use. However, if your dialogue script is runtime dependent then you might want to generate audio whenever you have the time and resources to do so, and stow the results away for later use. 

The [WavExporter](./api/Public%20API/Lingotion_Thespeon_Utils.md#class-wavexporter) class enables saving your generated audio to a wav file for later loading. Below is a snippet of code for how you can easily do that after a synthesis.


```csharp
    private List<float> audioData = new();
    void Start()
    {
        _engine = GetComponent<ThespeonEngine>();
        // Connect callbacks
        _engine.OnAudioReceived += OnAudioPacketReceive;
        _engine.OnSynthesisComplete += OnSynthComplete;
        //...
    }

    //...

    void OnAudioPacketReceive(float[] data, PacketMetadata metadata)
    {
        lock (_audioData)
        {
            _audioData.AddRange(data);
        }
    }

    void OnSynthComplete(PacketMetadata metadata)
    {
        lock (_audioData)
        {
            WavExporter.SaveWav("Assets/output.wav", _audioData.ToArray());
        }
    }
```