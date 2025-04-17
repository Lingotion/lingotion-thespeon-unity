// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.


using UnityEngine;
using Unity.Sentis;
using System.IO;
using UnityEditor;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lingotion.Thespeon.Utils;

namespace Lingotion.Thespeon
{

    public static class VocoderBuilder
    {
        // Input folder containing ONNX files
        static int[] upsample_paddings;
        static int[] resblock_kernel_sizes;
        static int[] upsample_kernel_sizes;
        static int[] upsample_rates;
        static int[][] resblock_dilation_sizes;
        static int[] upsample_channels;
        static int preconv_padding;
        static int postconv_padding;
        static int resblk_type;

        public static void SerializeVocoder(Dictionary<string, Model> inputModels, ref Dictionary<string, Model> outputModels, Config config)
        {
            ParseConfig(config);
            List<string> modelNames = new List<string>{"vocoder_first_chunk", "vocoder_middle_chunk", "vocoder_last_chunk"};
            foreach (string modelName in modelNames)
            {
                if (outputModels.ContainsKey(modelName))
                {
                    outputModels.Remove(modelName);
                }
            }
            outputModels.Add("vocoder_first_chunk", CreateFirstChunkModel(inputModels));
            outputModels.Add("vocoder_middle_chunk", CreateMiddleChunkModel(inputModels));
            outputModels.Add("vocoder_last_chunk", CreateLastChunkModel(inputModels));
        }


        private static void ParseConfig(Config config)
        {

            upsample_rates = config.upsample_rates.ToArray();

            upsample_kernel_sizes = config.upsample_kernel_sizes.ToArray();

            resblk_type = config.resblock;

            Debug.Assert(upsample_kernel_sizes.Length == upsample_rates.Length);

            int initial_channel = config.upsample_initial_channel;

            resblock_kernel_sizes = config.resblock_kernel_sizes.ToArray();

            preconv_padding = config.pre_conv_kernel / 2;
            postconv_padding = config.post_conv_kernel / 2;

            upsample_paddings = new int[upsample_kernel_sizes.Length];
            upsample_channels = new int[upsample_kernel_sizes.Length + 1];

            for (int i = 0; i < upsample_kernel_sizes.Length; i++)
            {
                upsample_paddings[i] = (upsample_kernel_sizes[i] - upsample_rates[i]) / 2;

                upsample_channels[i] = (int)(initial_channel / Math.Pow(2, i + 1));
            }

            upsample_channels[upsample_channels.Length - 1] = initial_channel;

            resblock_dilation_sizes = config.resblock_dilation_sizes.Select(a => a.ToArray()).ToArray();

        }

