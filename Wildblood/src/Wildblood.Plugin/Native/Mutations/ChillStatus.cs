// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The "Chilled" status a Frostbound creature's aura puts on a player, made at runtime as a <see cref="ChillEffect"/>
    /// so the HUD shows it as it shows any other: an icon (the game's own frost one), its name, and the stamina line in
    /// its tooltip. It is handed to the player's status effects as an object rather than by name, so it needs no place in
    /// the game's object database, and the game never writes status effects into the character file, so a character saved
    /// while chilled loads cleanly with or without the mod. Lives on this machine's own player only.
    /// </summary>
    internal static class ChillStatus
    {
        /// <summary>Its object name, from which the game takes its identity hash.</summary>
        private const string Key = "ecr_chilled";

        /// <summary>Seconds it lingers after the last aura check in reach: a few checks, so leaving drops it quickly.</summary>
        private const float Linger = 1f;

        /// <summary>Item icons to borrow when the game's own frost and cold effects have none.</summary>
        private static readonly string[] IconItems = { "FreezeGland", "FrostCore", "Wishbone" };

        private static readonly int Hash = Key.GetStableHashCode();

        private static ChillEffect? _template;

        /// <summary>Chills <paramref name="player"/> by <paramref name="cut"/> percent stamina regeneration for <see cref="Linger"/> seconds; each aura check renews it.</summary>
        public static void Hold(Player player, float cut)
        {
            SEMan seman = player.GetSEMan();
            ChillEffect? live = seman.GetStatusEffect(Hash) as ChillEffect;
            if (live == null)
            {
                live = seman.AddStatusEffect(Template()) as ChillEffect; // the game adds a copy and hands it back
            }
            live?.Hold(cut, Linger);
        }

        private static ChillEffect Template()
        {
            if (_template == null)
            {
                _template = ScriptableObject.CreateInstance<ChillEffect>();
                _template.name = Key;
                _template.m_name = "Chilled";
                _template.m_tooltip = "A Frostbound creature's freezing aura: your stamina recovers slowly while you stay near it.";
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
            if (db == null)
            {
                return null;
            }
            Sprite? own = EffectIcon(db, SEMan.s_statusEffectFrost) ?? EffectIcon(db, SEMan.s_statusEffectCold);
            return own != null ? own : ItemIcon(db);
        }

        private static Sprite? EffectIcon(ObjectDB db, int hash)
        {
            StatusEffect? effect = db.GetStatusEffect(hash);
            return effect != null && effect.m_icon != null ? effect.m_icon : null;
        }

        private static Sprite? ItemIcon(ObjectDB db)
        {
            foreach (string name in IconItems)
            {
                GameObject? prefab = db.GetItemPrefab(name);
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
