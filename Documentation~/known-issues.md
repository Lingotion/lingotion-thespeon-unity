## **Known Issues and Limitations**  
**These issues are known and are currently being addressed by the Lingotion development team. If you find any other issues, please create a new issue through the [GitHub repository](https://github.com/Lingotion/lingotion-thespeon-unity/issues/new).**

1. The first synthetization has higher latency and performance impact than subsequent synthetizations due to initializations. It is advised to make a mock synthetization once before running the intended syntheses.
2. Adaptive frame insertion for increased real-time performance may cause delays or stuttering in audio after numerous inferences. A temporary workaround until this is fixed is to restart the Thespeon Engine.
3. Heteronyms will be supported soon but is not supported in this release. The Thespeon Engine might therefore synthesize heteronyms incorrectly. As a workaround you can insert your heteronym words in separate segments with custom phonemization IPA text to get the correct pronunciation. See [this guide](./how-to-control-thespeon.md#34-pronunciation-control) Examples:
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

4. Build to web is not yet supported, but will be supported.
5. The engine's phonemizer sometimes produces strange results for some non-letter characters such as the single quotation mark ' and sometimes interprets some short words as abbreviations when it should not. This will be resolved in an upcoming release. As a workaround you may utilize the custom phonemization functionality to bypass the phonemizer.
6. Very short syntheses - shorter than about 1/3 of a second - are currently blocked. This blockade will be lifted in a future release.