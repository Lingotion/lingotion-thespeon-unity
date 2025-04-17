// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.


using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;
//using UnityEditor.Build.Content;
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using System.Linq;
using UnityEngine.Profiling;
using Lingotion.Thespeon.Utils;
using System.Threading.Tasks;
using System.Collections;

namespace Lingotion.Thespeon.ThespeonRunscripts
{
    public class ThespeonPhonemizer : ThespeonInferenceStep<Dictionary<string, (List<int>, string)>, PhonemizerInput>
    {
        private Vocabularies vocabularies;
        // private Dictionary<string, int> charToIdphonemeVocab_tts;
        private const string SOS_TOKEN = "<sos>";
        private const string EOS_TOKEN = "<eos>";




        public ThespeonPhonemizer(ref Worker phonemizerWorker, ref Vocabularies vocabs, double targetFrameTime, bool useAdaptiveScheduling, float overshootMargin) : base(overshootMargin)
        {
            _workers = new[]{phonemizerWorker};
            // Phonemizer adapts quickly, let it schedule a bit more
            TargetFrameTime = targetFrameTime * 1.2;

            HeavyLayers.Add(new List<int>());
            vocabularies = vocabs;
            UseAdaptiveScheduling = useAdaptiveScheduling;
            TargetFrameTime = targetFrameTime;
        }

        private List<int> EncodeSequence(string seq)
        {
            // Implement encoding logic based on your vocabulary
            List<int> encoded = new List<int>();
            foreach (var token in seq)
            {
                encoded.Add(vocabularies.grapheme_vocab[token.ToString().ToLower()]);
            }
            return encoded;
        }
        string DecodeSequence(List<int> seq)
        {
            // Implement decoding logic based on your inverse vocabulary
            string decoded = "";
            foreach (var token in seq)
            {
                decoded += vocabularies.phoneme_ivocab[token];
            }
            return decoded;
        }

        

