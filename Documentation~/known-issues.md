# Known Issues and Limitations  
**These issues are known and are currently being addressed by the Lingotion development team. If you find any other issues, please create a new issue through the [GitHub repository](https://github.com/Lingotion/lingotion-thespeon-unity/issues/new).**
* The Unity InferenceEngine GPUCompute backend does not work properly on Windows from Unity version 6000.0.53f1 and forward due to a buffer allocation issue in the DirectX pipeline. This issue has been reported to the Unity team, and we will update the package as soon as a fix is released. Until then, please use a Unity Editor version lower than 6000.0.53f1 for full functionality. A workaround is to not use the CPUCompute backend when synthesizing.
* The first synthetization has higher latency and performance impact than subsequent synthetizations due to initializations. It is advised to utilize `TryPreloadActor` or `TryPreloadActorCoroutine` with the `runWarmup` flag enabled.
* Adaptive frame insertion for increased real-time performance may cause delays or stuttering in audio after numerous inferences. A temporary workaround until this is fixed is to restart the Thespeon Engine.
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

* Build to web is not yet supported, but will be supported in the future.
* The phonemization process sometimes produces strange results for some non-letter characters such as the single quotation mark ' and sometimes interprets some short words as abbreviations when it should not. As a workaround you may utilize the custom phonemization functionality to bypass the phonemizer.
* Very short syntheses - shorter than about 1/3 of a second - are currently blocked.
* Changing dialect in segments changes the dialect of the whole input.
