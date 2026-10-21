// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Stones;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// The item endpoints (api.md section 4): reads of an item's state (rarity, colour, inscriptions, the decorated
    /// name, whether it may become magic, the rune a salvage may give back) and the two writes, a fresh roll and a
    /// cleanse, through the one item-state writer with the reason <c>api</c>. The writes act on the item object the
    /// caller hands in, on the caller's peer, like a rune: call them where the item is owned (the player's own
    /// inventory). A sealed item, or one written by a newer Inscribe, is never changed.
    /// </summary>
    internal static class ApiItems
    {
        public static bool IsMagic(ItemDrop.ItemData? item) => item != null && ItemState.Read(item).IsMagic;

        /// <summary>The rarity id (the base rarity's, <c>normal</c>, for a plain item; an unknown stored id as stored).</summary>
        public static string? Rarity(ItemDrop.ItemData? item) =>
            item == null ? null : ItemState.Read(item).RarityId ?? ActiveRules.Current.Economy.BaseRarity?.Id ?? "normal";

        /// <summary>The rarity's <c>#RRGGBB</c>; white for an unknown rarity.</summary>
        public static string? RarityColor(ItemDrop.ItemData? item)
        {
            if (item == null)
            {
                return null;
            }
            ItemState state = ItemState.Read(item);
            RarityDef? rarity = state.IsMagic ? state.Rarity : ActiveRules.Current.Economy.BaseRarity;
            return rarity?.Color ?? "#FFFFFF";
        }

        /// <summary><c>[{"id":..,"tier":..,"value":..,"affix":..,"active":..}]</c>; tier as shown (1 strongest; 0 for an orphan).</summary>
        public static string? InscriptionsJson(ItemDrop.ItemData? item)
        {
            if (item == null)
            {
                return null;
            }
            ItemState state = ItemState.Read(item);
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < state.AffixCount; i++)
            {
                AffixRoll roll = state.Affixes[i];
                AffixDef? def = state.DefinitionAt(i);
                sb.Append(i > 0 ? ",{" : "{").Append("\"id\":").Append(Quote(roll.Id))
                    .Append(",\"tier\":").Append(Numbers.Format(def != null ? def.ShownTier(roll.Tier) : 0))
                    .Append(",\"value\":").Append(Numbers.Format(roll.Value))
                    .Append(",\"affix\":").Append(def != null ? Quote(EnumIds<AffixKind>.Id(def.Kind)) : "null")
                    .Append(",\"active\":").Append(state.IsActiveAt(i) ? "true" : "false").Append('}');
            }
            return sb.Append(']').ToString();
        }

        /// <summary>The localized name as the grid tooltip titles it: in the rarity colour when the player's display shows one.</summary>
        public static string? DecoratedName(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
            {
                return null;
            }
            return Words.Localize(DisplayCache.Topic(ItemState.Read(item), item) ?? item.m_shared.m_name);
        }

        public static bool CanBeMagic(ItemDrop.ItemData? item) => ItemClasses.IsMagicBase(item);

        public static bool RollMagic(ItemDrop.ItemData? item, string? rarityId)
        {
            RarityDef? rarity = ActiveRules.Current.Rarity(rarityId);
            if (item == null || rarity == null || rarity.IsBase || !ItemClasses.IsMagicBase(item) || !Changeable(ItemState.Read(item)))
            {
                return false;
            }
            RollOutcome outcome = ItemRoller.RollFresh(ItemState.Read(item), rarity, RollContext.For(item));
            return outcome.Success && Write(item, outcome.State!);
        }

        public static bool Cleanse(ItemDrop.ItemData? item)
        {
            ItemState state = ItemState.Read(item);
            if (item == null || !Changeable(state))
            {
                return false;
            }
            if (!state.IsMagic && !state.HasAffixes)
            {
                return true;
            }
            return Write(item, state.ToBuilder().ClearAffixes().SetRarity(null).Build());
        }

        /// <summary>The rune prefab a salvage of the item may give back, or null (<see cref="SalvageRunes"/>).</summary>
        public static string? SalvageRune(ItemDrop.ItemData? item) => SalvageRunes.PrefabFor(item);

        public static bool DecorateIcon(UnityEngine.GameObject? icon, ItemDrop.ItemData? item)
        {
            UnityEngine.UI.Image? image = icon != null ? icon.GetComponent<UnityEngine.UI.Image>() : null;
            if (image == null)
            {
                return false;
            }
            Display.Backdrops.IconBackdrop.Set(image, item);
            return true;
        }

        public static float SalvageRuneChance(ItemDrop.ItemData? item) => SalvageRunes.ChanceFor(item);

        private static bool Changeable(ItemState state) => !state.IsSealed && !state.IsNewerFormat;

        private static bool Write(ItemDrop.ItemData item, ItemState state)
        {
            using (ItemChanges.Because(ItemChanges.Api))
            {
                return ItemState.Write(item, state);
            }
        }

        internal static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
