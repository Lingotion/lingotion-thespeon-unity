# **Thespeon Unity Integration Guide**

## **Table of Contents**
- [**Thespeon Unity Integration Guide**](#thespeon-unity-integration-guide)
  - [**Table of Contents**](#table-of-contents)
  - [**Step 1: Install the Thespeon Package from Git**](#step-1-install-the-thespeon-package-from-git)
  - [**Step 2: Get Acquainted with the Thespeon Info Window**](#step-2-get-acquainted-with-the-thespeon-info-window)
  - [**Step 3: Run the Simple Narrator sample**](#step-3-run-the-simple-narrator-sample)
  - [**Next Steps**](#next-steps)
- [**Tips**](#tips)


---

## **Step 1: Install the Thespeon Package from Git**
### Import the package
![Clone repo screenshot](./data/clone-repo.png?raw=true "Clone repo screenshot")

1. Open **Unity**.  
2. Open an existing project or add a new one.
3. Go to **Unity > Window > Package Manager** from the top menu.  
4. Click the **+** (Add) button → **Install package from Git URL...**  
5. Paste the repository web URL:
   ```
   https://github.com/Lingotion/lingotion-thespeon-unity.git
   ```
  or if you use SSH:

   ```
   git@github.com:Lingotion/lingotion-thespeon-unity.git
   ```

6. Click **Install**.  
### Import the Simple Narrator sample
The Simple Narrator sample contains a bare minimum example scene to use the package. This guide will be based on the Simple Narrator sample supplied in the package.

![Import sample screenshot](./data/import-sample.png?raw=true "Import sample screenshot")

1. Find **Lingotion Thespeon** in the package list and open the **Samples** tab.
2. Click **"Import"** next to the Simple Narrator sample.  Unity will create a copy of the sample in:
```
 Assets/Samples/Lingotion Thespeon/<version>/Simple Narrator
 ```
4. Navigate to the sample directory and open the **Simple Narrator Example.unity** scene.

---
## **Step 2: Get Acquainted with the Thespeon Info Window**
Now that the package is installed, we can import the downloaded actor and language packs from the Lingotion developer portal. 

>[!TIP]
>If you have not already downloaded your packs, make sure to follow the instructions from [Get Started with the Web Portal](./get-started-webportal.md).  

Thespeon has its own information window that displays an overview of installed actors and languages, as well as tools for importing and deleting packs from the project.

1. To find the **Thespeon Info Window**, go to **Unity > Window > Lingotion > Lingotion Thespeon Info** from the top menu. 

![Thespeon info empty screenshot](./data/thespeon-info-empty.png?raw=true "Thespeon info empty screenshot")

2. Click on **"Import Actor Pack"** and select your downloaded actor pack from the webportal. The imported actor(s) will show up under the **Imported Actors** tab.

![import-actor screenshot](./data/import-actor.png?raw=true "import-actor screenshot")

   Note the warning "*Missing language packs for: eng*" - this means that we need to import a corresponding language pack as well.

3.  Now, click on the "**Import Language Pack**" button and select your downloaded language pack(s). The imported languages can be seen in the **Imported Languages** tab.

![import-language screenshot](./data/import-language.png?raw=true "import-language screenshot")

If all languages that the chosen actor pack supports are imported, the warning will change into a notification confirming that all is well.
   
> [!TIP] 
> To remove an imported Actor Pack or Language Pack, use the corresponding buttons under the **Delete Tools** section. 
> 
> You can also do this manually by navigating to your project's **StreamingAssets** directory, where you will find the **LingotionRuntimeFiles > ActorModules** and **LingotionRuntimeFiles > LanguagePacks** folders, respectively. Each subfolder in these directories corresponds to an imported pack.

---

## **Step 3: Run the Simple Narrator sample**
Now that we have imported the actor and language packs, we can start interfacing with the package. The following section will detail how to get started with the Simple Narrator example.
## Running the sample
![Simple Narrator screenshot](./data/simple-narrator.png?raw=true "Simple Narrator screenshot")
The Simple Narrator example contains a mostly empty scene with a pre-configured narrator GameObject - to generate the real-time audio we simply need to tell the package which actor to use. 
1. Go back to the Thespeon Info window and click the **Imported Actors** under the **Imported Pack Overview**. There will be a list of your imported actors - click on their name to expand the information about the installed actor pack:
![Thespeon Info screenshot](./data/thespeon-info.png?raw=true "Thespeon info screenshot")
2. Note the **actor name** and **quality tag** for the actor you want to use. In this example, we have the actor *denel.honeyball* with the *high* quality tag. 
3. Open the SimpleNarrator.cs script in the sample folder and find the following lines in the **Update()** function:
```csharp
input = new UserModelInput("InsertActorNameHere", desiredTags, new List<UserSegment>() { testSegment });
```

```csharp
var desiredTags = new ActorTags("InsertQualityLevelHere");
``` 
4. Replace the sample strings with the values from the Actor Info tab as follows:
```csharp
input = new UserModelInput("denel.honeyball", desiredTags, new List<UserSegment>() { testSegment });
```

```csharp
var desiredTags = new ActorTags("high");
``` 
5. Save the script and go into **Play mode**. Press space, and your selected actor should speak a sample line!
> [!TIP]
> If the space button does nothing, try to change the KeyCode in the _Update()_ function if-statement.

## **Next Steps**
Now that you have successfully gotten your Actor to talk, you might be interested in tailoring the input to your project. Visit [How To Control Thespeon](./how-to-control-thespeon.md) for help in how to construct more advanced instances of the ThespeonEngine input class.

---
## **Tips**
1. See [Package Config](./PackageConfig.md) for details on how you can control parameters in the Thespeon Engine to optimize performance. 
2. For representative quality of sound on builds for low end mobile devices, consider turning off all render pipeline post processing to free up the resources required to run the Thespeon Engine smoothly. An example of how to do so is to create a Render Pipeline Asset and then go to Project Settings -> Quality -> Render Pipeline Asset and select the desired asset. One such asset is available in the DemoGUI sample under the sample's Settings folder (Mobile_RPAsset.asset). 
