// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>One active affix on an equipped item: the item, its roll, the live definition and the item's class.</summary>
    public readonly struct ActiveAffix
    {
        public ActiveAffix(ItemDrop.ItemData item, AffixRoll roll, AffixDef def, ItemClass? itemClass)
        {
            Item = item;
            Roll = roll;
            Def = def;
            Class = itemClass;
        }

        public ItemDrop.ItemData Item { get; }
        public AffixRoll Roll { get; }
        public AffixDef Def { get; }

        /// <summary>The item's class (classes-and-tiers.md section 1); null when it has none any more.</summary>
        public ItemClass? Class { get; }

        /// <summary>Shortcut to <see cref="AffixDef.ChannelIndex"/>.</summary>
        public int Channel => Def.ChannelIndex;
    }

    /// <summary>
    /// Enumerates what counts for effects (effects-runtime.md section 2): only equipped items (weapons and tools in
    /// hand, shield, armor, cape, utility item, and the trinket since trinkets became an item class that rolls;
    /// hidden hand items never; plus the items an API equipment provider names), only active affixes (defined and
    /// enabled; dormant and unreadable ones are skipped), only when the <c>Affix effects</c> switch is on. For event
    /// handlers (equip change, inventory change), never per frame: it walks the equipment slots and reads each item's
    /// cached state, filling caller-owned lists so a rebuild allocates nothing.
    /// </summary>
    public static class ItemEffects
    {
        /// <summary>The <c>Affix effects</c> master switch (synced).</summary>
        public static bool Enabled => ModSettings.AffixEffects == null || ModSettings.AffixEffects.Value;

        /// <summary>
        /// Fills <paramref name="into"/> with the equipped items of <paramref name="humanoid"/> that count: the game's
        /// slots, then for a player what the API's equipment providers add (<see cref="EquipmentProviders"/>).
        /// </summary>
        public static void EquippedItems(Humanoid? humanoid, List<ItemDrop.ItemData> into)
        {
            into.Clear();
            if (humanoid == null)
            {
                return;
            }
            AddIfPresent(into, humanoid.m_rightItem);
            AddIfPresent(into, humanoid.m_leftItem);
            AddIfPresent(into, humanoid.m_helmetItem);
            AddIfPresent(into, humanoid.m_chestItem);
            AddIfPresent(into, humanoid.m_legItem);
            AddIfPresent(into, humanoid.m_shoulderItem);
            AddIfPresent(into, humanoid.m_utilityItem);
            AddIfPresent(into, humanoid.m_trinketItem);
            if (humanoid is Player player)
            {
                EquipmentProviders.AddTo(player, into);
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> with every active affix on the local player's counted items. Empty when there
        /// is no local player (dedicated server) or the master switch is off.
        /// </summary>
        public static void CollectLocal(List<ActiveAffix> into, List<ItemDrop.ItemData> scratch)
        {
            into.Clear();
            if (!Enabled || Player.m_localPlayer == null)
            {
                return;
            }
            EquippedItems(Player.m_localPlayer, scratch);
            foreach (ItemDrop.ItemData item in scratch)
            {
                CollectItem(item, into);
            }
        }

        /// <summary>
        /// Adds what counts on one item: its active affixes (<see cref="ItemState.EffectRolls"/>). For item-local hooks use
        /// <see cref="ItemState.Read"/>.
        /// </summary>
        public static void CollectItem(ItemDrop.ItemData item, List<ActiveAffix> into)
        {
            IReadOnlyList<EffectRoll> rolls = ItemState.Read(item).EffectRolls;
            if (rolls.Count == 0)
            {
                return;
            }
            ItemClass? itemClass = ItemClasses.ClassOf(item);
            for (int i = 0; i < rolls.Count; i++)
            {
                into.Add(new ActiveAffix(item, rolls[i].Roll, rolls[i].Def, itemClass));
            }
        }

        /// <summary>
        /// Whether the item is one the local player has equipped, or one an equipment provider names for it (for
        /// <see cref="ItemStateCache.Written"/>).
        /// </summary>
        public static bool IsEquippedByLocalPlayer(ItemDrop.ItemData item)
        {
            Player? player = Player.m_localPlayer;
            return player != null && (player.IsItemEquiped(item) || EquipmentProviders.Provides(player, item));
        }

        private static void AddIfPresent(List<ItemDrop.ItemData> into, ItemDrop.ItemData? item)
        {
            if (item != null && !into.Contains(item))
            {
                into.Add(item);
            }
        }
    }
}
