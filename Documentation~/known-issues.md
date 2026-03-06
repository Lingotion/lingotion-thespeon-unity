> [!TIP]
> If you are experiencing very slow synthesis when running Thespeon with BackendType CPU but only when running in Editor - consider turning off Burst Native Debug Mode Compilation.

# Known Issues and Limitations  
**These issues are known and are currently being addressed by the Lingotion development team. If you find any other issues, please create a new issue through the [GitHub repository](https://github.com/Lingotion/lingotion-thespeon-unity/issues/new).**
* The first synthesis has higher latency and performance impact than subsequent synthetizations due to buffer initializations. It is advised to utilize `TryPreloadCharacter` or `TryPreloadCharacterCoroutine` with the `runWarmup` flag enabled.
* Heteronyms are not recognized based on context. As a workaround you can insert your heteronym words in separate segments with custom phonemization IPA text to get the correct pronunciation. See [this guide](./character-control.md#controlling-pronunciation) for details on using custom IPA segments.
Examples of heteronyms:
   1. **English**: **Lead** — *(to go first)* /liːd/ vs. *(a type of metal)* /lɛd/  
   2. **Swedish**: **Banan** — *(A banana)* /baˈnɑːn/ vs. *(The way)* /ˈbɑːnan/

* Build to web is not yet supported.
* The audio sample request system gives accurate audio sample indices, but streamed audio playback in Unity has a slight delay. One way to remedy this is to shift the indices by a flat amount until it aligns.
* If an audio sample request appears inside of a numerical part of the input line the timing may be slightly off due to how numbers are parsed into text and the marker heuristically reinserted before being converted to text.
* Certain combinations of random consonants might cause the engine to get confused and generate a long series of gibberish. 
* Very short syntheses - shorter than about 1/3 of a second - are currently blocked.
* Speed and loudness curves are currently not supported. Any provided curves will be ignored during synthesis. This feature will return in a future update.
