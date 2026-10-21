// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Changed by expDARE, 2026: the portable socket rules as plain data (parity #23 and #68) beside the adopted plugin code, so the
// numbers are tested without the game.
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCrafting 0.10.0 (Sockets/GemCatalog.cs, Sockets/SocketDrops.cs,
// Sockets/BossGems.cs, Stones/SalvageRunes.cs, Rules/Model/StoneCatalog.cs), geraldjglasgow/ValheimMods d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Inscribe.Rarity;

namespace ExtraPotions.Inscribe.Runes
{
    /// <summary>Which of a gem's stats an item's base takes; None takes no socket.</summary>
    public enum SocketBase { None, Weapon, Staff, Armour }

    /// <summary>
    /// The Dvergr Chisel and the eleven god gems (EliteCrafting 0.10.0): what each gem gives per base, which item groups take
    /// sockets (weapons, pickaxes, staves, armour, shields and backpacks), the chisel's and the drops' socket odds, and the boss gems.
    /// </summary>
    public static class SocketStones
    {
        public const string ChiselId = "dvergr_chisel";

        /// <summary>The class a backpack provider (Mantle) registers through <c>meadworks.items.classes</c>; worn like armour.</summary>
        public const string BackpackClass = "backpack";

        /// <summary>The one tool class that takes sockets: a pickaxe also fights, so it takes weapon gems (playtest 2026-10-20).</summary>
        public const string PickaxeClass = "pickaxe";

        public const int MaxSockets = 3;
        public const float BossGemChance = 50f;
        public const float BossChiselChance = 25f;

        public static IReadOnlyList<string> GemIds { get; } = new[]
        {
            "gem_surtr", "gem_ymir", "gem_thor", "gem_nidhogg", "gem_hel", "gem_tyr", "gem_freyja", "gem_odin", "gem_skadi", "gem_heimdall", "gem_sleipnir",
        };

        /// <summary>The eight game bosses (FrozenKing_p3 is the Deep North boss's last phase); a boss a server adds to its own boss map drops no gems.</summary>
        public static IReadOnlyList<string> Bosses { get; } = new[] { "Eikthyr", "gd_king", "Bonemass", "Dragon", "GoblinKing", "SeekerQueen", "Fader", "FrozenKing_p3" };

        // gem -> (weapon, staff, armour) inscription id; null where the gem does not fit.
        private static readonly Dictionary<string, (string? Weapon, string? Staff, string? Armour)> Stats = new Dictionary<string, (string?, string?, string?)>(StringComparer.Ordinal)
        {
            ["gem_surtr"] = ("emberbrand", "primal_fury", "flameward"),
            ["gem_ymir"] = ("rimebrand", "seidr_thrift", "frostward"),
            ["gem_thor"] = ("stormbrand", "thors_chain", "stormward"),
            ["gem_nidhogg"] = ("venombrand", "lingering_wounds", "venomward"),
            ["gem_hel"] = ("spiritbrand", "soul_reaper", "spiritward"),
            ["gem_tyr"] = ("honed_might", "grave_command", "resolute"),
            ["gem_freyja"] = ("blood_drinker", "blood_thrift", "vigor"),
            ["gem_odin"] = ("seidr_siphon", "seidr_flow", "wellspring"),
            ["gem_skadi"] = ("wind_siphon", "grave_vigor", "endurance"),
            ["gem_heimdall"] = ("keen_eye", "swift_casting", "mist_veil"),
            ["gem_sleipnir"] = (null, null, "fleetfoot"),
        };

        private static readonly float[] MagicDrop = { 80f, 15f, 4f, 1f };
        private static readonly float[] RareDrop = { 60f, 25f, 11f, 4f };

        public static bool IsGem(string? id) => id != null && Stats.ContainsKey(id);

        public static bool IsSocketStone(string? id) => id == ChiselId || IsGem(id);

        /// <summary>The socket base of an item class: by its group, a torch (<c>light</c>), every jewel but the backpack and every tool but the pickaxe take none.</summary>
        public static SocketBase BaseOf(string? group, string? classId)
        {
            switch (group)
            {
                case "onehand":
                case "twohand":
                case "ranged":
                    return SocketBase.Weapon;
                case "magic":
                    return SocketBase.Staff;
                case "armour":
                    return SocketBase.Armour;
                case "offhand":
                    return classId == "light" ? SocketBase.None : SocketBase.Armour;
                case "jewel":
                    return classId == BackpackClass ? SocketBase.Armour : SocketBase.None;
                case "tools":
                    return classId == PickaxeClass ? SocketBase.Weapon : SocketBase.None;
                default:
                    return SocketBase.None;
            }
        }

        /// <summary>The inscription a gem gives on this base; null when it does not fit or is unknown.</summary>
        public static string? StatFor(string gemId, SocketBase socketBase)
        {
            if (!Stats.TryGetValue(gemId, out var stats)) return null;
            return socketBase == SocketBase.Weapon ? stats.Weapon : socketBase == SocketBase.Staff ? stats.Staff : socketBase == SocketBase.Armour ? stats.Armour : null;
        }

        /// <summary>Weights for 0, 1, 2 and 3 sockets on dropped gear of this rarity.</summary>
        public static float[] DropWeights(string? rarityId) => (rarityId == "rare" ? RareDrop : MagicDrop).ToArray();

        /// <summary>The chance in percent that the chisel cuts this many sockets (1-3); a Normal item cuts as Magic.</summary>
        public static double CutChance(string? rarityId, int sockets)
        {
            var weights = CutWeights(rarityId);
            return sockets < 1 || sockets > MaxSockets ? 0 : 100.0 * weights[sockets - 1] / weights.Sum();
        }

        /// <summary>The sockets a chisel cuts: 1 to 3 by the drop weights without the none.</summary>
        public static int Cut(string? rarityId, IDice dice) => 1 + Math.Max(0, Weighted.Pick(CutWeights(rarityId), dice));

        /// <summary>A boss rolls one gem, plus one more per star.</summary>
        public static int BossGemRolls(int stars) => 1 + Math.Max(0, stars);

        private static float[] CutWeights(string? rarityId) => DropWeights(rarityId).Skip(1).ToArray();
    }

    /// <summary>
    /// #68: the rune a salvaged Magic or Rare item may give back (EliteCrafting 0.10.0 <c>SalvageRunes</c>): the first enabled
    /// promote rune that works on the rarity below the item's, at a fixed one in four. The salvaging product rolls the chance.
    /// </summary>
    public static class SalvageRune
    {
        public const float Chance = 0.25f;

        public static string? RuneFor(RuneCatalog runes, RarityLadder ladder, string? rarityId)
        {
            var rarity = ladder.Find(rarityId);
            if (rarity == null || rarity.Index == 0) return null;
            var below = ladder.Rarities.LastOrDefault(r => r.Index < rarity.Index);
            if (below == null) return null;
            return runes.Runes.FirstOrDefault(r => r.Enabled && r.Verb == RuneVerb.Promote && r.AppliesToRarity(below.Id))?.Id;
        }

        public static bool Rolls(IDice dice) => dice.Value() < Chance;
    }
}
