This markdown will walk you through how to get started with the Graphic User Interface (GUI) sample scene of the Lingotion Thespeon Unity package. This GUI is meant as a playground for new Thespeon users to test the Thespeon Engine in an intuitive manner and familiarize themselves with the Thespeon Engine workflow. The purpose of the Sample scene is to provide a GUI-based way of editing the [UserModelInput](./api/Lingotion_Thespeon_API.md#class-usermodelinput) class to let you explore the features of Thespeon before starting to code.
## Table of Contents
- [Introduction](#introduction)
- [Importing the Demo GUI Sample](#importing-the-demo-gui-sample)
- [Editable Fields](#editable-fields)
- [Loading Predefined Input](#loading-predefined-input)

---
## Introduction
  ![Alt text](./data/GUI.png?raw=true "GUI")
  The screen capture above shows the GUI in its entirety. It features the following components:
  - An editable [Text Box](#text-box) where you may enter any text you like.
  - An [Emotion Wheel](#emotion-wheel) of 33 clickable emotions.
  - [Speed and Loudness](#speed-and-loudness) adjustment curves editable through the Unity Editor Inpector window.
  - A [Frame Time Budget Slider](#frame-time-budget-slider) for adjusting the frame budget given to the model.
  - [Buttons](#buttons) to allow toggling of IPA, insertion of the Pause character and Synthesis of audio.
  - [Dropdown Menus](#dropdown-menus) allowing selection of available actors, quality level, language and whether to run the model on GPU or CPU.
  - A [JSON Visualizer](#json-visualizer) which showcases the current input and allows for [Loading Predefined Input](#loading-predefined-input) and copy to clipboard.
  - A frame counter and a constantly revolving cube with the Lingotion logo to indicate any performance impact.
  

## Importing the Demo GUI Sample

To start using the package you first have to have the Lingotion Thespeon package installed. You may follow [this guide](./get-started-unity.md) to do so. Once you have the package installed, have imported the DemoGUI sample and have at least one Actor Pack and its associated Language Pack(s) to work with you may open the scene from:

   ```
   Project > Assets > Samples > Lingotion Thespeon > /<version>/ > DemoGUI > Scenes > DemoGUI.unity
   ```

Once you have imported the GUI scene you can get started with properly exploring your Thespeon Actor. The GUI is merely a tool for creating `UserModelInput` instances in play time and testing the output they lead to. To implement its use in your game code you may follow [this guide](./how-to-control-thespeon.md) supplemented with the [API documentation for the `UserModelInput`](./api/Lingotion_Thespeon_API.md#class-usermodelinput) class.

## Editable Fields
This section lists and explains the functionality of each component of the DemoGUI and how to properly use them.

##### Text Box
The text box lets you easily edit the dialogue the chosen actor should read. You will also note that it will always be underlined by some color. This color indicates what [emotion](#emotion-wheel) a certain segment of text is annotated with, which in turn will determine how your Thespeon Engine will express your dialogue. A pipe character, |, has special meaning here, representing a segment break. The GUI will keep track of segments for you and will automatically merge any adjacent segments that share the same parameters (ignoring its text content). 

Do note that upon synthesis, the Thespeon Engine backend might reinterpret your input slightly. For instance, if you attempt to pass characters that the actor does not recognize (such as characters of a different language) they will be filtered out. This will be shown using warnings in the console.

##### Emotion Wheel
To annotate a segment of text with an emotion you first have to select some text in the text box with your cursor. The Emotion Wheel has 33 clickable regions each of which, when clicked, will annotate the latest selected text region with its emotion and underline the corresponding segment with its color. The white free-floating emotions will have a dual color of its adjacent emotion categories (meaning optimism will be yellow and orange). The emotion of the first segment will always be set to the default emotion of the input.

##### Speed and Loudness
Both Speed and Loudness are adjustable, but only through the use of the Unity Inspector window. In the scene there is a Game object called UI Manager. Its inspector window has two custom AnimationCurve windows, one for speed and one for loudness. Opening these will allow you to fine tune speed and volume over your dialogue line as seen in the bottom of the GUI. These windows are usable both in Edit Mode and Game Mode.

##### Frame Time Budget Slider
Every device and end user has unique demands on CPU/GPU availability. As such the GUI also features a slider for controlling the tradeoff between low latency and high frame rate. Do note that since the synthesis is an audio stream, which in this sample scene is played directly on generation, a harsh frame budget restriction will lead to slower audio generation resulting in a choppy playback. 

##### Buttons
The two buttons Mark as IPA and Insert Pause do precisely that; the first annotates the segment in which your cursor is currently as a custom phonemized text. More on what that means can be found [here](./how-to-control-thespeon.md#34-pronunciation-control). It does therefore not create a new segment through a text selection but rather toggles the segment in which the cursor currently resides as custom phonemized or not. The Insert Pause button also acts on where the cursor is currently and simply inserts the pause character at the cursor location. The pause character will be interpreted as a short pause by the Thespeon Actor on synthesis. 

##### Dropdown Menus
The 3 Dropdown Menus **Select Actor**, **Select Quality** and **Select Language** are dynamically populated such that the first lists all your available actors, and the second and third is repopulated with the valid qualities of the selected actor. Both the **Select Actor** and **Select Quality** act globally, changing the actor and quality level of the entire input text. The **Select Language** dropdown works similarly to the emotion selection, where it will annotate the latest selection of text with the selected language. 

Due to how the Event Triggers on these objects work, they will only cause a change in the input on ***change*** meaning if you have annotated one part of your text with a language and then select another segment and attempt to annotate that with the same language (without the dropdown having changed value inbetween) the annotation will not happen. Instead you must reset the dropdown by either selecting another language first and then back to your desired or by resetting it to the default selection "Select Language". 

##### JSON Visualizer
The JSON Visualizer shows you exactly what input you have generated and can be a useful tool for understanding how you can later build your input objects in your own projects. The exact structure of the visualizer can be put in a json file, which can be deserialized into a UserModelInput class during runtime if you want predefined input in your project. The Visualizer has a Copy to Clipboard button to let you easily construct such predefined input files.


## Loading Predefined Input
The [JSON Visualizer](#json-visualizer) also provides the option to load predefined input json files into the GUI. Do note that upon load the actor of the sample will be overwritten to your currently chosen actor and quality if there is one, otherwise it will use the first one it finds. No other fields will be changed, however, which means the loaded input might be overwritten with an actor which does not speak the language of the input, which is unchanged. Attempting to synthesize might in this case lead to unexpected output.

The sample already comes with a few predefined input samples, but feel free to extend this list of samples in any way you see fit. The files are fetched from your Assets/StreamingAssets/LingotionRuntimeFiles/ModelInputSamples folder, which is created when you exit Edit Mode. You may add files here if you only want them temporarily, but they will be overwritten on your next exit of Edit Mode. If you want to make persistent changes to this directory you must make your changes in the package directory Packages/Lingotion Thespeon/ModelInputSamples which will then automatically be copied to StreamingAssets.
