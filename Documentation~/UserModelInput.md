
Below is a comprehensive documentation of the **UserModelInput** class and its related classes which serve as the input format for Thespeon Engine synthesis in your Unity project. This documentation describes each field’s intent, usage, and any validation rules or optional behaviors. Use this documentation as a reference when integrating the input format into your Unity workflow.

---
## Table of Contents

- [Table of Contents](#table-of-contents)
- [1. Overview](#1-overview)
- [2. Classes](#2-classes)
  - [2.1. **UserModelInput**](#21-usermodelinput)
    - [Fields](#fields)
    - [Methods](#methods)
  - [2.2. **UserSegment**](#22-usersegment)
    - [Fields](#fields-1)
  - [2.3. **Language**](#23-language)
    - [Explanation:](#explanation)
    - [Explanation:](#explanation-1)
  - [2.4. **Where to Find Info**](#24-where-to-find-info)
- [3. JSON-like Structure](#3-json-like-structure)
- [4. Examples](#4-examples)
  - [4.1. **Minimum viable constructor in Unity**](#41-minimum-viable-constructor-in-unity)
  - [4.2. **Advanced creative control constructor in Unity**](#42-advanced-creative-control-constructor-in-unity)
  - [4.3. **JSON Input (Deserialization)**](#43-json-input-deserialization)
- [5. Validation](#5-validation)
- [5. Emotion Set Documentation](#5-emotion-set-documentation)
  - [Ecstasy](#ecstasy)
  - [Admiration](#admiration)
  - [Terror](#terror)
  - [Amazement](#amazement)
  - [Grief](#grief)
  - [Loathing](#loathing)
  - [Rage](#rage)
  - [Vigilance](#vigilance)
  - [Joy](#joy)
  - [Trust](#trust)
  - [Fear](#fear)
  - [Surprise](#surprise)
  - [Sadness](#sadness)
  - [Disgust](#disgust)
  - [Anger](#anger)
  - [Anticipation](#anticipation)
  - [Serenity](#serenity)
  - [Acceptance](#acceptance)
  - [Apprehension](#apprehension)
  - [Distraction](#distraction)
  - [Pensiveness](#pensiveness)
  - [Boredom](#boredom)
  - [Annoyance](#annoyance)
  - [Interest](#interest)
  - [Emotionless](#emotionless)
  - [Contempt](#contempt)
  - [Remorse](#remorse)
  - [Disapproval](#disapproval)
  - [Awe](#awe)
  - [Submission](#submission)
  - [Love](#love)
  - [Optimism](#optimism)
  - [Aggressiveness](#aggressiveness)
- [7. Common Questions / FAQ](#7-common-questions--faq)

---

## 1. Overview

The `UserModelInput` class (and its supporting classes) define the data necessary for configuring and submitting text segments, along with annotations such as language, emotion, speed, loudness, and pitch to the Thespeon Engine backend.  

You will typically:
1. Construct a `UserModelInput` object in Unity, either via constructor or by deserializing a JSON file/string (see [Examples](#4-examples)).  
2. Optionally call its validation method  `ValidateAndWarn()` to get feedback on your input before synthesizing.  
3. Pass the populated object to the Thespeon Engine API methods. E.g. ThespeonEngine.Synthesize(UserModelInput)

---

## 2. Classes
For information on where to find valid data to enter: see [Where to Find Info](#24-where-to-find-info)
### 2.1. **UserModelInput**

```csharp
public class UserModelInput
{
  [JsonProperty("moduleName", Required = Required.Always)]
  public string moduleName { get; set; }

  [JsonProperty("actorUsername", Required = Required.Always)]
  public string actorUsername { get; set; } = "";

  [JsonProperty("defaultLanguage", NullValueHandling = NullValueHandling.Ignore)]
  public Language? defaultLanguage { get; set; }

  [JsonProperty("defaultEmotion", NullValueHandling = NullValueHandling.Ignore)]
  public string? defaultEmotion { get; set; }

  [JsonProperty("segments", Required = Required.Always)]
  public List<UserSegment> segments { get; set; } = new();

  [JsonProperty("speed", NullValueHandling = NullValueHandling.Ignore)]
  public List<double> speed { get; set; }

  [JsonProperty("loudness", NullValueHandling = NullValueHandling.Ignore)]
  public List<double> loudness { get; set; }

    //This field captures extra data that is entered by mistake and is not taken into account during synthesis.
    [JsonExtensionData]
    public Dictionary<string, JToken>? extraData { get; set; }

    //Validation of input
    public (List<string> errors, List<string> warnings, float) ValidateAndWarn()
    {

        // ...some Validation logic...
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}
```

#### Fields

- **moduleName** (`string`, **Required**):  
  A unique identifier for the actor module you want to use. 

- **actorUsername** (`string`, **Required**):  
  Identifies the “actor” or speaker.  
  - Must be a non-empty string specifying the actor's username for this synth request.

- **defaultLanguage** (`Language?`, **Optional, but highly recommended**): 
  Specifies the default language for the entire input.  
  - Typically this is an object specifying language properties (see [Language](#23-language) for details). 
  - If not specified, the first language found for specified module will be used. We therefore highly recommended to specify a default language.
  - If non-valid with respect to , a closest match 

- **defaultEmotion** (`string?`, **Optional, but highly recommended**):  
  Specifies a default emotion.  
  - If left blank, a default emotion will apply.

- **segments** (`List<UserSegment>`, **Required**):  
  A list of text segments. Each `UserSegment` provides textual content and its own optional overrides for language, emotion, etc.  
  - Must contain at least one `UserSegment` for valid input.  
  - The total text is a sequential concatenation of all segment texts. 
  - A break between two segments must be placed between words. Having a segment break in the middle of a word will split that word into two and be treated as such.
  - See [UserSegment](#22-usersegment).

- **speed** (`List<double>`, *Optional*):  
  An optional list of non-negative speed multipliers to apply to the entire input text affecting the rate of speech on a per-character basis. Length of list is arbitrary.
  - If not present, no modifications are applied.  
  - 1 is default speed, 2 is double the speed, 0.5 is half of default speed.
  - If the list length does not match the number of characters, upsampling or downsampling will be performed by our backed.
    - Upsampling: Linear interpolation fills the list when it is shorter than the text provided. For single element lists, its element is applied to all characters.
    - Downsampling: Cubic Splines and resampling are used when list is longer than the text provided.
  - A lower limit has been set to 0.1x speed. Do note that low speed means longer audio and therefore heavier synthesis.

- **loudness** (`List<double>`, *Optional*):  
  An optional list of non-negative loudness multipliers to apply  to the entire input text affecting volume on a per-character basis. Length of list is arbitrary.
  - If not present, no global speed modifications are applied.  
  - Same scale and sampling rules as for speed.

- **extraData** (`Dictionary<string, JToken>?`, *Optional*):  
  An open-ended dictionary for any additional fields not formally defined in this class.  
  - These extra fields are used to collect any extra data passed in deserialization but will be ignored during synthesis.

#### Methods

- **ValidateAndWarn()**:  
  Ensures valid input with respect to two levels of "severity", errors and warnings.  
  - Throws an `InvalidOperationException` on any severe violation. 
  - Logs informative warnings highlighting when non-required attributes are invalid or not specified (for example `defaultLanguage`), but input will be processed anyway!
  - See more details on [Validation](#5-validation).
 
- **ToString()**:  
  Returns a pretty-printed JSON string of this object, which can be useful for reviewing and debugging.  

---

### 2.2. **UserSegment**

```csharp
public class UserSegment
{
    [JsonProperty("text", Required = Required.Always)]
    public string? text { get; set; }

    [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
    public Language? languageObj { get; set; }

    [JsonProperty("emotion", NullValueHandling = NullValueHandling.Ignore)]
    public string? emotion { get; set; }

    [JsonProperty("IsCustomPhonemized", NullValueHandling = NullValueHandling.Ignore)]
    public bool? isCustomPhonemized { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? extraData { get; set; }


}
```

#### Fields

- **text** (`string?`, **Required**):  
  The textual content of this segment.  
  - Must be non-null and non-empty.  
  - The total text is a sequential concatenation of all segment texts with eventual insertion of whitespace on join if not already present, meaning segment breaks inside a word will split that word into two.
  - The text will not take capitalization into consideration and will always turn all characters to lower case on synthesis.


- **languageObj** (`Language?`, *Optional*):  
  Overrides the `defaultLanguage` if you want a different language for this segment.  

- **emotion** (`string?`, *Optional*):  
  Overrides `defaultEmotion` if the text in this segment should have a different emotion annotation.  

<!---
- **style** (`string?`, *Optional*):  
  An additional style descriptor. Could be used for advanced styling, e.g. “whisper,” “shout,” or other custom speech style cues.  
-->

- **isCustomPhonemized** (`bool?`, *Optional*):  
  Indicates if this segment is already phonemized by custom phoneme text in segment's `text`
  - Full creative control of pronounciation is made possible by passing phonetic text (IPA) and marking it as such. All characters in this segment that are not part of the supported corpus of IPA characters will be ignored.
  - Use case example: Fictional in-game words and names with some custom pronounciation. Eg. glantherix is passed as `ˈɡlæn.θə.rɪks` as the segment's `text` and `isCustomPhonemized` is flagged as `true`.

<!---
- **heteronymDescription** (`Dictionary<string,string>`, *Optional*):  
  A place to disambiguate or define heteronyms for this segment’s text.  
  - Key: The heteronym word.  
  - Value: The intended meaning or phonetic hint.  
-->

- **extraData** (`Dictionary<string, JToken>?`, *Optional*):  
  As with `UserModelInput.extraData`, any undefined extra fields will be collected here.  

---

### 2.3. **Language**

```csharp
public class Language
{
    [JsonProperty("iso639_2", Required = Required.Always)]
    public string iso639_2 { get; set; } = "";

    [JsonProperty("iso639_3", NullValueHandling = NullValueHandling.Ignore)]
    public string? iso639_3 { get; set; }

    [JsonProperty("glottocode", NullValueHandling = NullValueHandling.Ignore)]
    public string? glottoCode { get; set; }

    [JsonProperty("iso3166_1", NullValueHandling = NullValueHandling.Ignore)]
    public string? iso3166_1 { get; set; }

    [JsonProperty("iso3166_2", NullValueHandling = NullValueHandling.Ignore)]
    public string? iso3166_2 { get; set; }

    [JsonProperty("customdialect", NullValueHandling = NullValueHandling.Ignore)]
    public string? customDialect { get; set; }

}
```

**Represents a language object** with optional support for various ISO standards and dialect codes. The primary required field is **iso639_2**. Other properties may be null or omitted. For better synth results you should use languages that are supported by the actor chosen otherwise a closest match will be selected for you and might lead to unexpected behavior.

**Fields**  
- **[iso639_2](https://en.wikipedia.org/wiki/ISO_639-2)** (`string`): *Required.* Primary ISO 639-2 code.  
- **[iso639_3](https://en.wikipedia.org/wiki/ISO_639-3)** (`string?`): Optional ISO 639-3 code.  
- **[glottoCode](https://content.iospress.com/articles/semantic-web/sw212843)** (`string?`): Optional Glottocode (linguistic database identifier).  
- **[iso3166_1](https://en.wikipedia.org/wiki/ISO_3166-1)** (`string?`): Optional ISO 3166-1 (country) code.  
- **[iso3166_2](https://en.wikipedia.org/wiki/ISO_3166-2)** (`string?`): Optional ISO 3166-2 (subdivision) code.  
- **customDialect** (`string?`): Optional user-defined dialect identifier. See [Where to Find Info](#24-where-to-find-info)


Examples:

```json

{
    iso639_2 = "eng",            // ISO 639-2 code for English
    iso639_3 = "eng",            // ISO 639-3 code for English
    glottoCode = "stan1293",     // Glottocode for Standard American English
    iso3166_1 = "US",            // Country code for the United States
    customDialect = "General American" // Custom dialect specification
}

```

#### Explanation:
- `iso639_2 = "eng"` → Standard **ISO 639-2** code for English. 
- `iso639_3 = "eng"` → **ISO 639-3** also defines English as `"eng"`.
- `glottoCode = "stan1293"` → **Glottolog** identifier for Standard American English.
- `iso3166_1 = "US"` → **ISO 3166-1** country code for the United States.
- `customDialect = "General American"` → Custom field to indicate **General American** as the specific variety.



This ensures that **General American English** is explicitly specified within the `Language` class, distinguishing it from other English dialects like British English or Australian English.
 





```json

{
    iso639_2 = "eng",            // ISO 639-2 code for English
    iso639_3 = "eng",            // ISO 639-3 code for English
    glottoCode = "sout3311",     // Glottocode for Standard American English
    iso3166_1 = "US",            // Country code for the United States
    customDialect = "Southern American English" // Custom dialect specification
}

```

#### Explanation:
- `iso639_2 = "eng"` → **ISO 639-2** code for English.
- `iso639_3 = "eng"` → **ISO 639-3** code for English.
- `glottoCode = "sout3311"` → **Glottolog code** for Southern American English.
- `iso3166_1 = "US"` → **Country code** for the United States.
- `customDialect = "Southern American English"` → Specifies the **Southern dialect** explicitly


This distinguishes **Southern American English** from **General American English** and other English varieties. If you want to be even more specific (e.g., Appalachian, Texan, Cajun English), you could refine `customDialect` further, such as:

```csharp
customDialect = "Appalachian English"; // For Appalachian dialect
customDialect = "Texan English"; // For Texan dialect
customDialect = "Cajun English"; // For Cajun English
``` 

---
When non-native actors speak a foreign language, they often carry over phonetic patterns, intonation, and rhythm from their native language, which creates a distinct way of speaking the foreign language. For instance the following is general english spoken by a swede! 🇸🇪🤝🇺🇸

```json
"language": {
    "iso639_2": "eng",
    "iso3166_1": "SE",
    "iso3166_2": "SE-Y",

}

```


<!---
**Key Methods**  
- **`GetItems()`**: Returns a dictionary of property names to their current values (as strings).  
- **`ToString()`** and **`ToDisplay()`**: Provide textual representations of the language object.  
- **`Equals(...)` & `GetHashCode()`**: Compare or hash objects based on all fields except.  
- **`MatchLanguage(...)`**: Checks if every non-empty property in one `Language` matches the corresponding property in another, ignoring empty or null fields.
-->
---
### 2.4. **Where to Find Info**
In this section you will find instructions on where to find valid information for each input specifier. Each valid entry can often be found either programmatically or in a provided tool. Below the System.LINQ library is utilized for convenience and brevity. 
Do note that for a module to be usable, its ActorPack needs to be registered first using `ThespeonAPI.RegisterActorPack(string actorUsername)`.

- **moduleName** (`string`):  
  After import of an ActorPack the Lingotion Thespeon Info Editor Window will list the available actors under Imported Actors Overview. Under each actor is an expandable list of Modules each with an item **Module: moduleName**.  
  Programatically, a list of available moduleNames for a given **actorUsername**, assuming its pack has been registered, can be retrieved using
    `ThespeonAPI.GetModulesWithActor(actorUsername).Select(module => module.name)` 
  
- **actorUsername** (`string`):  
  After import of an ActorPack the Lingotion Thespeon Info Editor Window will list the available actors under Imported Actors Overview.
  Programatically, a list of available moduleNames can be retrieved using
    `ThespeonAPI.GetActorsAvailabeOnDisk();` 
  
- **defaultLanguage** and **language** (`Language`): 
  After import of an ActorPack, actors' supported languages will be listed under each actor in the Lingotion Thespeon Info Editor Window under Imported Actors Overview. Using a language requires the corresponding Language Pack. If you lack any Language Pack for any of the supported languages the Lingotion Thespeon Info Editor Window will warn you. After import of a Language Pack the language(s) of each Language Pack is shown under Imported Languages Overview.
  Programatically, a list of available languages for a given **actorUsername** and **moduleName** can be retrieved using
    `ThespeonAPI.GetModulesWithActor(actorUsername).Where(module => module.name == moduleName).First().language_options.languages`
    ``
  
    

- **defaultEmotion** and **emotion** (`string`):  
  All actors support our current full range of emotions which consists of the 33 emotions in the image below.
  ![Alt text](./data/EmotionWheel.png?raw=true "Emotions")
  Programatically, a list of available emotions for a selected `actorUsername` can be retrieved in the following way: 
  - First an ActorPack has to be registered using `RegisterActorPack(string actorUsername)`  
  - The list of emotions can then be retrieved directly using the returned ActorPack object or at a later time by fetching the same object using `ThespeonAPI.GetRegisteredActorPacks()[actorUsername]`. The list is retrieved by the following:
    `actorPackObject.GetEmotions().Select(emotion => emotion.emotionsetname)`
    or alternatively
    `ThespeonAPI.GetModulesWithActor(actorUsername).Where(module => module.name == moduleName).First().emotion_options.emotions`

  
- **speed** (`List<double>`):  
  Any double values will work. Values below 0.1 will be clamped to 0.1. 
    Do note that extreme values might lead to poor quality.
  

- **loudness** (`List<double>`):  
  * Any double values will work. Values below 0 will be clamped to 0 (but 0 will lead to total silence). 
    Do note that extreme values might lead to poor quality.


---
## 3. JSON-like Structure

The UserModelInput class plays well with the JSON format and can be serialized and deserialized at will. Here’s how the JSON generally looks when creating or receiving a `UserModelInput` object:

```json
{
  "moduleName": "someUniqueModuleName",
  "defaultLanguage": {
    "iso639_2": "eng"
    // Additional Language properties here (iso639_3, glottocode, etc.)
  },
  "defaultEmotion": "Joy",
  "actorUsername": "actor123",
  "speed": [1.2, 0.95],
  "loudness": [0.8],
  "segments": [
    {
      "text": "Hello world!",
      "language": {
        "iso639_2": "eng"
      },
      "emotion": "Fear",
      "IsCustomPhonemized": false,

    },
    {
      "text": "ɡlæn.θə.rɪks wɜːrld",
      "language": {
        iso639_2 = "eng",            // ISO 639-2 code for English
        iso639_3 = "eng",            // ISO 639-3 code for English
        glottoCode = "stan1293",     // Glottocode for Standard American English
        iso3166_1 = "US",            // Country code for the United States
        customDialect = "General American" // Custom dialect specification
      },
      "emotion": "Fear",
      "IsCustomPhonemized": true,

    },
    {
      "text": "Some more text with default settings"
    }
    
  ]
}
```

Notes:
- **`moduleName`, `actorUsername`, and `segments` are always required**.  
- **`defaultLanguage` and `defaultEmotion` are optional but highly recommended, as it gives you control over the synthesis. Otherwise the first alternatives for the chosen module are selected.**
- **You will get a warning in Unity console if `defaultLanguage` or `defaultEmotion` is missing. Dismiss it if you are aware of your choice!**.   
- **`speed` and `loudness`** lists are optional with arbitrary lengths.  
- **`segments`** must contain at least one word in `text`.  

---

## 4. Examples

Here are some examples of how to construct the UserModelInput class in your Unity project to later be used for synthesis. The class itself can be found under the `Lingotion.Thespeon.Utils` namespace.
### 4.1. **Minimum viable constructor in Unity**
```csharp
using UnityEngine;
using Newtonsoft.Json;
using Lingotion.Thespeon.API;
using Lingotion.Thespeon.Utils;

public class ExampleUsage : MonoBehaviour
{
    void Start()
    {
      string actorUsername = "actorXYZ";
      string moduleName = "myVoiceModule";
      ThespeonAPI.RegisterActorPack(actorUsername);
      
        UserModelInput input = new UserModelInput
        {
            moduleName = moduleName,
      actorUsername = actor,
            segments = new List<UserSegment>
            {
                new UserSegment
                {
                    text = "Hello, world!",
                }
            },
        };

        // Optional but recommended: Validate Input
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();
        (errors, warnings)= input.ValidateAndWarn();

        // Serialize to JSON:
        string json = JsonConvert.SerializeObject(input, Formatting.Indented);
        Debug.Log("Serialized JSON: " + json);
    }
}
```



### 4.2. **Advanced creative control constructor in Unity**

```csharp
using UnityEngine;
using Newtonsoft.Json;
using Lingotion.Thespeon.API;
using Lingotion.Thespeon.Utils;


public class ExampleUsage : MonoBehaviour
{
    void Start()
    {
        UserModelInput input = new UserModelInput
        {
            moduleName = "myVoiceModule",
      actorUsername = "actorXYZ",
            defaultLanguage = new Language
            {
                iso639_2 = "eng",
                iso639_3 = "eng",
                customDialect = "US"
            },
            defaultEmotion = "Emotionless",
            segments = new List<UserSegment>
            {
                new UserSegment
                {
                    text = "Hello, world!",
                    emotion = "Joy"
                }
            },
            speed = new List<double> { 1.0 },
            loudness = new List<double> { 1.0 }
        };

        // Optional but recommended: Validate
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();
        (errors, warnings)= input.ValidateAndWarn();

        // Serialize to JSON:
        string json = JsonConvert.SerializeObject(input, Formatting.Indented);
        Debug.Log("Serialized JSON: " + json);
    }
}
```

### 4.3. **JSON Input (Deserialization)**

```csharp
string incomingJson = /* Possibly loaded from a file or a web request */;
UserModelInput userModel = JsonConvert.DeserializeObject<UserModelInput>(incomingJson);

// Now you can access all the fields:
Debug.Log("Module Name: " + userModel.moduleName);
if (userModel.segments != null && userModel.segments.Count > 0)
{
    UserSegment firstSegment = userModel.segments[0];
    Debug.Log("First segment text: " + firstSegment.text);
}
```

---

## 5. Validation

1. **`UserModelInput.ValidateAndWarn()`**  
   - The `ValidateAndWarn` method in the `UserModelInput` class is designed to validate the user input and provide feedback in the form of errors and warnings. This method ensures that the required fields are present and checks for potential issues in optional fields. 
   - Errors:
        - Checks if required fields (`moduleName`, `defaultLanguage`, `actorUsername`) are present and valid.
        - Validates that the `segments` list is not empty and that each segment has non-empty text.
    - Warnings:
        - Issues warnings if optional fields (`defaultEmotion`, `speed`, `loudness`) are missing or invalid and displays the valid options.
        - Provides feedback on potential issues without halting the process.
    - Logging:

        - Logs errors and warnings using Unity's `Debug.LogError` and `Debug.LogWarning` methods.
        - Throws an `InvalidOperationException` if any critical validation errors are found, ensuring that the calling code is immediately aware of the issues.
        Return Value:

    - Returns
        - a tuple containing a list of (string) errors and a list (string) of warnings. 



These methods help ensure your data is consistent and can be safely processed before initiating synthesis.

---


## 5. Emotion Set Documentation

This section outlines a set of defined emotions, including similar terms, typical sensations, the underlying message each emotion conveys, and real-world examples to illustrate the emotional experience.

---

### Ecstasy
- **Similar Words:** Delighted, Giddy  
- **Typical Sensations:** Abundance of energy  
- **Message:** This is better than I imagined.  
- **Example:** Feeling happiness beyond imagination, as if life is perfect at this moment.  

---

### Admiration
- **Similar Words:** Connected, Proud  
- **Typical Sensations:** Glowing  
- **Message:** I want to support the person or thing.  
- **Example:** Meeting your hero and wanting to express deep appreciation.  

---

### Terror
- **Similar Words:** Alarmed, Petrified  
- **Typical Sensations:** Hard to breathe  
- **Message:** There is big danger.  
- **Example:** Feeling hunted and fearing for your life.  

---

### Amazement
- **Similar Words:** Inspired, WOWed  
- **Typical Sensations:** Heart stopping  
- **Message:** Something is totally unexpected.  
- **Example:** Discovering a lost historical artifact in an abandoned building.  

---

### Grief
- **Similar Words:** Heartbroken, Distraught  
- **Typical Sensations:** Hard to get up  
- **Message:** Love is lost.  
- **Example:** Losing a loved one in an accident.  

---

### Loathing
- **Similar Words:** Disturbed, Horrified  
- **Typical Sensations:** Bileous & vehement  
- **Message:** Fundamental values are violated.  
- **Example:** Seeing someone exploit others for personal gain.  

---

### Rage
- **Similar Words:** Overwhelmed, Furious  
- **Typical Sensations:** Pounding heart, seeing red  
- **Message:** I am blocked from something vital.  
- **Example:** Being falsely accused and not believed by authorities.  

---

### Vigilance
- **Similar Words:** Intense, Focused  
- **Typical Sensations:** Highly focused  
- **Message:** Something big is coming.  
- **Example:** Watching over your child climbing a tree, ready to catch them if they fall.  

---

### Joy
- **Similar Words:** Excited, Pleased  
- **Typical Sensations:** Sense of energy and possibility  
- **Message:** Life is going well.  
- **Example:** Feeling genuinely happy and optimistic in conversation.  

---

### Trust
- **Similar Words:** Accepting, Safe  
- **Typical Sensations:** Warm  
- **Message:** This is safe.  
- **Example:** Trusting someone to be loyal and supportive.  

---

### Fear
- **Similar Words:** Stressed, Scared  
- **Typical Sensations:** Agitated  
- **Message:** Something I care about is at risk.  
- **Example:** Realizing you forgot to prepare for a major presentation.  

---

### Surprise
- **Similar Words:** Shocked, Unexpected  
- **Typical Sensations:** Heart pounding  
- **Message:** Something new happened.  
- **Example:** Walking into a surprise party.  

---

### Sadness
- **Similar Words:** Bummed, Loss  
- **Typical Sensations:** Heavy  
- **Message:** Love is going away.  
- **Example:** Feeling blue and unmotivated.  

---

### Disgust
- **Similar Words:** Distrust, Rejecting  
- **Typical Sensations:** Bitter & unwanted  
- **Message:** Rules are violated.  
- **Example:** Seeing someone put a cockroach in their food to avoid paying.  

---

### Anger
- **Similar Words:** Mad, Fierce  
- **Typical Sensations:** Strong and heated  
- **Message:** Something is in the way.  
- **Example:** Finding your car blocked by someone who left their car unattended.  

---

### Anticipation
- **Similar Words:** Curious, Considering  
- **Typical Sensations:** Alert and exploring  
- **Message:** Change is happening.  
- **Example:** Waiting eagerly for a long-awaited promise to be fulfilled.  

---

### Serenity
- **Similar Words:** Calm, Peaceful  
- **Typical Sensations:** Relaxed, open-hearted  
- **Message:** Something essential or pure is happening.  
- **Example:** Enjoying peaceful time with loved ones without stress.  

---

### Acceptance
- **Similar Words:** Open, Welcoming  
- **Typical Sensations:** Peaceful  
- **Message:** We are in this together.  
- **Example:** Welcoming a new person into your friend group.  

---

### Apprehension
- **Similar Words:** Worried, Anxious  
- **Typical Sensations:** Cannot relax  
- **Message:** There could be a problem.  
- **Example:** Worrying about the outcome of an unexpected meeting.  

---

### Distraction
- **Similar Words:** Scattered, Uncertain  
- **Typical Sensations:** Unfocused  
- **Message:** I don’t know what to prioritize.  
- **Example:** Struggling to focus during a conversation.  

---

### Pensiveness
- **Similar Words:** Blue, Unhappy  
- **Typical Sensations:** Slow & disconnected  
- **Message:** Love is distant.  
- **Example:** Feeling uninterested in suggested activities.  

---

### Boredom
- **Similar Words:** Tired, Uninterested  
- **Typical Sensations:** Drained, low energy  
- **Message:** The potential for this situation is not being met.  
- **Example:** Finding nothing enjoyable to do.  

---

### Annoyance
- **Similar Words:** Frustrated, Prickly  
- **Typical Sensations:** Slightly agitated  
- **Message:** Something is unresolved.  
- **Example:** Being irritated by repetitive behavior.  

---

### Interest
- **Similar Words:** Open, Looking  
- **Typical Sensations:** Mild sense of curiosity  
- **Message:** Something useful might come.  
- **Example:** Becoming curious when hearing unexpected news.  

---

### Emotionless
- **Similar Words:** Detached, Apathetic  
- **Typical Sensations:** No sensation or feeling at all  
- **Message:** This does not affect me.  
- **Example:** Feeling nothing during a conversation about irrelevant topics.  

---

### Contempt
- **Similar Words:** Distaste, Scorn  
- **Typical Sensations:** Angry and sad at the same time  
- **Message:** This is beneath me.  
- **Example:** Feeling disdain toward someone’s dishonest behavior.  

---

### Remorse
- **Similar Words:** Guilt, Regret, Shame  
- **Typical Sensations:** Disgusted and sad at the same time  
- **Message:** I regret my actions.  
- **Example:** Wishing you could undo a hurtful action.  

---

### Disapproval
- **Similar Words:** Dislike, Displeasure  
- **Typical Sensations:** Sad and surprised  
- **Message:** This violates my values.  
- **Example:** Rejecting a statement that contradicts your beliefs.  

---

### Awe
- **Similar Words:** Astonishment, Wonder  
- **Typical Sensations:** Surprise with a hint of fear  
- **Message:** This is overwhelming.  
- **Example:** Being speechless when meeting your idol.  

---

### Submission
- **Similar Words:** Obedience, Compliance  
- **Typical Sensations:** Fearful but trusting  
- **Message:** I must follow this authority.  
- **Example:** Obeying a trusted figure’s orders without question.  

---

### Love
- **Similar Words:** Cherish, Treasure  
- **Typical Sensations:** Joy with trust  
- **Message:** I want to be with this person.  
- **Example:** Feeling deep connection and joy with someone.  

---

### Optimism
- **Similar Words:** Cheerfulness, Hopeful  
- **Typical Sensations:** Joyful anticipation  
- **Message:** Things will work out.  
- **Example:** Seeing the positive side of any situation.  

---

### Aggressiveness
- **Similar Words:** Pushy, Self-assertive  
- **Typical Sensations:** Driven by anger  
- **Message:** I must remove obstacles.  
- **Example:** Forcing your viewpoint aggressively.  

---

## 7. Common Questions / FAQ

1. **Is `defaultEmotion` mandatory?**  
   - No, but highly recommended. If not provided, you’ll see a warning in the Unity console and a default emotion will be selected for you.  

2. **What happens if I omit `speed` or `loudness`?**  
   - They will remain default and no global speed/loudness modifications will be applied.  

3. **Do I need to supply `languageObj` inside every `UserSegment`?**  
   - Only if a particular segment’s language differs from `defaultLanguage`. Otherwise, it can be omitted.  

4. **Do I need to supply `emotion` inside every `UserSegment`?**  
   - Only if a particular segment’s emotion differs from `defaultEmotion`. Otherwise, it can be omitted.  



---
