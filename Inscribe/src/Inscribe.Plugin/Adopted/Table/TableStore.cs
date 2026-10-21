// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; stored Awakening and
// Tempering Runes move to Ascension and Honing on the owner (2026-10-19).
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// What one Rune Table holds, kept in its ZDO so the game saves and replicates it (rune-table.md section 5): a count
    /// per rune (<c>ecf_rt_rune_&lt;id&gt;</c>) and one pool of pure essence (<c>ecf_rt_essence</c>) every essence draws from. Anyone reads; only the
    /// ZDO's owner writes, and the table hands its ownership to whoever opens it (as the game's chests do), so every
    /// write here happens on the client that has it open. A write without ownership changes nothing and answers false.
    /// </summary>
    internal sealed class TableStore
    {
        private static readonly Dictionary<string, int> RuneKeys = new Dictionary<string, int>();
        private static readonly int EssenceKey = "ecf_rt_essence".GetStableHashCode();
        private static readonly int CastKey = "ecf_rt_cast".GetStableHashCode();
        private static readonly int CastStoneKey = "ecf_rt_cast_stone".GetStableHashCode();

        private readonly ZNetView _view;

        public TableStore(ZNetView view)
        {
            _view = view;
        }

        public bool Valid => _view != null && _view.IsValid();

        public bool Writable => Valid && _view.IsOwner();

        public int Runes(string runeId) => Valid ? _view.GetZDO().GetInt(RuneKey(runeId)) : 0;

        /// <summary>The pure essence in the table's pool, which every chosen essence costs from.</summary>
        public int Essence => Valid ? _view.GetZDO().GetInt(EssenceKey) : 0;

        /// <summary>How many stones were used at the table (every client plays the vortex when it grows) and the last one's id.</summary>
        public int Casts => Valid ? _view.GetZDO().GetInt(CastKey) : 0;

        public string CastStone => Valid ? _view.GetZDO().GetString(CastStoneKey) : "";

        /// <summary>One use of a stone at the table, on its owner (<see cref="TableCast"/> shows it everywhere).</summary>
        public void MarkCast(string stoneId)
        {
            if (!Writable)
            {
                return;
            }
            ZDO zdo = _view.GetZDO();
            zdo.Set(CastStoneKey, stoneId);
            zdo.Set(CastKey, zdo.GetInt(CastKey) + 1);
        }

        public bool AddRunes(string runeId, int count) => Change(RuneKey(runeId), count);

        public bool TakeRunes(string runeId, int count) => Runes(runeId) >= count && Change(RuneKey(runeId), -count);

        public bool AddEssence(int amount) => Change(EssenceKey, amount);

        public bool TakeEssence(int amount) => Essence >= amount && Change(EssenceKey, -amount);

        /// <summary>
        /// On the owner: moves every stored legacy rune (Awakening, Tempering) into its successor (Ascension, Honing), one
        /// for one, and zeroes the legacy count; the number moved. A second call moves nothing.
        /// </summary>
        public int MigrateLegacy()
        {
            if (!Writable)
            {
                return 0;
            }
            var counts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, string> pair in global::ExtraPotions.Inscribe.Runes.LegacyRunes.Successors)
            {
                counts[pair.Key] = Runes(pair.Key);
                counts[pair.Value] = Runes(pair.Value);
            }
            int moved = global::ExtraPotions.Inscribe.Runes.LegacyRunes.MoveCounts(counts);
            if (moved > 0)
            {
                ZDO zdo = _view.GetZDO();
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    zdo.Set(RuneKey(pair.Key), pair.Value);
                }
            }
            return moved;
        }

        /// <summary>Every rune id the table can hold, in the economy file's order (the built-in five).</summary>
        public static IEnumerable<string> RuneIds => StoneCatalog.BuiltInIds;

        private bool Change(int key, int delta)
        {
            if (!Writable || delta == 0)
            {
                return Writable;
            }
            ZDO zdo = _view.GetZDO();
            zdo.Set(key, System.Math.Max(0, zdo.GetInt(key) + delta));
            return true;
        }

        private static int RuneKey(string runeId) => KeyOf(RuneKeys, "ecf_rt_rune_", runeId);

        private static int KeyOf(Dictionary<string, int> cache, string prefix, string id)
        {
            if (!cache.TryGetValue(id, out int key))
            {
                key = (prefix + id).GetStableHashCode();
                cache[id] = key;
            }
            return key;
        }
    }
}
