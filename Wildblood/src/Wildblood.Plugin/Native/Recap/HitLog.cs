// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// The hits the local player took in the last seconds, kept on this machine only. A hit on a player is applied on
    /// that player's own client (it owns the body), so every hit passes here, on a dedicated server too, and nothing is
    /// sent anywhere. Burning and poison tick without an attacker, so the last creature whose hit carried fire, spirit or
    /// poison is remembered and named on those ticks.
    /// </summary>
    internal static class HitLog
    {
        /// <summary>Kept a little longer than the video, so a recap always has every hit its video shows.</summary>
        private const float Slack = 5f;

        private static readonly List<HitRecord> _hits = new List<HitRecord>();
        private static string _burnedBy = "";
        private static string _poisonedBy = "";

        /// <summary>From <c>RPC_Damage</c>, before the game splits a hit's fire, spirit and poison off into ticks.</summary>
        public static void NoteSource(HitData hit)
        {
            if (!hit.HaveAttacker())
            {
                return;
            }
            HitData.DamageTypes d = hit.m_damage;
            if (d.m_fire > 0f || d.m_spirit > 0f)
            {
                _burnedBy = HitDescribe.Attacker(hit.GetAttacker());
            }
            if (d.m_poison > 0f)
            {
                _poisonedBy = HitDescribe.Attacker(hit.GetAttacker());
            }
        }

        /// <summary>From <c>ApplyDamage</c>, once the hit has landed: the damage it did and the health left.</summary>
        public static void Record(Player player, HitData hit)
        {
            string source = HitDescribe.Source(hit);
            HitRecord record = new HitRecord
            {
                Time = Time.time,
                Attacker = AttackerOf(hit, source),
                Source = source,
                Damage = hit.GetTotalDamage(),
                Health = Mathf.Max(0f, player.GetHealth()),
                MaxHealth = player.GetMaxHealth(),
                Parts = HitDescribe.Parts(hit, source),
            };
            _hits.Add(record);
            Trim(record.Time - RecapSettings.Seconds.Value - Slack);
        }

        public static HitRecord? Last => _hits.Count > 0 ? _hits[_hits.Count - 1] : null;

        /// <summary>The hits from <paramref name="from"/> on, handed over and forgotten here, with the remembered sources.</summary>
        public static List<HitRecord> Take(float from)
        {
            Trim(from);
            List<HitRecord> taken = new List<HitRecord>(_hits);
            Clear();
            return taken;
        }

        public static void Clear()
        {
            _hits.Clear();
            _burnedBy = "";
            _poisonedBy = "";
        }

        private static string AttackerOf(HitData hit, string source)
        {
            if (hit.HaveAttacker())
            {
                return HitDescribe.Attacker(hit.GetAttacker());
            }
            return source switch
            {
                "burning" => _burnedBy,
                "poison" => _poisonedBy,
                _ => "",
            };
        }

        private static void Trim(float before)
        {
            int old = 0;
            while (old < _hits.Count && _hits[old].Time < before)
            {
                old++;
            }
            _hits.RemoveRange(0, old);
        }
    }
}
