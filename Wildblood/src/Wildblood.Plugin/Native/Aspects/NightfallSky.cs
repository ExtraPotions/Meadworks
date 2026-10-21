// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Nightfall's sky: every player in the fight - their own player within `range` of a living Nightfall boss,
    /// measured along the ground - sees a storming midnight on their own screen until the fight lets them go: the boss
    /// dies or is unloaded, or they leave. Attached by AspectInstaller on every machine; on one with a screen this only
    /// puts the boss on the sky's list while it is loaded and answers whether it holds a player
    /// (<see cref="NightfallBlend"/> decides how much storm to show, <see cref="NightfallLook"/> draws it,
    /// <see cref="NightfallChill"/> makes the player feel it). It is what each client draws, not the world's weather
    /// or time, so nothing is sent and nothing is stored, and a dedicated server, which draws nothing, never lists a
    /// boss at all.
    /// </summary>
    public sealed class NightfallSky : MonoBehaviour
    {
        private Character? _boss;
        private bool _listed;

        private void Start() => Guard.Run("NightfallSky.Start", Setup);

        private void Setup()
        {
            _boss = GetComponent<Character>();
            if (_boss == null || StormEffects.Headless())
            {
                enabled = false;
                return;
            }
            NightfallBlend.Join(this);
            _listed = true;
        }

        private void OnDestroy()
        {
            if (_listed)
            {
                NightfallBlend.Leave(this);
            }
        }

        /// <summary>
        /// True while the boss lives and <paramref name="viewer"/> is within <paramref name="reach"/> m of it along the
        /// ground, so a flying Moder holds the players below her. Only the owner marks a boss dead, after any death
        /// animation, so the replicated health reaching zero is what ends the fight on every other machine.
        /// </summary>
        public bool Holds(Vector3 viewer, float reach) =>
            isActiveAndEnabled && _boss != null && _boss.GetComponent<Runtime.EliteController>() is Runtime.EliteController controller
            && controller.Ready && !_boss.IsDead() && _boss.GetHealth() > 0f
            && StormTargets.FlatDistance(viewer, transform.position) <= reach;
    }
}
