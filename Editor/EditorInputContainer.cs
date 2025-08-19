// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Inputs;
using UnityEngine;
using System.Collections.Generic;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Scriptable object for Thespeon inputs. For use in Editor Window. 
    /// </summary>
    public class EditorInputContainer : ScriptableObject
    {
        public AnimationCurve speed = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve loudness = AnimationCurve.Constant(0, 1, 1);
        public List<ThespeonInputSegment> segments = new() { new("Hi! This is my voice", language: "eng", emotion: Emotion.Interest), new(" generated in real time.", language: "eng", emotion: Emotion.Interest) };
    }
}
