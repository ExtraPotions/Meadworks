// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/RockCatalog.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Which prefabs are rocks, and what kind, cached per prefab name (so a lookup per hit is a dictionary read). Runs on
    /// every machine and needs nothing but the prefab's own components, so every machine agrees.
    /// <list type="bullet">
    /// <item><b>Rock:</b> a MineRock5, MineRock or Destructible whose damage modifiers make it immune (or Ignore) to chop
    /// and blunt, but not to pickaxe damage. Discovered, not listed: modded rocks count; dungeon gates, iron walls and
    /// bar stacks (MineRocks without such modifiers), bushes and trees do not. Not-rocks are cached too.</item>
    /// <item><b>Ore:</b> its drop table holds an item not in "Plain Stone Items" (items of weight 0 count only when
    /// every item has weight 0, as the game then drops the first). A Destructible's table is its DropOnDestroyed, or,
    /// for an intact deposit, the table of the rock it turns into.</item>
    /// <item><b>Name:</b> the game's (m_name, a Destructible's HoverText or its fractured form's); a rock without one goes
    /// by its first ore item's name (<see cref="FallbackName"/>), so every callout, the Echo and discoveries can name
    /// it.</item>
    /// </list>
    /// "Plain Stone Items" is synced and hot reloaded: the cache is rebuilt whenever its value differs from the one the
    /// cache was built with.
    /// </summary>
    public static class RockCatalog
    {
        private const string FracSuffix = "_frac";

        /// <summary>What Utils.GetPrefabName reads from a live MineRock5, whatever its prefab (<see cref="PrefabName"/>).</summary>
        private const string RenamedChunks = "___MineRock5";

        private static readonly Dictionary<string, RockInfo> Cache = new Dictionary<string, RockInfo>();
        private static HashSet<string> plainStone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string cachedFor;

        /// <summary>The rock kind of a MineRock5, MineRock or Destructible; null for anything that is not a rock.</summary>
        public static RockInfo Info(Component target)
        {
            if (target == null || !(target is MineRock5 || target is MineRock || target is Destructible))
                return null;
            Refresh();
            string prefab = PrefabName(target);
            if (prefab == null)
                return null;
            if (!Cache.TryGetValue(prefab, out RockInfo info))
            {
                info = Classify(target, prefab);
                Cache[prefab] = info;
            }
            return info;
        }

        /// <summary>
        /// A rock's prefab name, from its ZDO's prefab hash when it has one: a live MineRock5 renames its own object in
        /// Awake (it names the MeshFilter it adds, and a component's name is its object's), so every one reads
        /// "___MineRock5 m_meshFilter" and would share one cache entry, the first one classified. Else the object's own
        /// name (a prefab, an object without a ZDO); null for a renamed MineRock5 without a ZDO, which cannot tell.
        /// </summary>
        private static string PrefabName(Component target)
        {
            ZNetView view = target.GetComponent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            GameObject prefab = zdo != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
            if (prefab != null)
                return prefab.name;
            string name = Utils.GetPrefabName(target.gameObject);
            return name == RenamedChunks ? null : name;
        }

        /// <summary>Whether an item prefab name is in "Plain Stone Items".</summary>
        public static bool IsPlainStone(string itemPrefab)
        {
            Refresh();
            return itemPrefab != null && plainStone.Contains(itemPrefab);
        }

        /// <summary>Whether a drop table holds anything but plain stone.</summary>
        public static bool HasOre(DropTable table) => FirstOre(table) != null;

        /// <summary>
        /// The first item of a drop table that is not plain stone; null when there is none. Items of weight 0 count only
        /// when every item has weight 0, as the game then drops the first.
        /// </summary>
        public static GameObject FirstOre(DropTable table)
        {
            if (table == null || table.m_drops == null)
                return null;
            bool weighted = table.m_drops.Exists(drop => drop.m_weight > 0f);
            foreach (DropTable.DropData drop in table.m_drops)
            {
                if (drop.m_item != null && (drop.m_weight > 0f || !weighted) && !IsPlainStone(drop.m_item.name))
                    return drop.m_item;
            }
            return null;
        }

        /// <summary>
        /// The name a rock without one of its own goes by: its first ore item's name token ("$item_ice"), else
        /// <paramref name="prefab"/>. Some deposits have no name (the ice rocks, Leviathan, the flametal rockstand).
        /// </summary>
        public static string FallbackName(DropTable drops, string prefab)
        {
            GameObject ore = FirstOre(drops);
            ItemDrop item = ore != null ? ore.GetComponent<ItemDrop>() : null;
            string token = item != null ? item.m_itemData.m_shared.m_name : null;
            return string.IsNullOrEmpty(token) ? prefab : token;
        }

        /// <summary>A prefab name without "_frac": the kind of rock.</summary>
        public static string KindOf(string prefab)
        {
            if (string.IsNullOrEmpty(prefab) || !prefab.EndsWith(FracSuffix, StringComparison.OrdinalIgnoreCase))
                return prefab ?? "";
            return prefab.Substring(0, prefab.Length - FracSuffix.Length);
        }

        /// <summary>Immune (or Ignore) to chop and blunt, not to pickaxe: the damage modifiers of a rock.</summary>
        public static bool IsRockModifiers(HitData.DamageModifiers modifiers) =>
            Blocks(modifiers.m_chop) && Blocks(modifiers.m_blunt) && !Blocks(modifiers.m_pickaxe);

        private static bool Blocks(HitData.DamageModifier modifier) =>
            modifier == HitData.DamageModifier.Immune || modifier == HitData.DamageModifier.Ignore;

        private static void Refresh()
        {
            string value = PickaxeSettings.PlainStoneItems.Value ?? "";
            if (value == cachedFor)
                return;
            Cache.Clear();
            plainStone = PickaxeSettings.ParsePlainStone(value);
            cachedFor = value;
        }

        private static RockInfo Classify(Component target, string prefab)
        {
            switch (target)
            {
                case MineRock5 chunks:
                    return IsRockModifiers(chunks.m_damageModifiers) ? Chunked(target, prefab, chunks.m_name, chunks.m_dropItems, chunks.m_minToolTier, chunks.m_health) : null;
                case MineRock chunks:
                    return IsRockModifiers(chunks.m_damageModifiers) ? Chunked(target, prefab, chunks.m_name, chunks.m_dropItems, chunks.m_minToolTier, chunks.m_health) : null;
                case Destructible piece:
                    return IsRockModifiers(piece.m_damages) ? RockPieces.Classify(piece, prefab) : null;
                default:
                    return null;
            }
        }

        private static RockInfo Chunked(Component target, string prefab, string name, DropTable drops, int tier, float health) => new RockInfo
        {
            Prefab = prefab,
            Kind = KindOf(prefab),
            Name = string.IsNullOrEmpty(name) ? FallbackName(drops, prefab) : name,
            IsOre = HasOre(drops),
            Tier = tier,
            Health = health,
            HasChunks = true,
            HasBeacon = target.GetComponentInChildren<Beacon>(true) != null,
        };
    }
}
