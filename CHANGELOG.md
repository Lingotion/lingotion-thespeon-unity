# CHANGELOG
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).
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

