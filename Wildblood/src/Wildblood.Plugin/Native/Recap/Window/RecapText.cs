// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>The recap's words: times, the killer line, the summary line, a death's list entry and a hit's row.</summary>
    internal static class RecapText
    {
        /// <summary>Seconds into the clip as "0:07.4".</summary>
        public static string Clip(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            return minutes.ToString(CultureInfo.InvariantCulture) + ":"
                + (seconds - minutes * 60f).ToString("00.0", CultureInfo.InvariantCulture);
        }

        public static string Killer(DeathRecap recap)
        {
            string who = "<noparse>" + recap.Killer + "</noparse>";
            if (!recap.Dealt)
            {
                return "Died: <b>" + who + "</b>";
            }
            string how = recap.Cause.Length > 0 && recap.Cause != "hit" ? " <color=#B0B0B0>(" + recap.Cause + ")</color>" : "";
            return "Killed by <b>" + who + "</b>" + how;
        }

        /// <summary>"Day 96, 14:32 · 312 damage in 8.4 s: slash 62%, frost 30%".</summary>
        public static string Summary(DeathRecap recap)
        {
            StringBuilder text = new StringBuilder(When(recap));
            if (recap.Hits.Count == 0)
            {
                return text.Append(" · no hits recorded").ToString();
            }
            Dictionary<string, float> byType = new Dictionary<string, float>();
            float total = 0f;
            foreach (HitRecord hit in recap.Hits)
            {
                total += hit.Damage;
                foreach (KeyValuePair<string, float> part in hit.Parts)
                {
                    byType.TryGetValue(part.Key, out float sum);
                    byType[part.Key] = sum + part.Value;
                }
            }
            float span = recap.Hits[recap.Hits.Count - 1].Time - recap.Hits[0].Time;
            text.Append(" · ").Append(Mathf.RoundToInt(total)).Append(" damage in ").Append(span.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s");
            return text.Append(Shares(byType)).ToString();
        }

        public static string When(DeathRecap recap) => "Day " + recap.Day + ", " + recap.Clock.ToString("HH:mm", CultureInfo.InvariantCulture);

        /// <summary>A death in the list: the killer, then the day, clock and clip length on a smaller line.</summary>
        public static string Entry(DeathRecap recap) =>
            "<b><noparse>" + recap.Killer + "</noparse></b>\n<size=13><color=#B8B8B8>" + When(recap) + " · "
            + Clip(recap.Duration) + "</color></size>";

        /// <summary>"0:07.4   Mad Greydwarf 2★   42   slash 34 · frost 8   HP 51/158".</summary>
        public static string Hit(HitRecord hit, float at)
        {
            StringBuilder text = new StringBuilder();
            text.Append("<pos=0%>").Append(Clip(at));
            string who = hit.Attacker.Length > 0 ? "<noparse>" + hit.Attacker + "</noparse>" : Capital(hit.Source);
            text.Append("<pos=9%>").Append(who);
            text.Append("<pos=46%><b>").Append(Mathf.RoundToInt(hit.Damage)).Append("</b>");
            text.Append("<pos=53%>").Append(Parts(hit.Parts));
            text.Append("<pos=84%><color=#B8B8B8>Health ").Append(Mathf.CeilToInt(hit.Health)).Append('/')
                .Append(Mathf.RoundToInt(hit.MaxHealth)).Append("</color>");
            return text.ToString();
        }

        private static string Parts(List<KeyValuePair<string, float>> parts)
        {
            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<string, float> part in parts)
            {
                if (text.Length > 0)
                {
                    text.Append(" · ");
                }
                text.Append("<color=").Append(Colour(part.Key)).Append('>').Append(part.Key).Append(' ')
                    .Append(Mathf.Max(1, Mathf.RoundToInt(part.Value))).Append("</color>");
            }
            return text.ToString();
        }

        // The four biggest damage types as shares of the whole, coloured like the hit rows.
        private static string Shares(Dictionary<string, float> byType)
        {
            List<KeyValuePair<string, float>> sorted = new List<KeyValuePair<string, float>>(byType);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            float sum = 0f;
            sorted.ForEach(p => sum += p.Value);
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < sorted.Count && i < 4 && sum > 0f; i++)
            {
                text.Append(i == 0 ? ": " : ", ").Append("<color=").Append(Colour(sorted[i].Key)).Append('>')
                    .Append(sorted[i].Key).Append(' ').Append(Mathf.RoundToInt(100f * sorted[i].Value / sum)).Append("%</color>");
            }
            return text.ToString();
        }

        private static string Colour(string type) => type switch
        {
            "fire" or "burning" or "lava" => "#FF9A4D",
            "frost" or "freezing" => "#9FDBFF",
            "lightning" => "#FFE36B",
            "poison" => "#9AD86A",
            "spirit" => "#E4DCFF",
            _ => "#E6E6E6",
        };

        public static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
