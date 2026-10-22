// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// One log line for each quiet gap after parked tames regain their stored stars.
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Stars;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// Notices when Tamed Stars turns on, and writes one line after a run of restores goes quiet.
    /// Creatures that regain stars in the same moment share that line.
    /// </summary>
    internal static class StarRestoreBatch
    {
        static int pending;
        static bool added;
        static bool wasOn;
        static bool haveWas;
        static int seenFrame = -1;
        static int promoteThroughFrame = -1;
        static long generation = long.MinValue;
        static bool settingOn;

        /// <summary>The server's Tamed Stars value for this frame, after rules and authority are in.</summary>
        public static bool SettingOn => settingOn;

        /// <summary>True on the frame the setting turns on and the frame after, so loaded tames are restored together.</summary>
        public static bool Due(int frame)
        {
            long gen = CruciblePlugin.Session.Generation;
            if (gen != generation)
            {
                generation = gen;
                haveWas = false;
                seenFrame = -1;
                pending = 0;
                added = false;
                promoteThroughFrame = -1;
            }
            if (frame != seenFrame)
            {
                seenFrame = frame;
                bool on = ReadOn();
                settingOn = on;
                if (!haveWas)
                {
                    haveWas = true;
                    wasOn = on;
                    if (on) promoteThroughFrame = frame + 1;
                }
                else if (on && !wasOn)
                {
                    wasOn = true;
                    promoteThroughFrame = frame + 1;
                }
                else wasOn = on;
            }
            return frame <= promoteThroughFrame;
        }

        public static void Count()
        {
            pending++;
            added = true;
        }

        /// <summary>Call after creature updates. A frame that restored nothing ends the batch and logs it.</summary>
        public static void EndFrame()
        {
            if (added)
            {
                added = false;
                return;
            }
            Write();
        }

        public static void FlushNow()
        {
            added = false;
            Write();
        }

        static void Write()
        {
            if (pending == 0) return;
            Log.Info("Wildblood restored stored stars on " + pending + " tamed " + (pending == 1 ? "creature." : "creatures."));
            pending = 0;
        }

        static bool ReadOn()
        {
            if (!ExtraPotions.Wildblood.Runtime.Plugin.NativeRulesReady) return false;
            var session = CruciblePlugin.Session;
            if (!TamedStars.MayAdoptRules(session.Role == ExtraPotions.Meadworks.Crucible.SessionRole.RemoteClient, session.AuthorityReady))
                return false;
            var settings = ExtraPotions.Wildblood.Runtime.Plugin.Settings;
            return settings != null && settings.Get(WildbloodSettings.General.Enabled)
                && settings.Get(WildbloodSettings.Traits.TamedStars);
        }
    }
}
