// SPDX-License-Identifier: GPL-3.0-or-later
// Loaded crossbow continuity adapted from ECR 51abb440b89313d2130519116b83f9c65a7c11de
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Changed by expDARE, 2026: owner-scoped weak memory, native checks and existing Wildblood lifecycle.
using System.Runtime.CompilerServices;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    internal static class LoadedWeapons
    {
        static ConditionalWeakTable<Player, ConditionalWeakTable<ItemDrop.ItemData, object>> kept = new();
        static readonly object Mark = new object();
        static bool hasMarks;

        internal static void Reset()
        {
            if (!hasMarks) return;
            kept = new();
            hasMarks = false;
        }

        internal static void Tick()
        {
            if (!LoadedWeaponAvailability.Active) Reset();
        }

        static bool Owns(Player player, ItemDrop.ItemData item) => player.m_nview != null
            && player.m_nview.IsValid() && player.m_nview.IsOwner()
            && item.m_shared.m_attack.m_requiresReload && player.GetInventory().ContainsItem(item);

        internal static void Unequipping(Humanoid humanoid, ItemDrop.ItemData item)
        {
            if (!LoadedWeaponAvailability.Active) { Reset(); return; }
            if (!(humanoid is Player player) || item == null) return;
            // Forget an earlier failed unequip too: a shot must never resurrect its previous mark.
            if (kept.TryGetValue(player, out var previous)) previous.Remove(item);
            if (!Owns(player, item) || !ReferenceEquals(player.m_weaponLoaded, item)) return;
            kept.GetOrCreateValue(player).Add(item, Mark);
            hasMarks = true;
        }

        internal static void Equipped(Humanoid humanoid, ItemDrop.ItemData item)
        {
            if (!LoadedWeaponAvailability.Active) { Reset(); return; }
            if (!(humanoid is Player player) || item == null || !kept.TryGetValue(player, out var weapons)
                || !weapons.TryGetValue(item, out _)) return;
            weapons.Remove(item);
            if (Owns(player, item) && player.IsItemEquiped(item)) player.SetWeaponLoaded(item);
        }
    }
}