        public override IEnumerator Infer(PhonemizerInput phonemizerInputs)
        {
            float phonemizerStartTime = Time.realtimeSinceStartup;
            // Dictionary to store the results: word -> (IDs, phoneme string)
            var wordPhonemeMap = new Dictionary<string, (List<int>, string)>();
            Profiler.BeginSample("Phonemize identify");
            // 1) Identify unique words to minimize repeated inferences
            var uniqueWords = phonemizerInputs.words
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct()
                .ToList();

            Profiler.EndSample();
            // If no words, just return empty
            if (uniqueWords.Count == 0)
            {
                phonemizerInputs.TaskCompletion.SetResult(wordPhonemeMap);
                yield break;
            }

            // 2) Prepare a batched input for the phonemizer
            
            Profiler.BeginSample("Phonemize prepare");
            //    (similar logic as before but simpler, since we only have words)
            int maxInLength = uniqueWords.Max(s => s.Length) + 2; // +2 for SOS and EOS
            int batchSize = uniqueWords.Count;

            TensorShape batchShape = new TensorShape(batchSize, maxInLength);
            Tensor<int> inputTensor = new Tensor<int>(batchShape,  true);

            // Encode each unique word into the batch
            for (int i = 0; i < batchSize; i++)
            {
                string word = uniqueWords[i];
                List<int> srcSeq = EncodeSequence(word);

                // Insert special tokens if required by your model
                srcSeq.Insert(0, vocabularies.grapheme_vocab[SOS_TOKEN]);  // Insert SOS
                srcSeq.Add(vocabularies.grapheme_vocab[EOS_TOKEN]);        // Insert EOS

                // Copy into the batch row
                for (int c = 0; c < srcSeq.Count; c++)
                {
                    inputTensor[i, c] = srcSeq[c];
                }
            }
            Profiler.EndSample();
            // 3) Initialize the target indices with SOS for each sequence
            Tensor<int> tgtIndices = new Tensor<int>(
                new TensorShape(batchSize, 1),
                Enumerable.Repeat(vocabularies.phoneme_vocab[SOS_TOKEN], batchSize).ToArray()
            );

            // For tracking when each sequence is finished (on EOS)
            int[] eos_start_values = Enumerable.Repeat(1, batchSize).ToArray();
            Tensor<int> eos_mask = new Tensor<int>(new TensorShape(batchSize, 1), eos_start_values);
            Tensor<int> finished_indices = new Tensor<int>(new TensorShape(batchSize));
            Tensor<int> num_finished_tensor = null;

            IEnumerator phonemizerSchedule = null; 
            int num_finished = 0;
            bool first_iteration = true;
            bool hasLayersLeft = true;
            int counter = 0;
            float startTime = 0f;
            float currentElapsedTime = 0f;
            
            // 4) Run the phonemizer model until all words reach EOS
            while (num_finished < batchSize)
            {
            
                Profiler.BeginSample("BatchedPhonemize");
                phonemizerSchedule = _workers[0].ScheduleIterable(inputTensor, tgtIndices, eos_mask, finished_indices);
                // Single step of decoding
                hasLayersLeft = true;
                while(hasLayersLeft)
                {
                    
                    Profiler.EndSample();
                    // move one frame and wait for the end of it.
                    yield return null;
                    yield return new WaitForEndOfFrame();                    
                    startTime = Time.realtimeSinceStartup;
                    // start a new "block" of deferred calls:
                    
                    Profiler.BeginSample("BatchedPhonemize");
                    while(true)
                    {
                        // schedule a call
                        hasLayersLeft = phonemizerSchedule.MoveNext();
                        counter++;
                        // have we exceeded the budget?
                        currentElapsedTime = Time.realtimeSinceStartup - startTime;
                        if(!hasLayersLeft || currentElapsedTime > TargetFrameTime || HeavyLayers[0].Contains(counter))//|| counter == 271 || counter == 270)
                        {
                            if (UseAdaptiveScheduling && currentElapsedTime > TargetFrameTime * OvershootMargin)
                            {

                                // If layer still is too heavy, add another before it
                                if(HeavyLayers[0].Contains(counter - 1))
                                {
                                    AddHeavyLayer(0, counter - 2);
                                } else 
                                {
                                    AddHeavyLayer(0, counter - 1);
                                }
                                
                            } 
                                    
                            break;
                        }
                    }
                }
                
                Profiler.EndSample();
                Profiler.BeginSample("BatchFinalize");
                // Clean up references from last iteration
                tgtIndices.Dispose();
                eos_mask.Dispose();
                finished_indices.Dispose();
                if (!first_iteration) num_finished_tensor.Dispose();


                // Retrieve outputs
                Tensor[] placeholders = new Tensor[4];
                _workers[0].CopyOutput(0, ref placeholders[0]);
                tgtIndices = (Tensor<int>)placeholders[0];

                _workers[0].CopyOutput(1, ref placeholders[1]);
                eos_mask = (Tensor<int>)placeholders[1];

                _workers[0].CopyOutput(2, ref placeholders[2]);
                finished_indices = (Tensor<int>)placeholders[2];

                placeholders[3] = _workers[0].PeekOutput(3);
                num_finished_tensor = placeholders[3].ReadbackAndClone() as Tensor<int>;

                // Update finished count
                num_finished = num_finished_tensor[0];
                first_iteration = false;
                Profiler.EndSample();
            }

            // Copy out the final predicted phonemes from the CPU
            var eosArray = finished_indices.DownloadToArray();
            var cpuResultTensor = tgtIndices.ReadbackAndClone();

            // Clean up data
            inputTensor.Dispose();
            tgtIndices.Dispose();
            eos_mask.Dispose();
            finished_indices.Dispose();
            num_finished_tensor.Dispose();

            // 5) For each unique word, reconstruct the phoneme IDs from the row data
            
            Profiler.BeginSample("Phonemize reconstruct");
            for (int i = 0; i < batchSize; i++)
            {
                int eosPos = eosArray[i];
                // If the model never filled in the row, just treat it as full length
                if (eosPos == 0) eosPos = cpuResultTensor.shape[1];

                // Build the ID list for the phonemes (skipping position 0 for SOS)
                var outputSeq = new List<int>();
                for (int c = 1; c < eosPos; c++)
                {
                    outputSeq.Add((int)cpuResultTensor[i, c]);
                }

                // Convert numeric IDs to a phoneme string (if you want a readable version)
                string decodedOutput = DecodeSequence(outputSeq);

                // Store the result in the dictionary
                string word = uniqueWords[i];
                wordPhonemeMap[word] = (outputSeq, decodedOutput);
            }

            // Return the dictionary: word -> (phoneme IDs, phoneme string)
            phonemizerInputs.TaskCompletion.SetResult(wordPhonemeMap);
            cpuResultTensor.Dispose();
            Profiler.EndSample();
        }
        
        protected override void DestroyInstance()
        {
            return;
        }

    }
    

    public class PhonemizerInput : InferenceInputs<Dictionary<string, (List<int>, string)>>
    {
        public List<string> words;
        public PhonemizerInput(Tensor[] inputs, TaskCompletionSource<Dictionary<string, (List<int>, string)>> tcs, List<string> words): base(inputs, tcs)
        {
            this.words = words;
        }
    }
}