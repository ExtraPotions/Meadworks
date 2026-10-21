// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// ECR f331465b3ed2551c89decc42ba81565a7b689543 behavior; authority receipts replace count-plus-one writes.
// Changed by expDARE, 2026: owned Wildblood integration, bounded authority receipts and native emission guards.
// Changed by expDARE, 2026: Deep North material row for Kall Fimbulbringer's final phase.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Wildblood.Bosses
{
    public sealed class BossProgression
    {
        public const int Maximum = 10;
        readonly List<string> receipts;
        BossProgression(List<string> receipts) { this.receipts = receipts; }
        public int Count => receipts.Count;
        public bool Contains(string token) => receipts.Contains(token);
        public float StarBoost(bool enabled) => enabled ? 1f + Count * .5f : 1f;
        public static string HintKey(int count,bool enabled)
        {
            if(count<0||count>Maximum)throw new ArgumentOutOfRangeException(nameof(count));
            return !enabled||count==0?"":count==1?"boss_defeated_once":count==Maximum?"boss_defeated_cap":"boss_defeated_times";
        }
        public string Encode() => "1|" + string.Join(",", receipts);
        public static bool ValidToken(string token) => Guid.TryParseExact(token, "N", out var value) && value != Guid.Empty && value.ToString("N") == token;
        public bool Complete(string token)
        {
            if (!ValidToken(token)) throw new ArgumentException("Invalid boss encounter token.");
            if (Count == Maximum || receipts.Contains(token)) return false;
            receipts.Add(token); return true;
        }
        public static BossProgression Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return new BossProgression(new List<string>());
            if (!text.StartsWith("1|", StringComparison.Ordinal) || text.Length > 331) throw new FormatException("Invalid boss progression state.");
            var tokens = text.Substring(2).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (tokens.Count > Maximum || tokens.Any(token => !ValidToken(token)) || tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Count
                || "1|" + string.Join(",", tokens) != text) throw new FormatException("Invalid boss progression receipts.");
            return new BossProgression(tokens);
        }
    }
    public sealed class BossMaterial
    {
        public string Prefab { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        internal BossMaterial(string prefab, int minimum, int maximum) { Prefab = prefab; Minimum = minimum; Maximum = maximum; }
        public int Roll(int stars, int players, float factor, Func<int,int,int> draw)
        {
            if (stars < 0 || players < 0 || float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0) throw new ArgumentOutOfRangeException();
            int rolls = checked((stars + 1) * Math.Max(1, players));
            long amount = 0;
            for (int i = 0; i < rolls; i++) { int rolled = draw(Minimum, Maximum + 1); if (rolled < Minimum || rolled > Maximum) throw new InvalidOperationException("Reward roll outside its row."); amount += rolled; }
            return (int)Math.Max(1, Math.Min(100, Math.Round((float)amount * factor, MidpointRounding.ToEven)));
        }
    }
    public static class BossMaterials
    {
        static readonly Dictionary<string,BossMaterial> rows = new Dictionary<string,BossMaterial>(StringComparer.Ordinal) {
            ["Eikthyr"] = new BossMaterial("Flint",2,4), ["gd_king"] = new BossMaterial("CopperOre",5,10),
            ["Bonemass"] = new BossMaterial("IronScrap",5,10), ["Dragon"] = new BossMaterial("SilverOre",5,10),
            ["GoblinKing"] = new BossMaterial("BlackMetalScrap",5,10), ["SeekerQueen"] = new BossMaterial("Carapace",5,10),
            ["Fader"] = new BossMaterial("FlametalOreNew",5,10),
            // Kall Fimbulbringer's final phase (the one that sets defeated_frozenking_p3) pays the Deep North ore, Petrified Tissue.
            ["FrozenKing_p3"] = new BossMaterial("GoldOre",5,10)
        };
        public static BossMaterial? For(string prefab) => rows.TryGetValue(prefab, out var row) ? row : null;
    }
}
