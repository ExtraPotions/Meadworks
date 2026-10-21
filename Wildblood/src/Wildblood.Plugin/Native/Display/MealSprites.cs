// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// The icon a devoured creature is drawn with on its devourer's nameplate: the trophy it drops - the first trophy in
    /// its own CharacterDrop, the game's own item icon - or, for a creature that drops none (a greyling, a hen, a modded
    /// creature) or one this game no longer knows, the game's horned monster head, the map's boss pin sprite. Nothing is
    /// shipped: both come from the game at runtime. The trophy for each kind of creature is looked up once per world.
    /// </summary>
    internal static class MealSprites
    {
        private static readonly Dictionary<int, Sprite?> Trophies = new Dictionary<int, Sprite?>();
        private static ZNetScene? _scene;

        /// <summary>One sprite per meal, in the order eaten; null only when not even the monster head can be found.</summary>
        public static List<Sprite?> For(List<int> meals)
        {
            List<Sprite?> sprites = new List<Sprite?>(meals.Count);
            Sprite? head = MonsterHead();
            foreach (int prefabHash in meals)
            {
                Sprite? trophy = Trophy(prefabHash);
                sprites.Add(trophy != null ? trophy : head);
            }
            return sprites;
        }

        private static Sprite? Trophy(int prefabHash)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
            {
                return null;
            }
            if (scene != _scene)
            {
                Trophies.Clear(); // a new world: remember nothing from the last one
                _scene = scene;
            }
            if (!Trophies.TryGetValue(prefabHash, out Sprite? trophy))
            {
                GameObject prefab = scene.GetPrefab(prefabHash);
                trophy = prefab != null ? TrophyOf(prefab.GetComponent<CharacterDrop>()) : null;
                Trophies[prefabHash] = trophy;
            }
            return trophy;
        }

        private static Sprite? TrophyOf(CharacterDrop? drops)
        {
            if (drops == null || drops.m_drops == null)
            {
                return null;
            }
            foreach (CharacterDrop.Drop drop in drops.m_drops)
            {
                ItemDrop? item = drop != null && drop.m_prefab != null ? drop.m_prefab.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData.SharedData? shared = item != null ? item.m_itemData?.m_shared : null;
                if (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy
                    && shared.m_icons != null && shared.m_icons.Length > 0 && shared.m_icons[0] != null)
                {
                    return shared.m_icons[0];
                }
            }
            return null;
        }

        // The horned head the map marks a boss's lair with: the game's own "a monster" picture, used for any creature
        // that drops no trophy. The minimap exists on every client, which is the only place a nameplate is drawn.
        private static Sprite? MonsterHead()
        {
            Minimap map = Minimap.instance;
            if (map == null || map.m_icons == null)
            {
                return null;
            }
            foreach (Minimap.SpriteData data in map.m_icons)
            {
                if (data.m_name == Minimap.PinType.Boss)
                {
                    return data.m_icon;
                }
            }
            return null;
        }
    }
}
