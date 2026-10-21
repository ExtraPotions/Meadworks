// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The "Ringing ears" status effect a shriek leaves on a player, made at runtime as a plain game StatusEffect so the
    /// HUD shows it as it shows any other: an icon (the game's own bell), its name, a countdown, and a line as it
    /// starts. It is handed to the player's status effects as an object rather than by name, so it needs no place in
    /// the game's object database, and the game never writes status effects into the character file, so a character
    /// saved mid-ringing loads cleanly with or without the mod. It is the deafness's one clock: a second shriek while it
    /// lasts only extends it, to whichever end is later, and never stacks a second copy.
    /// </summary>
    internal static class ShriekStatus
    {
        /// <summary>Its object name, from which the game takes its identity hash.</summary>
        private const string Key = "ecr_ringing_ears";

        /// <summary>Item icons to borrow, in order: a bell for ringing ears, then items every game has.</summary>
        private static readonly string[] IconItems = { "Bell", "BellFragment", "Wishbone" };

        private static readonly int Hash = Key.GetStableHashCode();

        private static StatusEffect? _template;

        /// <summary>The ringing on <paramref name="player"/> now, or null when they hear as normal.</summary>
        public static StatusEffect? On(Player player) => player.GetSEMan().GetStatusEffect(Hash);

        /// <summary>
        /// Rings <paramref name="player"/>'s ears for <paramref name="seconds"/> from now, or keeps them ringing that long
        /// when it outlasts the ringing already there. Null only when the game refused the effect.
        /// </summary>
        public static StatusEffect? Give(Player player, float seconds)
        {
            StatusEffect? current = On(player);
            if (current != null)
            {
                current.m_ttl = Mathf.Max(current.m_ttl, current.GetDuration() + seconds);
                return current;
            }
            StatusEffect template = Template();
            template.m_ttl = seconds; // the game adds a copy, which keeps this
            return player.GetSEMan().AddStatusEffect(template);
        }

        private static StatusEffect Template()
        {
            if (_template == null)
            {
                _template = ScriptableObject.CreateInstance<StatusEffect>();
                _template.name = Key;
                _template.m_name = "Ringing ears";
                _template.m_tooltip = "A shriek left your ears ringing: the world is muffled and spells will not cast.";
                _template.m_startMessage = "Your ears ring - the world goes quiet";
                _template.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            if (_template.m_icon == null)
            {
                _template.m_icon = Icon(); // asked again until the object database can answer
            }
            return _template;
        }

        private static Sprite? Icon()
        {
            ObjectDB db = ObjectDB.instance;
            foreach (string name in IconItems)
            {
                GameObject? prefab = db != null ? db.GetItemPrefab(name) : null;
                ItemDrop? item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                Sprite[]? icons = item != null ? item.m_itemData.m_shared.m_icons : null;
                if (icons != null && icons.Length > 0 && icons[0] != null)
                {
                    return icons[0];
                }
            }
            return null;
        }
    }
}
