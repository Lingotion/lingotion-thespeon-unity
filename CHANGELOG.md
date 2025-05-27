# CHANGELOG
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).
# [0.3.1] - 2025-05-27
## Fixed
* Fixed an issue with contractions that occurred during text preprocessing.
# [0.3.0] - 2025-05-15
## Added
* Added a new guide in the documentation for controlling actor speech.
* Added limit for extremely short inputs.
* Added new input samples to the GUI Sample.
## Changed
* Documentation on how to get started in Unity is now more detailed.
* API documentation is now more comprehensive.
## Fixed
* Fixed a click occurring at the end of audio output.
* Fixed a missing folders warning on package install.
* Very short inputs will now output audio correctly.
# [0.2.2] - 2025-05-02
## Added
* Added delete buttons for imported packs.
## Changed
* It is now possible to queue synthesize tasks - if a synthetization is underway, any subsequent calls to Synthesize will wait until it is done.
* User will now be notified properly if an incompatible pack is imported.
## Fixed
* Fixed numerous issues related to audio quality.
# [0.2.1] - 2025-04-25
## Added
* The DemoGUI sample scene now offers the ability to copy the generated JSON structure to your system clipboard to paste into your own input_sample.json files.
## Changed
* The Thespeon Info Window now has a clearer layout with a scrollbar to more easily view imported Actor and Language Packs.
* The Documentation has been updated to be easier to follow with more information and improved links.
## Fixed
* Various minor Demo GUI Sample bugs are fixed for smoother use.

# [0.2.0] - 2025-04-17
## Added
* It is now possible to change or override the basic configuration of the Thespeon package, enabling custom presets.
## Changed
* Models now need to be selected with a specific *quality* tag, see updated *Simple Narrator* sample for an example.
* Model packs are now downloaded as *.lingotion*-files instead of *.zip*, making them easier to find during import. Please redownload your models from [https://portal.lingotion.com](https://portal.lingotion.com).
## Fixed
* Silence in start of audio and clicks at the end of audio output is now fixed.
* Import of packs now works when a Unity project is located on a different disk.

# [0.1.0] - 2025-03-19
## Added
* Initial release of package.
