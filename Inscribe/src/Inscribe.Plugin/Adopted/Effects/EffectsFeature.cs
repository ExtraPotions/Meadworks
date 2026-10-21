// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Entry point of the Effects area, called once from plugin Awake after the rules, settings and words are loaded.
    /// Owns: the ECF_Aggregate status effect (<see cref="EcfAggregate"/>, <see cref="AggregateHost"/>), its rebuild
    /// triggers (<see cref="EffectRuntime"/>), the channel totals and caps (<see cref="AggregateBuilder"/>), the
    /// item-local hooks (<see cref="ItemLocalCache"/> and the patches in this folder), the max-pool postfix and the
    /// field writes, and the read side for commands (<see cref="EffectTotals"/>).
    /// Harmony patches of this area are ordinary [HarmonyPatch] classes in this folder; the plugin's PatchAll finds them.
    /// <para>
    /// Multiplayer: player-global effects exist only on the local player's own client (the machine that runs that
    /// player's stats); item-local effects are computed by whichever peer asks, from the item's replicated custom data
    /// and the server-synced rules, so every peer computes the same numbers. On a dedicated server there is no local
    /// player: no aggregate, and the item hooks return on items without state.
    /// </para>
    /// <para>
    /// Phase 2 sends three things, each scoped: the hits the local player builds carry their changes to the target's
    /// owner (the game's own damage RPC, <see cref="OutgoingHits"/>); a creature's owner sends one routed RPC to the
    /// killer's peer on a kill, only when the killer has Reaper or Soul Reaper (<see cref="KillCredit"/>; Evader's Fury's
    /// <see cref="MeleeDodge"/> likewise); and the player's own client publishes the few totals other
    /// peers apply - stagger length, taming, sailing, gathering, light, mist, and which of those RPCs it wants - on its
    /// player ZDO when they change (<see cref="PlayerStats"/>). Summons carry their staff's bonus in their own ZDO (<see cref="Summons"/>).
    /// </para>
    /// </summary>
    public static class EffectsFeature
    {
        public static void Init()
        {
            EffectKinds.VerifyCatalog();
            EffectRuntime.Install();
        }
    }
}
