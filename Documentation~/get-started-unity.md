# **Thespeon Unity Integration Guide**

## **Table of Contents**
- [**Thespeon Unity Integration Guide**](#thespeon-unity-integration-guide)
  - [**Table of Contents**](#table-of-contents)
  - [**Step 1: Install the Thespeon Package from Git**](#step-1-install-the-thespeon-package-from-git)
  - [**Step 2 (optional): Add Sample Scenes**](#step-2-optional-add-sample-scenes)
  - [**Step 3: Get Acquainted with the Thespeon Info Window**](#step-3-get-acquainted-with-the-thespeon-info-window)
  - [**Step 4: Synth Request**](#step-4-synth-request)
    - [**Unity NPC Object**](#unity-npc-object)
  - [**Tips**](#tips)


---

## **Step 1: Install the Thespeon Package from Git**

1. Open **Unity**.  
2. Open an existing project or add a new one.
1. Go to **Unity > Window > Package Manager** from the top menu.  
2. Click the **+** (Add) button → **Install package from Git URL...**  
3. Paste the repository web URL:  

   ```
   https://github.com/Lingotion/lingotion-thespeon-unity.git
   ```
or if you use SSH:

   ```
   git@github.com:Lingotion/lingotion-thespeon-unity.git
   ```

4. Click **Install**.  

---

## **Step 2 (optional): Add Sample Scenes**

1. In **Package Manager**, select the installed package.  
2. Expand the **Samples** section (if available).  
3. Click **"Import"** next to the Demo GUI sample scene and the Simple Narrator sample scene.  

   - Unity will copy the sample files into:  

     ```
     Assets/Samples/Lingotion Thespeon/<version>/
     ```


Clicking Import will create a local instance of the Sample directory in your Assets directory and be automatically opened. Navigate to the Scenes directory in the sample to open the sample scene. The two samples have different uses with [DemoGUI](./using-the-demogui-sample.md) serving as an experimental playground and SimpleNarrator serving as a simple example of how to use the package in your own scene. 

---

## **Step 3: Get Acquainted with the Thespeon Info Window**

1. Go to **Unity > Window > Lingotion > Lingotion Thespeon Info** from the top menu.  
2. If you have not already downloaded your packs, make sure to download them via the portal. Follow instructions from [Get Started with Web Portal](./get-started-webportal.md).  
3. Click on **"Import Actor Pack"** or **"Import Language Pack"** to import your downloaded packs into your project.  
   - ![Editor Screenshot](./data/editor1.png?raw=true "Editor Screenshot")  

4. Once you have imported your packs, you can view an overview of imported Packs in **"Imported Pack Overview"**. You will find information about your imported Actor and Language Packs under the **"Imported Actors"** and **"Imported Languages"** tabs respectively.
   - Example showing imported Actor Packs for `"denel.honeyball"` and `"EliasGranhammar"`, and an English language pack (while missing the Swedish language pack):  
   - ![Editor Screenshot](./data/editor2.png?raw=true "Editor Screenshot")  

5. Always check if you have imported the necessary language packs for the synth request.  
   - Above example after importing the Swedish language pack:  
   - ![Editor Screenshot](./data/editor3.png?raw=true "Editor Screenshot")  

6. To remove an imported Actor or Language Pack, navigate to your project **StreamingAssets** directory where you will find the **LingotionRuntimeFiles > ActorModules** and **LingotionRuntimeFiles > LanguagePacks** folders. Each of these contains all your imported Actor and Language Packs respectively. When in the correct directory, simply delete the folder of the Pack you wish to remove.

---

## **Step 4: Synth Request**



To start using the package, you can either use the [DemoGUI sample scene](./using-the-demogui-sample.md) or follow the SimpleNarrator example. An explanation of how to replicate this behavior follows.


---
### **Unity NPC Object**

The Lingotion Thespeon is mainly interacted with using a component called the Thespeon Engine, from which you can schedule audio generation from an input text of your choice.  You may find the ready-to-go example by opening the imported SimpleNarrator sample:

```
Project > Assets > Samples > LingotionThespeon > /<version>/ > Simple Narrator > Simple Narrator Example.unity
```


---
## **Tips**
1. See [User Model Input](./UserModelInput.md) for alternative ways of creating the input to the ThespeonEngine, but the above workflow works regardless of how you create your input.
2. In the [Lingotion_Thespeon API documentation](./api/) you will find more information on how to make more advanced calls using a higher level of creative control.
3. See [Package Config](./PackageConfig.md) for details on how you can control parameters in the Thespeon Engine to optimize performance. 
4. For representative quality of sound on builds for low end mobile devices, consider turning off all render pipeline post processing to free up the resources required to run the Thespeon Engine smoothly. An example of how to do so is to create a Render Pipeline Asset and then go to Project Settings -> Quality -> Render Pipeline Asset and select the desired asset. One such asset is available in the DemoGUI sample under the sample's Settings folder (Mobile_RPAsset.asset). 

