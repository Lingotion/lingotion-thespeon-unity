# CHANGELOG
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

# [3.0.0] - 2026-10-08
This version requires updated character and language modules, please re-download your characters. Modules made for earlier package versions can no longer be imported.

### Added
* The Thespeon Info Window now lists imported modules that this package version does not support, so outdated characters and languages are easy to find and replace.
* A new Samples guide describing every sample that ships with the package.
* While the package is not yet activated, opening the Thespeon Info Window sends a small anonymous message to help us see where setup gets stuck. The Get Started guide lists exactly what is sent.
### Changed
* How text is read - normalization, how numbers are spoken and what counts as a word - now comes from the language module, so it can be improved with a module update instead of a new package version, and text is handled the same way on every Unity version and platform.
* Importing character and language modules is faster.
* Characters now use the language module version they were made for when several versions are imported.
* Text entered in the Synthesis Lab is kept when switching character.
* The Demo GUI sample is now called Advanced GUI, and all samples have been updated to run without errors.
### Fixed
* Numbers are now spoken correctly in more cases, including ordinals, very large numbers and decimals on systems set to a region that uses a decimal comma.
* Custom pronunciation segments are now kept exactly as written.
* Accented letters and text outside the Latin alphabet are now handled correctly.
* Inputs saved as JSON now load back correctly, including when no character name is given.
* Buffer length and log verbosity set for one synthesis call no longer carry over to other calls.
* Fixed broken links in the documentation.
### Removed
* The `NumberConverter` class, which had no remaining use.

# [2.1.0] - 2026-09-10
This version requires updated character and language modules, please re-download your characters to enable the new functionality.

### Added
* Segments can now carry an emotion *blend* at their start and end, letting a line move gradually between several emotions.
* Segments can now carry start/end speed and loudness values.
* The Advanced GUI sample now has a karaoke overlay showing the speaker's portrait and revealing the line in time with the audio, matching the Unreal demo.
* The Thespeon Info Window now shows the module version for every imported character and language module, in both the Overview and the Synthesis Lab.
### Changed
* Speed and loudness are supported again. Input-wide `AnimationCurve`s are sampled at each segment boundary, and per-segment values are used where no curve is supplied.
* Emotion, speed and loudness boundary values are treated as keypoints on a single curve over the whole line, so values stay continuous across segment boundaries and across segments split by number conversion.
### Fixed
* Inference should no longer crash when the input contains a word longer than 150 characters
* A character whose pinned language module is not imported now falls back to any imported module serving the same language, warning that pronunciation may differ, instead of failing synthesis with "Language was never imported".
* Fixed a missing dependency in the Protobuf library on Unity versions earlier than 6000.3.

# [2.0.1] - 2026-06-09
### Added
* Verified Solution Attribution support for Unity Asset Store integration.
### Changed
* Streamlined the user registration and license activation flow.
* Refreshed the Thespeon Info Window for a cleaner, more reliable editor experience.
* Improved diagnostics to help us identify and resolve issues faster.

# [2.0.0] - 2026-03-06
### Added
* Characters can now be loaded onto and unloaded from several backends independently.
* New acting models with increased fidelity and performance. New _.lingotion_ files will need to be downloaded and imported to replace existing characters and languages.
### Changed
* `ThespeonEngine` has been renamed to `ThespeonComponent`.
* `ThespeonComponent` bindable actions have been reworked and now give adequate information to identify their source.
* Logs are now more descriptive and helpful in troubleshooting.
* Moved config defaults to a ScriptableObject for easier access.
* API Docs have been streamlined and made easier to navigate.
### Fixed
* Release builds will no longer freeze when synthesizing with unknown words.
* Manual asset regeneration now properly updates corrupted assets.
* Ordinals in text are now properly pronounced.
* CPU backend no longer causes severe stutters on high-end devices.
* GPU backend no longer crashes on certain Unity versions.
### Removed
* Speed and loudness control has been temporarily removed. Usage will be ignored.
# [1.3.2] - 2026-02-02
### Changed
* Updated package dependency to Sentis version 2.5.0.
## Fixed
* Error messages now correctly shows the stack trace.
# [1.3.1] - 2025-10-22
## Fixed
* GUI Sample Runtime assembly now contains the correct references.
# [1.3.0] - 2025-10-03
## Added
* License verification step to activate package use.
## Fixed
* Users are now reminded to turn off Burst Native Debug Mode Compilation when running Thespeon as it heavily affects model performance when run on CPU.
# [1.2.0] - 2025-09-22
## Added
* New `OnSynthesisFailed` callback for ThespeonEngine.
## Fixed
* Synthesis of multiple simultaneous engines no longer results in resource collisions.
* Thespeon now properly waits until the end of the frame during synthesis.
* Inference failure in the ThespeonInfoWindow no longer results in a non-responsive UI.
# [1.1.1] - 2025-09-17
## Added
* New section to the Actor Control Guide, detailing how speed and loudness can be set for individual words or regions of an input.
## Fixed
* Fixed a crash from disposing GPU tensors after the GPU is uninitialized.
# [1.1.0] - 2025-08-29
## Added
* Precise mid-sentence callbacks are now possible through the AudioSampleRequest control character, allowing for events to be synchronized to the playback of a specific letter in the input text.
* Audio Callback sample showcasing how the mid-sentence callbacks can be used.
## Changed
* Rewrote the Thespeon Tools Manual and DemoGUI Sample Guide documentation.
* Removed old entries from the Known Issues documentation.
## Fixed
* Fixed pack import crashing when the Unity project is on a separate disk from the pack file.
* Fixed API documentation not properly showing generic methods and classes.
* Fixed dialect selection not being possible without selecting language first.
* Selecting a dialect in a specific segment should now only affect that segment.
* Fixed audio stutters in the beginning of synthesized audio.
# [1.0.0] - 2025-08-19
## Added
* First major release of Lingotion Thespeon.
