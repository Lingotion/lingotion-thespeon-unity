> [!TIP]
> If you are experiencing very slow synthesis when running Thespeon with BackendType CPU but only when running in Editor - consider turning off Burst Native Debug Mode Compilation.

# Known Issues and Limitations  
**These issues are known and are currently being addressed by the Lingotion development team. If you find any other issues, please create a new issue through the [GitHub repository](https://github.com/Lingotion/lingotion-thespeon-unity/issues/new).**
* The Unity InferenceEngine GPUCompute backend does not work properly on Windows from Unity version 6000.0.53f1 and forward due to a buffer allocation issue in the DirectX pipeline. This issue has been reported to the Unity team, and we will update the package as soon as a fix is released. Until then, please use a Unity Editor version lower than 6000.0.53f1 to use the GPU backend. On higher versions, please use the CPU backend when synthesizing.
* The first synthesis has higher latency and performance impact than subsequent synthetizations due to buffer initializations. It is advised to utilize `TryPreloadActor` or `TryPreloadActorCoroutine` with the `runWarmup` flag enabled.
* Heteronyms are not recognized based on context. As a workaround you can insert your heteronym words in separate segments with custom phonemization IPA text to get the correct pronunciation. See [this guide](./actor-control.md#controlling-pronunciation) for details on using custom IPA segments.
Examples of heteronyms:
   1. **English Heteronyms**  
      1. **Lead**  
         - *(to go first)* – /liːd/  
         - *(a type of metal)* – /lɛd/  
      2. **Tear**  
         - *(to rip)* – /tɛəɹ/  
         - *(a drop of liquid from the eye)* – /tɪəɹ/  
      3. **Wind**  
         - *(movement of air)* – /wɪnd/  
         - *(to turn or coil)* – /waɪnd/   
      4. **Desert**  
         - *(a barren place)* – /ˈdɛzɚt/  
         - *(to abandon)* – /dɪˈzɜːt/  

   2. **Swedish Heteronyms**  
      1. **Banan**  
         - *(A banana)* –  /baˈnɑːn/ 
         - *(The way)* – /ˈbɑːnan/  

* Build to web is not yet supported.
* The audio callback system gives accurate audio sample indices, but streamed audio playback Unity has a slight delay. One way to remedy this is to shift the indices by a flat amount until it aligns.
* Certain combinations of random consonants might cause the engine to get confused and generate a long series of gibberish. 
* Very short syntheses - shorter than about 1/3 of a second - are currently blocked.
