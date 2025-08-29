# **DemoGUI Sample Guide**
This markdown will walk you through how to get started with the Graphic User Interface (GUI) sample scene of the Lingotion Thespeon Unity package. This GUI is meant as a playground for new Thespeon users to test the Thespeon Engine in an intuitive manner and familiarize themselves with the Thespeon Engine workflow. The purpose of the Sample scene is to provide a GUI-based way of editing the [ThespeonInput](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoninput) class to let you explore the features of Thespeon before starting to code.
## Table of Contents
  - [Introduction](#introduction)
  - [Importing the Demo GUI Sample](#importing-the-demo-gui-sample)
  - [Build a performance instruction](#build-a-performance-instruction)
  - [Verify and Synthesize](#verify-and-synthesize)

---
## Introduction
  ![Alt text](./data/GUI.png?raw=true "GUI")
  The screen capture above shows the GUI in its entirety. It features the following components:
  - An editable *Text Box* where you may enter any text you like.
  - *Speed* and *Loudness* adjustment curves editable through the Unity Editor Inpector window.
  - A *Frame Time Budget Slider* for adjusting the frame budget given to the model.
  - *Buttons* to allow toggling of IPA, insertion of the Pause character and Synthesis of audio.
  - *Dropdown Menus* allowing selection of available actors and sizes, language and whether to run the model on GPU or CPU.
  - A *JSON Visualizer* which showcases the current input and allows for *Loading Predefined Input* and copy to clipboard.
  - A frame counter and a constantly revolving cube with the Lingotion logo to indicate any performance impact.
  

## Importing the Demo GUI Sample

To start using the package you first have to have the Lingotion Thespeon package installed. You may follow [this guide](./get-started-unity.md) to do so. Once you have the package installed, have imported the DemoGUI sample and have at least one Actor Pack and its associated Language Pack(s) to work with you may open the scene from:

   ```
   Project > Assets > Samples > Lingotion Thespeon > /<version>/ > DemoGUI > Scenes > DemoGUI.unity
   ```

Once you have imported the GUI scene you can get started with properly exploring your Thespeon Actor. The GUI is merely a tool for creating `ThespeonInput` instances in Play Mode and testing the output they lead to. To do so in code you may follow [this guide](./actor-control.md) supplemented with the API documentation for [`ThespeonInput`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoninput) and [`ThespeonInputSegment`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#class-thespeoninputsegment) classes.



## Build a performance instruction
Here follows a step-by-step guide on how to instruct Thespeon to act a given line in a particular manner using all components in the GUI. Note that most steps are optional.

1. Write text in the Text Box
> [!TIP]
> Text will always be underlined by some color. This color indicates what emotion a certain segment of text is annotated with, which in turn will determine how your Thespeon Engine will express your dialogue. A pipe character, |, has special meaning here, representing a segment break. The GUI will keep track of segments for you and will automatically merge any adjacent segments that share the same parameters (ignoring its text content). 

> [!NOTE]
> Upon synthesis, the Thespeon Engine backend might reinterpret your input slightly. For instance, if you attempt to pass characters that the actor does not recognize (such as characters of a different language) they will be filtered out. The [`LingotionLogger`](./api/Public%20API/Lingotion_Thespeon_Core.md#class-lingotionlogger) will log these as warnings. Set the [`InferenceConfigOverride` verbosity](./thespeon-configuration.md#verbosity-verbositylevel) to see when this happens.

2. Select your desired actor using the *Select Actor* dropdown
3. Select your desired size using the *Select Size* dropdown.

4. Select a segment of text you wish to anotate - a part or all of it.
5. Use the *Select Language* dropdown to select a language for the corresponding text.

> [!WARNING]
> Due to how the Event Triggers on these objects work, they will only cause a change in the input on ***change*** meaning if you have annotated one part of your text with a language and then select another segment and attempt to annotate that with the same language (without the dropdown having changed value inbetween) the annotation will not happen. Instead you must reset the dropdown by either selecting another language first and then back to your desired or by resetting it to the default selection 'Select Language'.

6. Select some text again, or keep the old selection.
7. Click an emotion in the Emotion Wheel - you should see a change in text underline color under the corresponding text.
8. Optionally repeat to annotate a different bit of text.

9. Click the Game Object *UI Manager* in the Hierarchy window and find the AnimationCurve windows in the Inspector Window.
10. Play around with changing the Speed and Loudness curves and see how the GUI mirrors your changes.

11. Place the cursor at desired location in the text and press the *Insert Pause* button.

12. Add some IPA text (eg. ˈlɪŋɡəʊʃᵊn ˈθɛspˌɪɑːn) to the text box and select it.
13. With the IPA text selected - click the *Mark as IPA* button.

> [!TIP]
> For more details on how to use IPA with Thespeon, see the [Actor Control Guide](./actor-control.md#controlling-pronunciation). 


## Verify and Synthesize

1. Verify the instructions are what you intended by studying the *JSON Visualizer*.

2. Select your desired backend using the *Backend Selector* dropdown. Available options are GPU and CPU. 

3. Adjust the *Frame Time Budget Slider* to a suitable level.

Every device and end user has unique demands on CPU/GPU availability. The slider controls the tradeoff between low latency and high frame rate. 

> [!CAUTION]
> Sliding far to the left and setting the value very low will lead to slower audio generation and may result in stuttering on synthesis.

4. Press the *Synthesize* Button and listen to the result.

> [!TIP]
> You may save a finished performance instruction in a json format to later be serialized directly into a `ThespeonInput` object. Use the *Copy To Clipboard* button and paste into your empty json file. Use [`ThespeonInput.ParseFromJson`](./api/Public%20API/Lingotion_Thespeon_Inputs.md#thespeoninput-parsefromjsonstring-jsonpath-inferenceconfig-configoverride--null) to read such a file.