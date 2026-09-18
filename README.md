# lingotion-thespeon-unity
<div align="center">

![](./Documentation~/data/lingotion_icon_ios_01_small.png)
  
</div>

<a target="_blank" href="https://discord.gg/9f2HFyu5gF"><img src="https://dcbadge.limes.pink/api/server/https://discord.gg/9f2HFyu5gF" alt="Join our Discord server" /></a>


**Lingotion Thespeon** is an on-device AI engine designed to generate real-time character acting and voiceovers.
The package runs entirely offline on the player’s device, eliminating cloud costs and network dependencies.

This is version 2.1.0 of Lingotion Thespeon, and we appreciate any and all feedback on the package and its use. [Get in touch with the Lingotion developers and Thespeon users on our discord](https://discord.gg/9f2HFyu5gF)!

[Please report any encountered issues to the Issues page](https://github.com/Lingotion/unity-package/issues) or in the Support section of the [Lingotion Discord](https://discord.gg/9f2HFyu5gF).

---

# Requirements

[![Unity 6+](https://img.shields.io/badge/Unity-6000.0%2B-black.svg?style=flat&logo=unity)](https://unity.com/releases/editor/archive)
[![.NET Standard 2.1](https://img.shields.io/badge/.NET_Standard-2.1-blueviolet.svg?style=flat)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![Sentis](https://img.shields.io/badge/Unity_Sentis-2.5.0%2B-blue.svg?style=flat&logo=unity)](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html)



# Features

* Fully on-device, no usage cost, no internet connection required
* Real-time generation both on GPU and CPU
* Ethical and legally safe models, voice actors are compensated
* Syncing in-game events with generated audio
* Support for 33 emotions, with more on the way
* Mixing and blending emotions over time
* Tunable speed and loudness parameters
* Custom pronunciation support with IPA notation
* Number and ordinal pronunciation
* PC, Mac and mobile platforms supported, with more coming soon
* In-editor generation for quick prototyping
* Tunable configuration for different performance contexts

# Getting started

The process of getting started with Lingotion Thespeon consist of four main parts:
- Sign up in the Lingotion Developer Portal
- Download _.lingotion_ file(s) related to your chosen character(s) 
- Import the Thespeon package to your Unity project
- Import _.lingotion_ file(s) into your Unity project

> [!TIP]
> If you are a new user, you can install the Thespeon package first and let the **Thespeon Info Window** guide you through account creation. It opens the signup page for you and, once you're set up, automatically fills in your license key and downloads a couple of starter models. See the [Create an account](./Documentation~/get-started-unity.md#get-acquainted-with-the-thespeon-info-window) branch in the Unity guide.

## **Developer Portal Setup**
The _.lingotion_ file(s) are downloaded from the Lingotion developer portal. To pick and download your own character(s) you need an account on the portal.
The following guide will step you through the process of creating an account at the Lingotion developer portal:
[Get Started - Webportal](https://github.com/Lingotion/.github/blob/main/profile/portal-docs/get-started-webportal.md)

## **Unity Package Setup**  
Lingotion Thespeon is a Unity package, and easily integrated into a Unity Project. The following guide will step you through the process of installing the package and importing the _.lingotion_ file(s) into your Unity Project:  
[Get Started - Unity](./Documentation~/get-started-unity.md)

---

# Changelog
See [CHANGELOG.md](https://github.com/Lingotion/lingotion-thespeon-unity/blob/main/CHANGELOG.md) for changes.


# Known Issues
See [known-issues.md](./Documentation~/known-issues.md) for a list of known issues.


# License
![License](https://img.shields.io/badge/license-Custom-blue.svg)

This project is licensed according to the Terms of Service found at [lingotion.com/terms-of-service/](https://lingotion.com/terms-of-service/).

# Uninstalling Lingotion Thespeon 
Simply uninstalling the package using the Package Manager will not remove all Thespeon related files. To completely remove Thespeon from your project you will also have to remove the following: 

```
Folder: Assets > LingotionThespeon
Folder: StreamingAssets > LingotionRuntimeFiles
File: ProjectSettings > Lingotion.Thespeon.license
```