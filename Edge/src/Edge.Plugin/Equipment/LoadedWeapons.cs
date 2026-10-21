// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-or-later
// Portions from EliteCreaturesReborn 3.21.0 (Gerald Glasgow), GPL-3.0-or-later, at 9da28eb58adbe065be1e4baef779a448acd38e0f:
// EliteCreaturesReborn/EliteCreaturesReborn/Runtime/LoadedWeapons.cs. Source notice retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, the Keep Crossbow Loaded setting and Edge's product gate.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System.Runtime.CompilerServices;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Runtime.Equipment;

/// <summary>
/// A loaded crossbow stays loaded while it is put away. The game forgets the loaded weapon whenever one is unequipped
/// (a swap, a swim, sitting down) and reloads on the next draw. This remembers each weapon that was loaded when this
/// machine's player put it away, and hands the loaded state back when the same weapon is drawn again; firing still
/// empties it as before. Only the player's owner holds the state and it writes the game's own loaded flag (which the
/// game replicates), so it works the same on a dedicated server. Memory only: weakly held, so a dropped or destroyed
/// weapon is simply forgotten, and a relog reloads as before.
/// </summary>
internal static class LoadedWeapons
{
    private static readonly ConditionalWeakTable<ItemDrop.ItemData, object> Kept = new();
    private static readonly object Mark = new();

    /// <summary>Edge setting <c>5 - Equipment / Keep Crossbow Loaded</c>, the whole-product switch and the session gate.</summary>
    internal static bool Enabled => Plugin.Ready && Plugin.Settings != null
        && Plugin.Settings.Get(EdgeSettings.General.Enabled) && Plugin.Settings.Get(EdgeSettings.Equipment.KeepCrossbowLoaded)
        && EdgeModules.ProductActive(out _);

    /// <summary>Before the game unequips <paramref name="item"/>: keep it if it is this player's loaded weapon.</summary>
    internal static void Unequipping(Humanoid humanoid, ItemDrop.ItemData item)
    {
        if (humanoid is Player player && Enabled && IsOwnLoaded(player, item))
        {
            Kept.Remove(item);
            Kept.Add(item, Mark);
        }
    }

    /// <summary>After the game equipped <paramref name="item"/>: if it was put away loaded, it is loaded again.</summary>
    internal static void Equipped(Humanoid humanoid, ItemDrop.ItemData item)
    {
        if (humanoid is not Player player || !Kept.TryGetValue(item, out _))
        {
            return;
        }
        Kept.Remove(item);
        if (Enabled && player.m_nview.IsOwner() && player.IsItemEquiped(item))
        {
            player.SetWeaponLoaded(item);
        }
    }

    private static bool IsOwnLoaded(Player player, ItemDrop.ItemData item) =>
        item.m_shared.m_attack.m_requiresReload && ReferenceEquals(player.m_weaponLoaded, item)
        && player.m_nview.IsValid() && player.m_nview.IsOwner();
}
