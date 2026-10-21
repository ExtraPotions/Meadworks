// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Text;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// <c>crit_chance</c> (Keen Eye) and <c>crit_damage</c> (Brutal Strikes), player-global: every weapon, projectile or
    /// spell hit the local player deals has the summed chance to be critical, and a critical hit deals
    /// x(1.5 + the summed crit damage). Decided on the attacker's client while the hit is built, so the raised numbers
    /// travel in the HitData to the target's owner; the "Critical!" text is drawn on the attacker's screen only. Runs
    /// before the other outgoing-hit effects (high priority), so Blood Drinker's leech and Thor's Chain measure the
    /// critical damage.
    /// </summary>
    internal static class CriticalHits
    {
        private const float BaseMultiplier = 1.5f;

        public static void Roll(HitData hit)
        {
            AggregateValues v = AggregateHost.Current;
            float chance = v[EffectKind.CritChance];
            if (chance <= 0f || Random.value >= chance)
            {
                return;
            }
            hit.m_damage.Modify(BaseMultiplier + Mathf.Max(0f, v[EffectKind.CritDamage]));
            ShowText(hit.m_point);
        }

        // DamageText.ShowText would broadcast to every peer; the attacker's own in-world text is drawn directly.
        private static void ShowText(Vector3 point)
        {
            DamageText? texts = DamageText.instance;
            Camera? camera = Utils.GetMainCamera();
            if (texts == null || camera == null || Hud.IsUserHidden())
            {
                return;
            }
            float distance = Vector3.Distance(camera.transform.position, point);
            if (distance <= texts.m_maxTextDistance)
            {
                texts.AddInworldText(DamageText.TextType.Bonus, point, distance, Words.Localize("$ecf_fx_critical"), false);
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class CriticalHitPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(Character __instance, HitData hit)
        {
            Player? player = LocalHits.Attacker(hit);
            if (player != null && !ReferenceEquals(__instance, player) && !__instance.IsDead())
            {
                CriticalHits.Roll(hit);
            }
        }
    }
}
