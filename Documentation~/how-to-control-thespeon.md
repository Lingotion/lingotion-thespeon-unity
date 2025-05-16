# **Thespeon Advanced Control Guide**

By now you should have been able to hear the Thespeon Engine generate a sample line after having followed the [Thespeon Unity Integration Guide](./get-started-unity.md), and perhaps you have even started playing around with changing the sample input. 

This guide will go into more detail on how to use the [UserModelInput](./api/Lingotion_Thespeon_API.md#class-usermodelinput) class to influence how the actor speaks. Feel free to follow along by editing the [SimpleNarrator](./get-started-unity.md#step-3-run-the-simple-narrator-sample) sample and listen to the results of each section.

---
# Table of Contents
- [**Table of Contents**](#table-of-contents)
- [1. **Base Case and Overview**](#1-base-case-and-overview)
- [2. **Changing Basic Parameters**](#2-changing-basic-parameters)
  - [2.1. **Actor and Tags**](#21-actor-and-tags)
  - [2.2. **Text**](#22-text)
    - [**Pauses**](#pauses)
  - [2.3. **Default Emotion**](#23-default-emotion)
  - [2.4. **Default Language**](#24-default-language)
- [3. **Changing Advanced Parameters**](#3-changing-advanced-parameters)
  - [3.1. **Emotion**](#31-emotion)
  - [3.2. **Language**](#32-language)
  - [3.3. **Speed and Loudness**](#33-speed-and-loudness)
  - [3.4. **Pronunciation Control**](#34-pronunciation-control)
- [4. **Available Parameter Options**](#4-available-parameter-options)
  - [4.1. **Actor and Tags**](#41-actor-and-tags)
  - [4.2. **Emotion**](#42-emotion)
  - [4.3. **Language**](#43-language)
  - [4.4. **Speed and Loudness**](#44-speed-and-loudness)

---
# 1. Base Case and Overview
To learn how to control your actor we will in all cases start out from this most basic example where we will simply have our actor Elias Granhammar say the line "Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.". 
This is a minimal example which contains only the mandatory input parameters: A valid Actor and some text. 
```csharp
UserSegment singleSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { singleSegment });
```
Of course, you can do much more with the Thespeon Engine and `UserModelInput` contains various optional parameters with defaults implicitly added by the Thespeon backend. 
Generally a `UserModelInput` instance consists of a list of [`UserSegments`](./api/Lingotion_Thespeon_API.md#class-usersegment) with some global parameters affecting all segments, and some local parameters limited to the current segment. Each segment contains some text which upon synthesis are joined (with eventual whitespace inserted between segments) into the complete line of dialogue. Note that if a break between two segments occurs in the middle of a word, this will split the word into two. 

You will find help with basic parameters in [2. Changing Basic Parameters](#2-changing-basic-parameters), while more advanced control is found in [3. Changing Advanced Parameters](#3-changing-advanced-parameters). In section [4. Available Parameter Options](#4-available-parameter-options) you will find an account of which parameter options are available to you and what they mean. 

For each parameter below we will start out from the above basic case and expand from there, adjusting the parameter in question while leaving the others untouched. Feel free to use the links in the table of contents to navigate to the particular parameter you are interested in exploring. 

---
# 2. Changing Basic Parameters
This section contains a guide for changing basic parameters. Generally, these are the global parameters which apply to your entire `UserModelInput` instance.

## 2.1. **Actor and Tags**
The actor selection is primarily decided through the actor's name - the first parameter in the UserModelInput constructor (eg. `"Elias Granhammar"`). However, you might have noticed that on the [Lingotion Portal](https://portal.lingotion.com) there are several different versions of Elias Granhammar available. If you happen to have several of these imported and registered in your scene, only supplying the actor name will passively select one of them. Actively selecting one is done using [ActorTags](./api/Lingotion_Thespeon_API.md#class-actortags). Every version of an Actor has a number of associated tags which together with the actor name uniquely identifies it. 

For instance, you may have downloaded both the _High_ quality and _Mid_ quality versions of Elias Granhammar which you would then see in the **Thespeon Info Window**. The following example explicitly selects the _High_ quality version:
```csharp
UserSegment singleSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
ActorTags desiredTags = new ActorTags("High");
UserModelInput input = new UserModelInput("Elias Granhammar", desiredTags, new List<UserSegment>() { singleSegment });
```
## 2.2. **Text**
Changing the text in a segment is as easy as changing the string supplied to the `UserSegment` constructor. When you are using more advanced control to change parameters on a local level, these will be applied on specific parts of a text - meaning text will have to be split up into several segments. Below is an example of the base case split into several segments. Note that since no local parameters are set, this will yield the exact same output as the base case. 
```csharp
UserSegment firstSegment = new UserSegment("Greetings!");
UserSegment secondSegment = new UserSegment("It is time to start showcasing how flexible I am. You have a lot of ");
UserSegment thirdSegment = new UserSegment("creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { firstSegment, secondSegment, thirdSegment });
```

### **Pauses**
Controlling the flow of the sentence is affected by almost all the parameters but one thing you can control explicitly is pausing. You may have noticed that commas, full stops and other punctuations will yield a natural short pause in the speech. You also have the possibility to insert an explicit short pause by adding the Double Vertical Bar Unicode character ⏸ (U+23F8) in the text wherever you want a longer pause. 
```csharp
UserSegment singleSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am.⏸You have a lot of creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { singleSegment });
```
## 2.3. **Default Emotion**
So far we have only concerned ourselves with talking, but acting is much more than that. Emotional delivery is what sets good acting apart from mere speech. The simplest way of selecting emotion is to do so globally - this will set the default emotion to be applied to all segments where no local deviation is provided. This can be done by setting the input.defaultEmotion key to one of the [available options](#42-emotion) as in this example:
```csharp
UserSegment singleSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { singleSegment });
input.defaultEmotion = "Interest";
```

## 2.4. **Default Language**
Changing the spoken language works in the same way, and can be changed either globally or locally. Globally this is done by setting the _input.defaultLanguage_ parameter to a [valid instance](#43-language) of the [`Language` class](./api/Lingotion_Thespeon_API.md/#class-language). Again, this will apply to all segments where no other language has been provided.
Of course, it is a good idea to change the text to reflect the spoken language - but Thespeon is capable of reading text in any language and synthesize the acting sounding like another language. Here is an example with the same english text but spoken as if it were Swedish:
```csharp
UserSegment singleSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { singleSegment });
input.defaultLanguage = new Language { iso639_2="swe" };
```

---
# 3. Changing Advanced Parameters
In this section we will explore the more advanced parameter adjustments you can make, which will often entail splitting your dialogue into several `UserSegments`, each with its own specific parameter settings. Included here are also the Speed and Loudness adjustments, which are advanced tools applied over all segments.

## 3.1. **Emotion**
This section will handle setting emotions on a specific segment, which will result in deviations from the globally set [default emotion](#23-default-emotion). The Thespeon Engine is capable of changing emotions in the middle of sentences. Due to how the segments are joined with eventual whitespace, the effective resolution at which you can change emotions is at the word level. The below example will let the default emotion remain unspecified (leading to a backend set default), but we specify that the middle segment should specifically be said angrily.

```csharp
UserSegment firstSegment = new UserSegment("Greetings!");
UserSegment secondSegment = new UserSegment("It is time to start showcasing how flexible I am. You have a lot of ", emotion:"Anger");
UserSegment thirdSegment = new UserSegment("creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { firstSegment, secondSegment, thirdSegment });
```

## 3.2. **Language**
Similarly to emotions, language can too be changed on a segment basis down to the word level. This behaves in the exact same way as above meaning the [default language](#24-default-language) applies wherever no segment specific language is provided. Language also encompasses dialects if the Actor has support for several (See [the class documentation](./api/Lingotion_Thespeon_API.md#class-language) for full specification on what you can do). Below is an example where a segment with some Swedish text is added with accompanying language specification. 
```csharp
UserSegment originalSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
UserSegment swedishSegment = new UserSegment("⏸Jag pratar också Svenska.", language: new Language { iso639_2="swe" });
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { originalSegment, swedishSegment });
```

## 3.3. **Speed and Loudness**
The Thespeon Engine also provides a fine detailed creative control for both speed and loudness on every input line. Both are provided as `List<double>` objects of arbitrary length which will be extrapolated and/or interpolated to fit the *entire* line of dialogue on a per character basis - across all segments. Speed will affect rate of speech whereas Loudness affects volume. Both of these tools work in the same way being a list of multipliers - values of 1.0 will lead to no change, whereas 2.0 will lead to twice the speed or volume over the corresponding section. The two examples below control speed and loudness, respectively - the first of which will apply a flat 50% speed increase to the entire line and the second will apply a varying volume adjustment over time using a few keypoints.

```csharp
UserSegment originalSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { originalSegment });
input.speed = new List<double> { 1.5 };
```

```csharp
UserSegment originalSegment = new UserSegment("Greetings! It is time to start showcasing how flexible I am. You have a lot of creative control when using Thespeon.");
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { originalSegment });
input.loudness = new List<double> { 0.8, 1.2, 2, 0.2, 0.2, 1, 0.9};
```


## 3.4. **Pronunciation Control**
In some cases you might want Thespeon to pronounce something that is not necessarily a normal part of the language the actor speaks, such as fictional names from your game's fictional world or single words from other languages. Thespeon utilizes [International Phonetic Alphabet (IPA)](https://en.wikipedia.org/wiki/International_Phonetic_Alphabet) transcriptions of your text which may not produce exactly the pronunciation you want from a fictional name. By activating the `isCustomPhonemized` flag, you mark an entire segment to be interpreted as phonetic IPA script - meaning you can provide your own bypass transcription to control exact pronunciation. The following example showcases Elias Granhammar saying the name of Thor's famous hammer Mjölner the way a Swede would pronounce it.

```csharp
UserSegment firstSegment = new UserSegment("The great Norse God Thor swings his mighty hammer");
UserSegment IPASegment = new UserSegment("mjˌøːlnɛr", isCustomPhonemized: true, language: new Language { iso639_2="swe" });
UserModelInput input = new UserModelInput("Elias Granhammar", new List<UserSegment>() { firstSegment, IPASegment });
```


# 4. Available Parameter Options
Here you will find which valid options are available for each relevant parameter or where to find them for the remaining.
## 4.1. **Actor and Tags**
In the Thespeon Info Window under the **Imported Actors** Tab you will find which version of each actor you have imported. See the [Thespeon Unity Integration Guide](./get-started-unity.md#step-2-get-acquainted-with-the-thespeon-info-window) for details.


## 4.2. **Emotion**
All our actors have access to the same range of emotions. This section outlines the complete set along with a description of similar words, typical sensations, the underlying message each emotion conveys, and real-world examples to illustrate the emotional experience.

### Ecstasy
- **Similar Words:** Delighted, Giddy  
- **Typical Sensations:** Abundance of energy  
- **Message:** This is better than I imagined.  
- **Example:** Feeling happiness beyond imagination, as if life is perfect at this moment.  

### Admiration
- **Similar Words:** Connected, Proud  
- **Typical Sensations:** Glowing  
- **Message:** I want to support the person or thing.  
- **Example:** Meeting your hero and wanting to express deep appreciation.  

### Terror
- **Similar Words:** Alarmed, Petrified  
- **Typical Sensations:** Hard to breathe  
- **Message:** There is big danger.  
- **Example:** Feeling hunted and fearing for your life.  

### Amazement
- **Similar Words:** Inspired, WOWed  
- **Typical Sensations:** Heart stopping  
- **Message:** Something is totally unexpected.  
- **Example:** Discovering a lost historical artifact in an abandoned building.  

### Grief
- **Similar Words:** Heartbroken, Distraught  
- **Typical Sensations:** Hard to get up  
- **Message:** Love is lost.  
- **Example:** Losing a loved one in an accident.  

### Loathing
- **Similar Words:** Disturbed, Horrified  
- **Typical Sensations:** Bileous & vehement  
- **Message:** Fundamental values are violated.  
- **Example:** Seeing someone exploit others for personal gain.  

### Rage
- **Similar Words:** Overwhelmed, Furious  
- **Typical Sensations:** Pounding heart, seeing red  
- **Message:** I am blocked from something vital.  
- **Example:** Being falsely accused and not believed by authorities.  

### Vigilance
- **Similar Words:** Intense, Focused  
- **Typical Sensations:** Highly focused  
- **Message:** Something big is coming.  
- **Example:** Watching over your child climbing a tree, ready to catch them if they fall.  

### Joy
- **Similar Words:** Excited, Pleased  
- **Typical Sensations:** Sense of energy and possibility  
- **Message:** Life is going well.  
- **Example:** Feeling genuinely happy and optimistic in conversation.  

### Trust
- **Similar Words:** Accepting, Safe  
- **Typical Sensations:** Warm  
- **Message:** This is safe.  
- **Example:** Trusting someone to be loyal and supportive.  

### Fear
- **Similar Words:** Stressed, Scared  
- **Typical Sensations:** Agitated  
- **Message:** Something I care about is at risk.  
- **Example:** Realizing you forgot to prepare for a major presentation.  

### Surprise
- **Similar Words:** Shocked, Unexpected  
- **Typical Sensations:** Heart pounding  
- **Message:** Something new happened.  
- **Example:** Walking into a surprise party.  

### Sadness
- **Similar Words:** Bummed, Loss  
- **Typical Sensations:** Heavy  
- **Message:** Love is going away.  
- **Example:** Feeling blue and unmotivated.  

### Disgust
- **Similar Words:** Distrust, Rejecting  
- **Typical Sensations:** Bitter & unwanted  
- **Message:** Rules are violated.  
- **Example:** Seeing someone put a cockroach in their food to avoid paying.  

### Anger
- **Similar Words:** Mad, Fierce  
- **Typical Sensations:** Strong and heated  
- **Message:** Something is in the way.  
- **Example:** Finding your car blocked by someone who left their car unattended.  

### Anticipation
- **Similar Words:** Curious, Considering  
- **Typical Sensations:** Alert and exploring  
- **Message:** Change is happening.  
- **Example:** Waiting eagerly for a long-awaited promise to be fulfilled.  

### Serenity
- **Similar Words:** Calm, Peaceful  
- **Typical Sensations:** Relaxed, open-hearted  
- **Message:** Something essential or pure is happening.  
- **Example:** Enjoying peaceful time with loved ones without stress.  

### Acceptance
- **Similar Words:** Open, Welcoming  
- **Typical Sensations:** Peaceful  
- **Message:** We are in this together.  
- **Example:** Welcoming a new person into your friend group.  

### Apprehension
- **Similar Words:** Worried, Anxious  
- **Typical Sensations:** Cannot relax  
- **Message:** There could be a problem.  
- **Example:** Worrying about the outcome of an unexpected meeting.  

### Distraction
- **Similar Words:** Scattered, Uncertain  
- **Typical Sensations:** Unfocused  
- **Message:** I don’t know what to prioritize.  
- **Example:** Struggling to focus during a conversation.  

### Pensiveness
- **Similar Words:** Blue, Unhappy  
- **Typical Sensations:** Slow & disconnected  
- **Message:** Love is distant.  
- **Example:** Feeling uninterested in suggested activities.  

### Boredom
- **Similar Words:** Tired, Uninterested  
- **Typical Sensations:** Drained, low energy  
- **Message:** The potential for this situation is not being met.  
- **Example:** Finding nothing enjoyable to do.  

### Annoyance
- **Similar Words:** Frustrated, Prickly  
- **Typical Sensations:** Slightly agitated  
- **Message:** Something is unresolved.  
- **Example:** Being irritated by repetitive behavior.  

### Interest
- **Similar Words:** Open, Looking  
- **Typical Sensations:** Mild sense of curiosity  
- **Message:** Something useful might come.  
- **Example:** Becoming curious when hearing unexpected news.  

### Emotionless
- **Similar Words:** Detached, Apathetic  
- **Typical Sensations:** No sensation or feeling at all  
- **Message:** This does not affect me.  
- **Example:** Feeling nothing during a conversation about irrelevant topics.  

### Contempt
- **Similar Words:** Distaste, Scorn  
- **Typical Sensations:** Angry and sad at the same time  
- **Message:** This is beneath me.  
- **Example:** Feeling disdain toward someone’s dishonest behavior.  

### Remorse
- **Similar Words:** Guilt, Regret, Shame  
- **Typical Sensations:** Disgusted and sad at the same time  
- **Message:** I regret my actions.  
- **Example:** Wishing you could undo a hurtful action.  

### Disapproval
- **Similar Words:** Dislike, Displeasure  
- **Typical Sensations:** Sad and surprised  
- **Message:** This violates my values.  
- **Example:** Rejecting a statement that contradicts your beliefs.  

### Awe
- **Similar Words:** Astonishment, Wonder  
- **Typical Sensations:** Surprise with a hint of fear  
- **Message:** This is overwhelming.  
- **Example:** Being speechless when meeting your idol.  

### Submission
- **Similar Words:** Obedience, Compliance  
- **Typical Sensations:** Fearful but trusting  
- **Message:** I must follow this authority.  
- **Example:** Obeying a trusted figure’s orders without question.  

### Love
- **Similar Words:** Cherish, Treasure  
- **Typical Sensations:** Joy with trust  
- **Message:** I want to be with this person.  
- **Example:** Feeling deep connection and joy with someone.  

### Optimism
- **Similar Words:** Cheerfulness, Hopeful  
- **Typical Sensations:** Joyful anticipation  
- **Message:** Things will work out.  
- **Example:** Seeing the positive side of any situation.  

### Aggressiveness
- **Similar Words:** Pushy, Self-assertive  
- **Typical Sensations:** Driven by anger  
- **Message:** I must remove obstacles.  
- **Example:** Forcing your viewpoint aggressively.  

## 4.3. **Language**
In the Thespeon Info Window under the **Imported Languages** tab you will find which languages you have imported and which keys it has. See the [Thespeon Unity Integration Guide](./get-started-unity.md#step-2-get-acquainted-with-the-thespeon-info-window) for details and refer to the [Language class documentation](./api/Lingotion_Thespeon_API.md#class-language) for how to construct the object using those keys.

## 4.4. **Speed and Loudness**
Valid values Speed are any number larger than `0.1` with loudness accepting all non-negative values. Values outside the respecitve ranges will be clamped. Do note that extreme values may lead to unexpected results - notably, a low speed will lead to long synthesis latency. 