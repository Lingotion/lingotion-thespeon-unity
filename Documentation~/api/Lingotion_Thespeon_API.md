# Lingotion.Thespeon.API API Documentation

## Class `LingotionData`

Fills the "quality" attributes in the given summary object by matching languages and emotions with the data from the specified module and actor.  1) Finds the actor by username in module.model_options?.recording_data_info?.actors. 2) For each item in summary.Summary, fuzzy-match the language using LanguageExtensions.FindClosestLanguage(). 3) For each emotion in that summary item, set "quality" to the matching ActorPack data (if found).


## Class `LingotionDataPacket`

A Lingotion data packet containing the type of data, metadata and the data itself.


## Class `LingotionSynthRequest`

A Lingotion synthetization request containing a unique ID, estimated quality, errors, warnings and metadata. Is used to initiate data synthesis.
