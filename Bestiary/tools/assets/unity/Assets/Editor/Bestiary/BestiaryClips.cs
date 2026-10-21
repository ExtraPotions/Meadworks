// Bestiary bundle build: humanoid clips from editor curves. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Bestiary.Build
{
    /// <summary>One clip as tools/assets/clip_curves.py writes it (build/assets/curves/&lt;bundle&gt;/&lt;clip&gt;.json).</summary>
    [Serializable]
    public class ClipRecord
    {
        public string name;
        public float sampleRate;
        public ClipSettingsRecord settings;
        public ClipEventRecord[] events;
        public CurveRecord[] curves;
    }

    [Serializable]
    public class ClipSettingsRecord
    {
        public bool mirror, loopTime, loopBlend, loopBlendOrientation, loopBlendPositionY, loopBlendPositionXZ;
        public bool keepOriginalOrientation, keepOriginalPositionY, keepOriginalPositionXZ, heightFromFeet;
        public float startTime, stopTime, orientationOffsetY, level, cycleOffset;
    }

    [Serializable]
    public class ClipEventRecord
    {
        public float time;
        public string functionName, data;
        public float floatParameter;
        public int intParameter;
    }

    /// <summary>A humanoid value's curve: its attribute index and its keys, flat (time, value, inSlope, outSlope).</summary>
    [Serializable]
    public class CurveRecord
    {
        public int attribute;
        public float[] keys;
    }

    /// <summary>
    /// Writes a humanoid .anim from a clip record: one Animator float curve per humanoid value, named as Unity names
    /// them in a clip (RootT.x, LeftHandQ.w, "Spine Front-Back", "LeftHand.Index.1 Stretched"), with the clip's settings
    /// and events. Unity compiles the muscle clip from these curves when it builds the bundle; no avatar is needed.
    /// </summary>
    public static class BestiaryClips
    {
        private static readonly string[] Goals = { "LeftFoot", "RightFoot", "LeftHand", "RightHand" };
        private static readonly string[] Components = { "T.x", "T.y", "T.z", "Q.x", "Q.y", "Q.z", "Q.w" };
        private static readonly Regex Finger = new Regex(@"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (.+)$");

        public static AnimationClip Build(string recordPath, string clipPath)
        {
            var record = JsonUtility.FromJson<ClipRecord>(File.ReadAllText(recordPath));
            AssetDatabase.DeleteAsset(clipPath);
            var clip = new AnimationClip { name = record.name, frameRate = record.sampleRate };
            foreach (CurveRecord curve in record.curves ?? new CurveRecord[0])
            {
                var keys = new Keyframe[curve.keys.Length / 4];
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = new Keyframe(curve.keys[4 * i], curve.keys[4 * i + 1], curve.keys[4 * i + 2], curve.keys[4 * i + 3]);
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), Name(curve.attribute));
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
            }
            AssetDatabase.CreateAsset(clip, clipPath);
            ClipSettingsRecord s = record.settings;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.mirror = s.mirror;
            settings.loopTime = s.loopTime;
            settings.loopBlend = s.loopBlend;
            settings.loopBlendOrientation = s.loopBlendOrientation;
            settings.loopBlendPositionY = s.loopBlendPositionY;
            settings.loopBlendPositionXZ = s.loopBlendPositionXZ;
            settings.keepOriginalOrientation = s.keepOriginalOrientation;
            settings.keepOriginalPositionY = s.keepOriginalPositionY;
            settings.keepOriginalPositionXZ = s.keepOriginalPositionXZ;
            settings.heightFromFeet = s.heightFromFeet;
            settings.orientationOffsetY = s.orientationOffsetY;
            settings.level = s.level;
            settings.cycleOffset = s.cycleOffset;
            if (record.curves != null && record.curves.Length > 0)
            {
                settings.startTime = s.startTime;
                settings.stopTime = s.stopTime;
            }
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, (record.events ?? new ClipEventRecord[0]).Select(e => new AnimationEvent
            {
                time = e.time, functionName = e.functionName, stringParameter = e.data ?? "",
                floatParameter = e.floatParameter, intParameter = e.intParameter,
            }).ToArray());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            BestiaryBundles.Line($"clip {record.name}: {record.curves?.Length ?? 0} curves, length {clip.length:F3}s, " +
                                 $"human {clip.humanMotion}, {record.events?.Length ?? 0} events");
            return clip;
        }

        /// <summary>The clip curve name of a humanoid value index (Unity's order: motion, root, goals, muscles).</summary>
        public static string Name(int attribute)
        {
            if (attribute >= 7 && attribute < 14)
                return "Root" + Components[attribute - 7];
            if (attribute >= 14 && attribute < 42)
                return Goals[(attribute - 14) / 7] + Components[(attribute - 14) % 7];
            int muscle = attribute - 42;
            if (muscle >= 0 && muscle < HumanTrait.MuscleCount)
            {
                string name = HumanTrait.MuscleName[muscle];
                Match m = Finger.Match(name);
                return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : name;
            }
            throw new ArgumentException("unsupported humanoid value index " + attribute);
        }
    }
}
