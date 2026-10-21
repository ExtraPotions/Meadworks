// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Stagger length, through the stagger animation's speed. A character is staggered while its animator plays the
    /// stagger state; the game resets the animation speed to 1 as soon as the character can move again, and the owner's
    /// animation speed is synced to every peer (ZSyncAnimation), so a faster or slower stagger looks the same everywhere.
    /// <list type="bullet">
    /// <item>Quick Recovery (local player, its own client): while staggered, the animation runs 1/(1-X) as fast, so the
    /// lockout is X% shorter.</item>
    /// <item>Dazing Blows (the creature's owner, any peer): a stagger caused by a player's hit plays 1/(1+X) as fast, so
    /// it lasts X% longer. The owner reads X from the attacker's player ZDO (<see cref="PlayerStats"/>), because it
    /// cannot see the attacker's gear. Up to 16 dazed creatures are tracked at once; the oldest is dropped.</item>
    /// </list>
    /// Both re-assert the speed every frame while the stagger plays (nothing else sets it then) and stop when it ends.
    /// </summary>
    internal static class StaggerSpeed
    {
        private static readonly Character?[] Dazed = new Character?[16];
        private static readonly float[] DazeSpeed = new float[16];
        private static readonly float[] DazeSince = new float[16];

        /// <summary>A new stagger shows in the animator a frame or two after the trigger; until then an entry waits.</summary>
        private const float Grace = 0.5f;
        private static int _next;

        /// <summary>Once per frame on every peer, with or without a local player (a dedicated server owns creatures).</summary>
        public static void Tick()
        {
            Player? player = Player.m_localPlayer;
            if (player != null && player.IsStaggering())
            {
                float faster = AggregateHost.Current[EffectKind.StaggerRecovery];
                if (faster > 0f)
                {
                    player.m_zanim.SetSpeed(1f / (1f - UnityEngine.Mathf.Min(faster, 0.9f)));
                }
            }
            for (int i = 0; i < Dazed.Length; i++)
            {
                TickDazed(i);
            }
        }

        private static void TickDazed(int i)
        {
            Character? creature = Dazed[i];
            if (creature == null)
            {
                return;
            }
            bool staggering = creature.IsStaggering();
            if (!creature.m_nview.IsValid() || !creature.m_nview.IsOwner() || creature.IsDead()
                || (!staggering && UnityEngine.Time.time - DazeSince[i] > Grace))
            {
                Dazed[i] = null;
                return;
            }
            if (staggering)
            {
                creature.m_zanim.SetSpeed(DazeSpeed[i]);
            }
        }

        /// <summary>On the creature's owner, after stagger damage staggered it.</summary>
        public static void OnStaggered(Character creature, HitData? hit)
        {
            if (hit == null || creature.IsPlayer())
            {
                return;
            }
            float longer = PlayerStats.OfAttacker(hit, PlayerStats.Daze);
            if (longer <= 0f)
            {
                return;
            }
            Dazed[_next] = creature;
            DazeSpeed[_next] = 1f / (1f + longer);
            DazeSince[_next] = UnityEngine.Time.time;
            _next = (_next + 1) % Dazed.Length;
        }
    }

    /// <summary>Dazing Blows: AddStaggerDamage returns true when the hit staggered the character (owner only).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.AddStaggerDamage))]
    internal static class DazePatch
    {
        private static void Postfix(Character __instance, HitData hit, bool __result)
        {
            if (__result)
            {
                StaggerSpeed.OnStaggered(__instance, hit);
            }
        }
    }
}