        static Model CreateFirstChunkModel(Dictionary<string, Model> inputModels)
        {
            var graph = new FunctionalGraph();
            var target_shape = DynamicTensorShape.DynamicOfRank(3);

            
            var chunked_input = graph.AddInput<float>(target_shape);
            var loudness_input = graph.AddInput<float>(target_shape);

            int resblk_rest_count = 2 * resblock_dilation_sizes.Length * resblock_kernel_sizes.Length * 4;

            int add_rest_count = (upsample_channels.Length - 1) * (resblock_kernel_sizes.Length - 1);
            // output, x_rest, ups_rests, resblk_rests, add_rests, out_rest
            FunctionalTensor[] outputs = new FunctionalTensor[1 + 1 + 4 + resblk_rest_count + add_rest_count + 1  + 1];
            int[] pads = { preconv_padding, 0, 0, 0, 0, 0 };
            var padded = Functional.Pad(chunked_input, pads, 0);
            // REST 1
            outputs[1] = padded[.., .., ^(preconv_padding * 2)..];

            var preconv = Functional.ForwardWithCopy(inputModels["vocoder_preconv"], padded);

            int resblk_index = 0;
            var ups_in = preconv[0];

            for (int u = 0; u < 4; u++)
            {

                //// REST 2: upsample overlap value
                var lrelu = Functional.LeakyRelu(ups_in, 0.1f);
                outputs[2 + u] = lrelu[.., .., ^1..];

                // Upsample input
                var upsampled = Functional.ForwardWithCopy(inputModels[$"vocoder_upsampler_{u}"], lrelu);
                // Remove tail garbage
                FunctionalTensor upsampled_trimmed = null;
                if (upsample_paddings[u] != 0)
                {
                    upsampled_trimmed = upsampled[0][.., .., ..^upsample_paddings[u]];
                }
                else
                {
                    upsampled_trimmed = upsampled[0];
                }
                FunctionalTensor xs = null;
                int rest_amount = 0;
                for (int k = 0; k < resblock_kernel_sizes.Length; k++)
                {

                    var resblk_input = Functional.Clone(upsampled_trimmed);
                    int[] current_dilations = resblock_dilation_sizes[k];
                    int pad_1 = 0, pad_2 = 0;
                    for (int d = 0; d < current_dilations.Length; d++)
                    {
                        pad_1 = (resblock_kernel_sizes[k] - 1) * current_dilations[d] / 2;
                        int[] resblk_pad = { pad_1, 0, 0, 0, 0, 0 };
                        var resblk_input_padded = Functional.Pad(resblk_input, resblk_pad, 0);

                        // REST 3: save tail end -pad_1 * 2 of padded
                        outputs[6 + resblk_index] = resblk_input_padded[.., .., ^(pad_1 * 2)..];

                        resblk_index++;

                        // conv1
                        var c1 = Functional.ForwardWithCopy(inputModels[$"vocoder_ups_{u}_kernel_{k}_dilation_{d}_convs1"], resblk_input_padded)[0];

                        pad_2 = (resblock_kernel_sizes[k] - 1) / 2;

                        resblk_pad[0] = pad_2;
                        var padded_c1 = Functional.Pad(c1, resblk_pad, 0);

                        //REST 4: Save tail end -pad_1 * 2 of padded
                        outputs[6 + resblk_index] = padded_c1[.., .., ^(pad_2 * 2)..];

                        resblk_index++;

                        // conv2
                        var c2 = Functional.ForwardWithCopy(inputModels[$"vocoder_ups_{u}_kernel_{k}_dilation_{d}_convs2"], padded_c1)[0];
                        // Align input to c2, rest handled in next chunk
                        var input_sliced = resblk_input[.., .., ..^(pad_1 + pad_2)];
                        resblk_input = Functional.Clone(c2 + input_sliced);

                    }

                    // Save rest amount to align next kernel result
                    if (xs == null)
                    {
                        rest_amount = (pad_1 + pad_2) * 2;
                        xs = Functional.Clone(resblk_input);
                    }
                    else
                    {
                        outputs[78 + u * (resblock_kernel_sizes.Length - 1) + (k - 1)] = xs[.., .., ^((pad_1 + pad_2) * 2 - rest_amount)..];
                        var xs_aligned = xs[.., .., ..^((pad_1 + pad_2) * 2 - rest_amount)];
                        xs = resblk_input + xs_aligned;
                        rest_amount = (pad_1 + pad_2) * 2;

                    }

                }
                ups_in = xs / resblock_kernel_sizes.Length;

            }

            var final_zero_pad = Functional.Pad(Functional.LeakyRelu(ups_in), pads, 0);

            outputs[86] = final_zero_pad[.., .., ^(postconv_padding * 2)..];

            var loudness = loudness_input;

            var final_outputs= Functional.ForwardWithCopy(inputModels["vocoder_postconv"], new [] {final_zero_pad,loudness } );

            outputs[0] = final_outputs[0];

            outputs[87] = final_outputs[1];


            var output_model = graph.Compile(outputs);
            return output_model;
        }

        static Model CreateMiddleChunkModel(Dictionary<string, Model> inputModels)
        {
            var graph = new FunctionalGraph();
            int resblk_rest_count = 2 * resblock_dilation_sizes.Length * resblock_kernel_sizes.Length * 4;
            int add_rest_count = (upsample_channels.Length - 1) * (resblock_kernel_sizes.Length - 1);
            var target_shape = DynamicTensorShape.DynamicOfRank(3);

            // input, x_rest, ups_rests, resblk_rests, add_rests, out_rest, loudness_rest
            FunctionalTensor[] inputs = new FunctionalTensor[1 + 1 + 4 + resblk_rest_count + add_rest_count + 1 + 1];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = graph.AddInput<float>(target_shape);
            }
            // output, x_rest, ups_rests, resblk_rests, add_rests, out_rest, loudness_rest
            FunctionalTensor[] outputs = new FunctionalTensor[1 + 1 + 4 + resblk_rest_count + add_rest_count + 1  + 1];

            var padded = Functional.Concat(new[] { inputs[1], inputs[0] }, 2);
            // REST 1
            outputs[1] = padded[.., .., ^(preconv_padding * 2)..];

            var preconv = Functional.ForwardWithCopy(inputModels["vocoder_preconv"], padded);

            int resblk_index = 0;
            var ups_in = preconv[0];

