# Get Started - Unity

## Table of Contents
- [**Overview**](#overview)
- [**Install the Thespeon Unity Package**](#install-the-thespeon-unity-package)
  - [Importing Thespeon from git](#importing-thespeon-from-git)
  - [Import the Minimal Character sample](#import-the-minimal-character-sample)
- [**Get acquainted with the Thespeon Info Window**](#get-acquainted-with-the-thespeon-info-window)
- [**Run the Minimal Character sample**](#run-the-minimal-character-sample)
- [**Next Steps**](#next-steps)

---

## Overview
This document details a step-by-step guide on how to install Lingotion Thespeon in your Unity project, as well as how to import the packs downloaded from the Lingotion Developer Portal.
This process has three main steps:
1. Install the Thespeon Package
2. Import the downloaded _.lingotion_ files
3. Run the _Minimal Character_ sample
> [!TIP]
> If you have not downloaded any _.lingotion_ files, please follow the [Get Started - Webportal](./get-started-webportal.md) guide before proceeding.
> 

--- 
## Install the Thespeon Unity Package
### Importing Thespeon from git
![Clone repo screenshot](./data/clone-repo.png?raw=true "Clone repo screenshot")

1. Open your Unity project.  
2. Go to **Unity > Window > Package Manager** from the top menu.  
3. Click the **+** (Add) button → **Install package from Git URL...**  
4. Paste the repository web URL:

   ```
   https://github.com/Lingotion/lingotion-thespeon-unity.git
   ```
  or if you use SSH:

   ```
   git@github.com:Lingotion/lingotion-thespeon-unity.git
   ```

5. Click **Install**.

Unity will now install the Thespeon package and its dependencies to your Unity project.

> [!TIP]
> You may also clone the package yourself and add it from disk if you wish to have a local copy.
> 
### Import the Minimal Character sample
The Minimal Character sample contains a bare minimum example scene datailing how to use the package -- this will be the basis for this guide.

![Import sample screenshot](./data/import-sample.png?raw=true "Import sample screenshot")

1. Find **Lingotion Thespeon** in the package list and open the **Samples** tab.
2. Click **Import** next to the Minimal Character sample.  Unity will create a copy of the sample in your project assets:
```
 Samples/Lingotion Thespeon/<version>/Minimal Character
 ```
3. Navigate to the sample directory and open the **Minimal Character.unity** scene.

---
## Get acquainted with the Thespeon Info Window
Now that the package is installed, we can import the downloaded actor and language packs from the Lingotion developer portal. 

Thespeon has its own information window that displays an overview of installed actors and languages, tools for importing and deleting packs from the project, as well as a tab for audio synthesis laboration in Edit Mode.

1. To find the _Thespeon Info Window_, go to **Window > Lingotion > Thespeon Info** from the top menu. 

![Thespeon info empty screenshot](./data/thespeon-info-empty.png?raw=true "Thespeon info empty screenshot")

2. Press the **Import Pack** button and select your downloaded actor pack from the webportal. The imported actor(s) will show up under the **Imported Actor Packs** list.

![import-actor screenshot](./data/import-actor.png?raw=true "import-actor screenshot")

   Note the warning - this means that we need to import a corresponding language pack as well.

3.  Now, press the "**Import Pack**" button again and select your downloaded language pack(s). The imported languages can be seen in the **Imported Language Packs** list.

![import-language screenshot](./data/import-language.png?raw=true "import-language screenshot")

If all languages that the chosen actor pack supports are imported, you should see the warning disappear. Multilingual actors do not strictly need all their language packs to run and can be used as long as it has at least one, but its use will then be limited to that language only.
   
> [!TIP] 
> To remove an imported Actor Pack or Language Pack, select the pack in its list and press the **Delete Pack** button. 
> 

---

## Run the Minimal Character sample
The _MinimalCharacter.cs_ script shows how easy it is to run Thespeon by only providing a single dialogue line -- this is the only thing that is explicitly required for Thespeon to synthesize audio. In the absence of parameters, Thespeon will do its best to fill in the blanks with default values. In the current case, the first available actor is selected for you along with a fallback emotion and language.

> [!IMPORTANT]
> The Unity InferenceEngine GPUCompute backend does not work properly on Windows from Unity version 6000.0.53f1 and forward due to a buffer allocation issue in the DirectX pipeline. This issue has been reported to the Unity team, and we will update the package as soon as a fix is released.
> 
> Until then, please use a Unity Editor version lower than 6000.0.53f1 for full functionality. 
> A workaround is to not use the CPUCompute backend when synthesizing.

Now that we have imported the actor and language packs, we can start interfacing with the package. Enter play mode and press the **Space**, **Enter** or **S** key to initiate a synthesis and you should hear the imported actor speak.

Feel free to check out the other package samples for more examples on how to use Thespeon!

> [!TIP]
> A quick way to interactively experiment with different lines in the Unity Editor is to use the **Audio Test Lab** under the **Actors** tab in the **Thespeon Info Window**.
---
## Next Steps
Now that you have successfully produced audio with your chosen actor, you can follow the [Actor Control Guide](./actor-control.md) to learn how to direct how the actor should speak their lines.

See the [Configuration and Performance Tuning Manual](./thespeon-configuration.md) for details on how to control the performance and memory usage of Thespeon.


