// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// HUD icons for the affix states a player could not otherwise see: Evader's Fury and Steel Rhythm while their
    /// window runs (with the game's own countdown), the Runic Ward while charged. Each is a plain status effect added
    /// locally to the local player (never registered in ObjectDB, never sent); its icon is the equipped item that grants
    /// the affix, its name the English word <c>$ecf_fx_*</c>. Local player only.
    /// </summary>
    internal static class Indicators
    {
        public const int Fury = 0, Rhythm = 1, Ward = 2;

        private static readonly string[] Names = { "ECF_Indicator_Fury", "ECF_Indicator_Rhythm", "ECF_Indicator_Ward" };
        private static readonly string[] Titles = { "$ecf_fx_fury", "$ecf_fx_rhythm", "$ecf_fx_ward" };
        private static readonly EffectKind[] Kinds = { EffectKind.DodgeFury, EffectKind.ComboFinisher, EffectKind.CalmWard };
        private static readonly int[] Hashes = { Names[0].GetStableHashCode(), Names[1].GetStableHashCode(), Names[2].GetStableHashCode() };
        private static readonly StatusEffect?[] Templates = new StatusEffect?[3];
        private static readonly Sprite?[] Icons = new Sprite?[3];

        /// <summary>Rebuild: forget the icons before the equipped affixes are walked again.</summary>
        public static void ClearIcons()
        {
            Icons[0] = Icons[1] = Icons[2] = null;
        }

        /// <summary>Rebuild: the first equipped item feeding an indicated kind lends its icon.</summary>
        public static void Note(EffectKind kind, ItemDrop.ItemData item)
        {
            for (int i = 0; i < Kinds.Length; i++)
            {
                if (Kinds[i] == kind && Icons[i] == null)
                {
                    Icons[i] = item.GetIcon();
                }
            }
        }

        /// <summary>Shows (or restarts) an indicator; <paramref name="seconds"/> 0 = until hidden.</summary>
        public static void Show(int indicator, float seconds)
        {
            Player? player = Player.m_localPlayer;
            if (player == null || player.IsDead())
            {
                return;
            }
            SEMan seman = player.GetSEMan();
            StatusEffect? effect = seman.GetStatusEffect(Hashes[indicator]) ?? seman.AddStatusEffect(Template(indicator));
            if (effect != null)
            {
                effect.m_ttl = Mathf.Max(0f, seconds);
                effect.m_time = 0f;
                effect.m_icon = Icons[indicator];
            }
        }

        public static void Hide(int indicator)
        {
            Player? player = Player.m_localPlayer;
            if (player != null)
            {
                player.GetSEMan().RemoveStatusEffect(Hashes[indicator], quiet: true);
            }
        }

        // Built once per process and kept alive (the game's clones share its native object).
        private static StatusEffect Template(int indicator)
        {
            StatusEffect? template = Templates[indicator];
            if (template == null)
            {
                template = ScriptableObject.CreateInstance<StatusEffect>();
                template.name = Names[indicator];
                template.hideFlags = HideFlags.HideAndDontSave;
                template.m_name = Titles[indicator];
                template.m_startMessage = "";
                template.m_stopMessage = "";
                Templates[indicator] = template;
            }
            return template;
        }
    }
}