            for (int u = 0; u < 4; u++)
            {

                //// REST 2: upsample overlap value
                var lrelu = Functional.LeakyRelu(ups_in, 0.1f);
                // If the transpose convolution has padding, append overlap value
                if (upsample_paddings[u] != 0)
                {
                    lrelu = Functional.Concat(new[] { inputs[2 + u], lrelu }, 2);
                }

                outputs[2 + u] = lrelu[.., .., ^1..];
                // Upsample input
                var upsampled = Functional.ForwardWithCopy(inputModels[$"vocoder_upsampler_{u}"], lrelu);
                FunctionalTensor upsampled_trimmed = null;
                // Remove head and tail garbage
                if (upsample_paddings[u] != 0)
                {
                    upsampled_trimmed = upsampled[0][.., .., upsample_paddings[u]..^upsample_paddings[u]];
                }
                else
                {
                    upsampled_trimmed = upsampled[0];
                }

                FunctionalTensor xs = null;
                int rest_amount = 0;
                for (int k = 0; k < resblock_kernel_sizes.Length; k++)
                {

                    var resblk_input = Functional.Clone(upsampled_trimmed);
                    int[] current_dilations = resblock_dilation_sizes[k];
                    int pad_1 = 0, pad_2 = 0;
                    for (int d = 0; d < current_dilations.Length; d++)
                    {
                        pad_1 = (resblock_kernel_sizes[k] - 1) * current_dilations[d] / 2;

                        //int[] resblk_pad = {pad_1, 0, 0, 0, 0, 0};
                        var resblk_input_padded = Functional.Concat(new[] { inputs[6 + resblk_index], resblk_input }, 2);

                        // REST 3: save tail end -pad_1 * 2 of padded
                        outputs[6 + resblk_index] = resblk_input_padded[.., .., ^(pad_1 * 2)..];

                        resblk_index++;

                        // conv1
                        var c1 = Functional.ForwardWithCopy(inputModels[$"vocoder_ups_{u}_kernel_{k}_dilation_{d}_convs1"], resblk_input_padded)[0];

                        pad_2 = (resblock_kernel_sizes[k] - 1) / 2;



                        //resblk_pad[0] = pad_2;
                        var padded_c1 = Functional.Concat(new[] { inputs[6 + resblk_index], c1 }, 2);

                        //REST 4: Save tail end -pad_1 * 2 of padded
                        outputs[6 + resblk_index] = padded_c1[.., .., ^(pad_2 * 2)..];

                        resblk_index++;

                        // conv2
                        var c2 = Functional.ForwardWithCopy(inputModels[$"vocoder_ups_{u}_kernel_{k}_dilation_{d}_convs2"], padded_c1)[0];
                        // Slice off padding portion of input head
                        var input_sliced_head = resblk_input_padded[.., .., (pad_1 - (resblock_kernel_sizes[k] - 1) / 2)..];

                        // Align input to c2, rest handled in next chunk
                        var input_sliced = input_sliced_head[.., .., ..^(pad_1 + pad_2)];
                        resblk_input = Functional.Clone(c2 + input_sliced);

                    }

                    // Save rest amount to align next kernel result
                    if (xs == null)
                    {
                        rest_amount = (pad_1 + pad_2) * 2;
                        xs = Functional.Clone(resblk_input);
                    }
                    else
                    {

                        outputs[78 + u * (resblock_kernel_sizes.Length - 1) + (k - 1)] = xs[.., .., ^((pad_1 + pad_2) * 2 - rest_amount)..];

                        var appended_xs = Functional.Concat(new[] { inputs[78 + u * (resblock_kernel_sizes.Length - 1) + (k - 1)], xs }, 2);

                        var xs_aligned = appended_xs[.., .., ..^((pad_1 + pad_2) * 2 - rest_amount)];
                        xs = resblk_input + xs_aligned;
                        rest_amount = (pad_1 + pad_2) * 2;

                    }

                }
                ups_in = xs / resblock_kernel_sizes.Length;

            }

            var final_zero_pad = Functional.Concat(new[] { inputs[86], Functional.LeakyRelu(ups_in) }, 2);

            outputs[86] = final_zero_pad[.., .., ^(postconv_padding * 2)..];


            var loudness =inputs[87];

            var final_outputs= Functional.ForwardWithCopy(inputModels["vocoder_postconv"], new [] {final_zero_pad,loudness } );

            outputs[0] = final_outputs[0];

            outputs[87] = final_outputs[1];

            var output_model = graph.Compile(outputs);
            return output_model;
        }

