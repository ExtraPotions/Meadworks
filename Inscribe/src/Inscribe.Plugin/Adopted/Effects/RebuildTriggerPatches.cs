// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Equip, unequip and hiding or showing the hand items (swimming, eating) all end in
    /// <c>Humanoid.SetupEquipment</c>: mark the aggregate dirty when it is the local player's. Runs on every peer for
    /// every humanoid; the filter keeps it to the local player's own client.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupEquipment))]
    internal static class EquipmentChangedPatch
    {
        private static void Postfix(Humanoid __instance)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
            {
                EffectRuntime.MarkDirty();
            }
        }
    }

    /// <summary>
    /// First spawn and respawn after death: the new Player has no status effects (death removed them all), so the
    /// aggregate is rebuilt and re-added. Local player only.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class PlayerSpawnedPatch
    {
        private static void Postfix(Player __instance)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
            {
                EffectRuntime.MarkDirty();
            }
        }
    }
}
