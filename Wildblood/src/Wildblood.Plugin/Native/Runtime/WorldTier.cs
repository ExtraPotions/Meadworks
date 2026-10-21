// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// The world tier as this machine sees it: how many of the listed bosses the world carries a defeat key for. It is
    /// derived, never stored. The server owns the game's global keys and already sends them to every client on join and
    /// on every change, so the client that owns and rolls a creature reads the same tier as the server without a
    /// message of the mod's own. With tiers off, or before the world is up, the tier is 0. The count is kept until the
    /// world's keys change (<see cref="KeysChanged"/>, from the game's own key handling), the rule set is replaced or the
    /// world is left, and in any case for no more than <see cref="RecountSeconds"/>, so the HUD box can ask every frame.
    /// </summary>
    public static class WorldTier
    {
        /// <summary>The longest a count is kept, should a key ever change by a path that does not report it.</summary>
        private const float RecountSeconds = 5f;

        private static int _tier;
        private static int _version;
        private static int _countedVersion = -1;
        private static RuleSet? _countedRules;
        private static ZoneSystem? _countedZones;
        private static float _countedAt;

        public static int Current()
        {
            RuleSet set = RuleState.Active;
            if (!set.Tiers.Enabled) return 0;
            if (set.Tiers.Override >= 0) return Mathf.Clamp(set.Tiers.Override, 0, 7);
            ZoneSystem zones = ZoneSystem.instance;
            if (_countedVersion == _version && ReferenceEquals(set, _countedRules) && ReferenceEquals(zones, _countedZones)
                && Time.unscaledTime - _countedAt < RecountSeconds)
            {
                return _tier;
            }
            _tier = Count(set.Tiers, zones);
            _countedVersion = _version;
            _countedRules = set;
            _countedZones = zones;
            _countedAt = Time.unscaledTime;
            return _tier;
        }

        /// <summary>The world's global keys changed: the next <see cref="Current"/> counts again.</summary>
        public static void KeysChanged() => _version++;

        private static int Count(TierRules rules, ZoneSystem zones)
        {
            if (!rules.Enabled || zones == null)
            {
                return 0;
            }
            int tier = 0;
            foreach (string key in rules.BossKeys)
            {
                if (zones.GetGlobalKey(key))
                {
                    tier++;
                }
            }
            return tier;
        }

        /// <summary>The highest tier the listed bosses can raise the world to; 0 with tiers off.</summary>
        public static int Ceiling()
        {
            TierRules rules = RuleState.Active.Tiers;
            return rules.Enabled ? rules.BossKeys.Count : 0;
        }

        /// <summary>True when the world carries this defeat key.</summary>
        public static bool IsDefeated(string key) => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);

        /// <summary>
        /// Every boss the game knows that sets a defeat key when it dies, vanilla and modded, as key to prefab name - the
        /// name the rule file's `per boss` block matches on. This is how `wildblood tier` names the listed keys and how a
        /// mistyped key gets noticed.
        /// </summary>
        public static Dictionary<string, string> KnownBosses()
        {
            Dictionary<string, string> bosses = new Dictionary<string, string>();
            if (ZNetScene.instance == null)
            {
                return bosses;
            }
            foreach (UnityEngine.GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Character? boss = prefab != null ? prefab.GetComponent<Character>() : null;
                if (boss != null && boss.m_boss && !string.IsNullOrEmpty(boss.m_defeatSetGlobalKey))
                {
                    bosses[boss.m_defeatSetGlobalKey.ToLowerInvariant()] = prefab!.name;
                }
            }
            return bosses;
        }

        /// <summary>Logs every listed key that no known boss sets, once per world start, so a typo does not sit silent.</summary>
        public static void WarnUnknownKeys()
        {
            Dictionary<string, string> known = KnownBosses();
            if (known.Count == 0)
            {
                return;
            }
            foreach (string key in RuleState.Active.Tiers.BossKeys)
            {
                if (!known.ContainsKey(key))
                {
                    Log.Warn($"world tiers: no boss this game knows sets '{key}', so it only counts if something else sets it");
                }
            }
        }
    }
}
