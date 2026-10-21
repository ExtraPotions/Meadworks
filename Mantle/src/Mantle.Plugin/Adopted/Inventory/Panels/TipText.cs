using System.Collections.Generic;
using System.Text;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// One stat's breakdown as the tooltip's text: lines of a source and its part ("Iron helmet: 20"), now and then a
    /// heading, and the rest of the total nobody named as one "Other effects" line, so the parts always add up to the
    /// line's number. Sources are localized here; amounts are orange, as the game's own tooltips show numbers.
    /// </summary>
    public sealed class TipText
    {
        private readonly StringBuilder text = new StringBuilder();

        public bool Empty => text.Length == 0;

        public TipText Part(string source, string amount)
        {
            Line().Append(Language.Localize(source)).Append(": <color=orange>").Append(amount).Append("</color>");
            return this;
        }

        public TipText Heading(string title)
        {
            if (!Empty)
                text.Append('\n');
            Line().Append("<color=#C9A46B>").Append(Language.Localize(title)).Append("</color>");
            return this;
        }

        /// <summary>What the named parts leave of the total, as "Other effects", when it is at least half a unit.</summary>
        public TipText Rest(float rest, System.Func<float, string> format)
        {
            return Mathf.Abs(rest) >= 0.5f ? Part(StatTipWords.Other, format(rest)) : this;
        }

        /// <summary>The breakdown, or the "nothing gives this" line when no part was named.</summary>
        public override string ToString() => Empty ? Language.Localize(StatTipWords.Nothing) : text.ToString();

        private StringBuilder Line() => Empty ? text : text.Append('\n');

        public static string Whole(float value) => value.ToString("0");

        public static string Signed(float value) => value.ToString("+0;-0;0");

        public static string Percent(float fraction) => (fraction * 100f).ToString("+0;-0;0") + "%";

        /// <summary>Everything the player wears or holds (the game's slots and Mantle's extra utilities).</summary>
        public static IEnumerable<ItemDrop.ItemData> Worn(Player player)
        {
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item.m_equipped)
                    yield return item;
            }
        }

        /// <summary>A status effect's own name, or "An effect" for the game's unnamed ones.</summary>
        public static string EffectName(StatusEffect effect) =>
            string.IsNullOrEmpty(effect.m_name) ? StatTipWords.Effect : effect.m_name;
    }
}
