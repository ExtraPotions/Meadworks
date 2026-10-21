// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The status effect a player wears while standing in a trail's patch - "Slick ice" or "Deep mud" - made at runtime
    /// as a plain game <c>SE_Stats</c> so the HUD shows it as it shows any other: an icon borrowed from the game, its
    /// name, and a tooltip that, besides its own words, lists the movement it takes away. The slow is the effect's own
    /// speed modifier, so the game applies it wherever it applies any status effect's. It has no timer while the player
    /// stands in a patch, and a short one (the kind's linger) once they step out, after which the game removes it.
    /// Handed to the player's status effects as an object, never by name, so it needs no place in the object database,
    /// and the game never writes status effects into the character file, so a character saved in mud loads cleanly with
    /// or without the mod.
    /// </summary>
    internal static class TrailStatus
    {
        private static readonly SE_Stats?[] Templates = new SE_Stats?[TrailKind.All.Length];

        /// <summary>The kind's status on <paramref name="player"/> now, or null.</summary>
        public static StatusEffect? On(Player player, TrailKind kind) =>
            player.GetSEMan().GetStatusEffect(kind.StatusHash);

        /// <summary>
        /// Keeps <paramref name="player"/> in the kind's status, with no timer, taking <paramref name="slow"/> of their
        /// speed (0 to 1). Null only when the game refused the effect.
        /// </summary>
        public static SE_Stats? Hold(Player player, TrailKind kind, float slow)
        {
            StatusEffect? current = On(player, kind);
            SE_Stats? status = current != null
                ? current as SE_Stats
                : player.GetSEMan().AddStatusEffect(Template(kind)) as SE_Stats;
            if (status != null)
            {
                status.m_speedModifier = -slow;
                status.m_ttl = 0f; // endless while they stand in it
            }
            return status;
        }

        /// <summary>
        /// The player stepped out (or off the ground): the status stays the kind's linger longer, then goes.
        /// </summary>
        public static void Release(Player player, TrailKind kind)
        {
            StatusEffect? status = On(player, kind);
            if (status != null && status.m_ttl <= 0f)
            {
                status.m_ttl = status.GetDuration() + kind.Linger;
            }
        }

        private static SE_Stats Template(TrailKind kind)
        {
            SE_Stats? template = Templates[kind.Index];
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<SE_Stats>();
                template.name = kind.StatusKey;
                template.m_name = kind.StatusName;
                template.m_tooltip = kind.Tooltip;
                template.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Templates[kind.Index] = template;
            }
            if (template.m_icon == null)
            {
                // asked again until the object database can answer
                template.m_icon = EffectIcon(kind) ?? ItemIcon(kind);
            }
            return template;
        }

        private static Sprite? EffectIcon(TrailKind kind)
        {
            ObjectDB db = ObjectDB.instance;
            foreach (string name in kind.IconEffects)
            {
                StatusEffect? effect = db != null ? db.GetStatusEffect(name.GetStableHashCode()) : null;
                if (effect != null && effect.m_icon != null)
                {
                    return effect.m_icon;
                }
            }
            return null;
        }

        private static Sprite? ItemIcon(TrailKind kind)
        {
            ObjectDB db = ObjectDB.instance;
            foreach (string name in kind.IconItems)
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
