# Lingotion.Thespeon.ThespeonRunscripts API Documentation

## Class `ThespeonPhonemizer`


## Class `PhonemizerInput`


## Class `ThespeonEngine`

ThespeonEngine is the main game component for interfacing with the Thespeon API from your scene. It is responsible for loading and managing actors and modules, and for running inference jobs.

> ### Method `UserModelInput CreateModelInput(string actorName, List<UserSegment> textSegments)`
> 
> Creates a new UserModelInput object with the specified actor name and text segments.
> **Parameters:**
> 
> - `actorName`: The name of the actor to use for inference.
> - `textSegments`: A list of UserSegment objects representing the text to synthesize.
> 
> **Returns:** A new UserModelInput object.
> 
> 
> 
> ### Method `void SetCustomSkipIndices(List<int>[] customSkipIndices)`
> 
> Sets the custom skip indices for the decoder.
> **Parameters:**
> 
> - `customSkipIndices`: A list of lists of integers representing the custom skip indices for each layer of the decoder.
> 
> 
> 
> ### Method `void Synthesize(UserModelInput input, Action<float[]> audioStreamCallback = null)`
> 
> Starts a Thespeon inference job with the specified input.
> 
> 
> ### Method `void QueueSynthAudio(LingotionDataPacket<float> dataPacket)`
> 
> Enqueues a finished synthesized audio chunk.
> **Parameters:**
> 
> - `dataPacket`: 
> 
> 

## Class `ThespeonVocoder`


## Class `VocoderInput`


## Class `SplineCoefficients`

Upsamples a list to a larger size using linear interpolation.


## Class `ThespeonDecoder`


## Class `DecoderInput`


## Class `ThespeonEncoder`


## Class `EncoderInput`
