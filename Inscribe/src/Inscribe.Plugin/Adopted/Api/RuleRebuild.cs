// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// Registrations made after the rules loaded rebuild the rule set once, at the end of the frame (api.md section 3):
    /// a mod registering its classes, levels, inscriptions and pools in its Awake costs one rebuild, not one per call.
    /// Before the rules first load nothing is scheduled: that load reads every registration. The rebuild itself is
    /// <see cref="ActiveRules.RebuildForApi"/>. Runs on every peer (each registers the same things). Main thread only.
    /// </summary>
    internal static class RuleRebuild
    {
        private static bool _pending;
        private static bool _inscriptions;
        private static RuleRebuildDriver? _driver;

        /// <summary>Schedules a rebuild; <paramref name="inscriptions"/>: the inscription family must be built again.</summary>
        public static void Request(bool inscriptions)
        {
            if (ActiveRules.Generation == 0)
            {
                return;
            }
            _pending = true;
            _inscriptions |= inscriptions;
            if (_driver == null)
            {
                GameObject host = new GameObject("ECF_ApiRebuild") { hideFlags = HideFlags.HideAndDontSave };
            global::ExtraPotions.Inscribe.Runtime.NativeGameplay.Own(host);
                UnityEngine.Object.DontDestroyOnLoad(host);
                _driver = host.AddComponent<RuleRebuildDriver>();
            }
        }

        /// <summary>The frame's pending rebuild, if any (the driver's LateUpdate).</summary>
        public static void RunPending()
        {
            if (!_pending)
            {
                return;
            }
            bool inscriptions = _inscriptions;
            _pending = false;
            _inscriptions = false;
            ActiveRules.RebuildForApi(inscriptions);
        }
    }

    /// <summary>Runs <see cref="RuleRebuild.RunPending"/> after every Update of the frame. Lives on a hidden, persistent object.</summary>
    internal sealed class RuleRebuildDriver : MonoBehaviour
    {
        private void LateUpdate()
        {
            try
            {
                RuleRebuild.RunPending();
            }
            catch (Exception e)
            {
                Log.Error($"rebuilding the rules after API registrations failed: {e}");
            }
        }
    }
}