        static Model CreateLastChunkModel(Dictionary<string, Model> inputModels)
        {
            //TODO create final final slicer to slice away and remove last 3 values.

            var graph = new FunctionalGraph();
            int resblk_rest_count = 2 * resblock_dilation_sizes.Length * resblock_kernel_sizes.Length * 4;
            int add_rest_count = (upsample_channels.Length - 1) * (resblock_kernel_sizes.Length - 1);
            var target_shape = DynamicTensorShape.DynamicOfRank(3);

            FunctionalTensor[] inputs = new FunctionalTensor[1 + 1 + 4 + resblk_rest_count + add_rest_count + 1 + 1];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = graph.AddInput<float>(target_shape);
            }
            // output, x_rest, ups_rests, resblk_rests, add_rests, out_rest
            FunctionalTensor output;
            int[] pads = { 0, preconv_padding, 0, 0, 0, 0 };
            inputs[0] = inputs[0][.., .., ..^3];
            var padded = Functional.Concat(new[] { inputs[1], Functional.Pad(inputs[0], pads, 2) }, 2);

            var preconv = Functional.ForwardWithCopy(inputModels["vocoder_preconv"], padded);

            int resblk_index = 0;
            var ups_in = preconv[0];

            for (int u = 0; u < 4; u++)
            {

                var lrelu = Functional.LeakyRelu(ups_in, 0.1f);
                // If the transpose convolution has padding, append overlap value
                if (upsample_paddings[u] != 0)
                {
                    lrelu = Functional.Concat(new[] { inputs[2 + u], lrelu }, 2);
                }

                // Upsample input
                var upsampled = Functional.ForwardWithCopy(inputModels[$"vocoder_upsampler_{u}"], lrelu);
                FunctionalTensor upsampled_trimmed = null;
                // Remove head garbage
                if (upsample_paddings[u] != 0)
                {
                    upsampled_trimmed = upsampled[0][.., .., upsample_paddings[u]..];
                }
                else
                {
                    upsampled_trimmed = upsampled[0];
                }

                FunctionalTensor xs = null;
                int rest_amount = 0;
                for (int k = 0; k < resblock_kernel_sizes.Length; k++)
                {

                    var resblk_input = Functional.Clone(upsampled_trimmed);
                    int[] current_dilations = resblock_dilation_sizes[k];
                    int pad_1 = 0, pad_2 = 0;
                    for (int d = 0; d < current_dilations.Length; d++)
                    {
                        pad_1 = (resblock_kernel_sizes[k] - 1) * current_dilations[d] / 2;

                        int[] resblk_pad = { 0, pad_1, 0, 0, 0, 0 };
                        var resblk_input_padded = Functional.Concat(new[] { inputs[6 + resblk_index], Functional.Pad(resblk_input, resblk_pad, 0) }, 2);

                        resblk_index++;

                        // conv1
                        var c1 = Functional.ForwardWithCopy(inputModels[$"vocoder_ups_{u}_kernel_{k}_dilation_{d}_convs1"], resblk_input_padded)[0];

                        pad_2 = (resblock_kernel_sizes[k] - 1) / 2;

                        var padded_c1 = Functional.Concat(new[] { inputs[6 + resblk_index], Functional.Pad(c1, resblk_pad, 0) }, 2);

                        resblk_index++;

                        // conv2
                        var c2 = Functional.ForwardWithCopy(inputModels[$"vocoder_ups_{u}_kernel_{k}_dilation_{d}_convs2"], padded_c1)[0];
                        // Slice off padding portion of input head
                        int offset = pad_1 - (resblock_kernel_sizes[k] - 1) / 2;
                        var input_sliced_head = resblk_input_padded[.., .., offset..];

                        // Align input to c2, rest handled in next chunk
                        var input_sliced = input_sliced_head[.., .., ..^(pad_1 - offset)];
                        resblk_input = Functional.Clone(c2 + input_sliced);

                    }

                    // Save rest amount to align next kernel result
                    if (xs == null)
                    {
                        rest_amount = (pad_1 + pad_2) * 2;
                        xs = Functional.Clone(resblk_input);
                    }
                    else
                    {

                        var appended_xs = Functional.Concat(new[] { inputs[78 + u * (resblock_kernel_sizes.Length - 1) + (k - 1)], xs }, 2);

                        xs = resblk_input[.., .., ..^(12 * k)] + appended_xs;
                        rest_amount = (pad_1 + pad_2) * 2;

                    }

                }
                ups_in = xs / resblock_kernel_sizes.Length;

            }

            pads[1] = postconv_padding;
            var final_zero_pad = Functional.Concat(new[] { inputs[86], Functional.Pad(Functional.LeakyRelu(ups_in), pads, 0) }, 2);


            var loudness =inputs[87];

            var final_outputs= Functional.ForwardWithCopy(inputModels["vocoder_postconv"], new [] {final_zero_pad,loudness } );

            output = final_outputs[0];



            var output_model = graph.Compile(output);
            return output_model;
        }
    }
}