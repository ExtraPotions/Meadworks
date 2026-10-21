// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Adrenaline (the trinket mechanic) and Shield Mend, on the local player's own client: the game keeps a player's
    /// adrenaline and status effects on the machine that owns that player (another peer's stagger bonus reaches it as
    /// the game's own RPC_AddAdrenaline), and resolves a player's blocks inside that player's RPC_Damage. Nothing is sent.
    /// <list type="bullet">
    /// <item><c>trinket_duration</c>: the status effect a worn trinket grants when the adrenaline bar fills lasts X% longer
    /// (<see cref="TrinketDuration"/>).</item>
    /// <item>Blood Up (<c>adrenaline_gain</c>): what a block or a parry gives is X% more (anything gained while the
    /// block resolves), before the game's own rate and curve.</item>
    /// <item>Shield Mend (<c>block_restore</c>): a block or parry against an attacker restores X of the resource, when
    /// the block held (stamina left, guard not broken). Parries also get Seidr Riposte's restore.</item>
    /// </list>
    /// </summary>
    internal static class AdrenalineHooks
    {
        /// <summary>The local player's BlockAttack is running.</summary>
        public static bool Blocking;

        public static void OnBlockEnd(Humanoid blocker, Character attacker, bool blocked)
        {
            Blocking = false;
            if (!blocked || attacker == null || !ReferenceEquals(blocker, Player.m_localPlayer))
            {
                return;
            }
            // The game blocks nothing when the stamina runs out or the guard breaks (a stagger sets the bar full).
            if (blocker.HaveStamina() && blocker.GetStaggerPercentage() < 1f)
            {
                Restore(blocker, AggregateHost.Current.BlockRestore);
            }
        }

        private static void Restore(Humanoid player, float[] amounts)
        {
            if (amounts[AggregateValues.Health] > 0f)
            {
                player.Heal(amounts[AggregateValues.Health]);
            }
            if (amounts[AggregateValues.Stamina] > 0f)
            {
                player.AddStamina(amounts[AggregateValues.Stamina]);
            }
            if (amounts[AggregateValues.Eitr] > 0f)
            {
                player.AddEitr(amounts[AggregateValues.Eitr]);
            }
        }
    }

    /// <summary>
    /// <c>trinket_duration</c>. When the local player's adrenaline bar fills, Player.AddAdrenaline adds the
    /// <c>m_fullAdrenalineSE</c> of every equipped item that has one through SEMan.AddStatusEffect, which returns the
    /// player's own clone: that clone gets the item's time Ã— (1 + X). Only a status effect passed in as an equipped
    /// item's own <c>m_fullAdrenalineSE</c> counts; the adrenaline level effects added in the same call come from
    /// ObjectDB and are left alone. A trinket effect still running when the bar fills again is restarted by the game
    /// (ResetTime) and keeps the longer time it was given.
    /// </summary>
    internal static class TrinketDuration
    {
        private static int _depth;

        public static void Begin() => _depth++;

        public static void End() => _depth = _depth > 0 ? _depth - 1 : 0;

        public static void OnAdded(SEMan seman, StatusEffect source, StatusEffect? added)
        {
            if (_depth == 0 || added == null || source.m_ttl <= 0f || !(seman.m_character is Player player)
                || !ReferenceEquals(player, Player.m_localPlayer))
            {
                return;
            }
            float longer = AggregateHost.Current[EffectKind.TrinketDuration];
            if (longer > 0f && IsTrinketEffect(player, source))
            {
                added.m_ttl = source.m_ttl * (1f + longer);
            }
        }

        // The same items the game walks when the bar fills: everything equipped in the inventory.
        private static bool IsTrinketEffect(Player player, StatusEffect source)
        {
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item.m_equipped && ReferenceEquals(item.m_shared.m_fullAdrenalineSE, source))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
    internal static class BlockRestorePatch
    {
        private static void Prefix(Humanoid __instance) => AdrenalineHooks.Blocking = ReferenceEquals(__instance, Player.m_localPlayer);

        private static void Postfix(Humanoid __instance, Character attacker, bool __result) =>
            AdrenalineHooks.OnBlockEnd(__instance, attacker, __result);
    }

    /// <summary>
    /// The local player's Player.AddAdrenaline: Blood Up scales a gain made while a block resolves, and the call is
    /// bracketed (a depth, since a trinket effect's up-front adrenaline calls it again from inside) for
    /// <see cref="TrinketDuration"/>.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.AddAdrenaline))]
    internal static class AddAdrenalinePatch
    {
        private static void Prefix(Player __instance, ref float v, out bool __state)
        {
            __state = ReferenceEquals(__instance, Player.m_localPlayer);
            if (!__state)
            {
                return;
            }
            TrinketDuration.Begin();
            if (v > 0f && AdrenalineHooks.Blocking)
            {
                v *= 1f + AggregateHost.Current[EffectKind.AdrenalineGain];
            }
        }

        private static void Postfix(bool __state)
        {
            if (__state)
            {
                TrinketDuration.End();
            }
        }
    }

    /// <summary>The clone SEMan made for a status effect added to the local player while its adrenaline changes.</summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect),
        new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) })]
    internal static class TrinketEffectPatch
    {
        private static void Postfix(SEMan __instance, StatusEffect statusEffect, StatusEffect? __result) =>
            TrinketDuration.OnAdded(__instance, statusEffect, __result);
    }
}
