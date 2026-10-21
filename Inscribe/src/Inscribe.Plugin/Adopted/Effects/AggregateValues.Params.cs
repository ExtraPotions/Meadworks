// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The Phase 2 tables of a value set: channels whose param picks a slot (damage type taken, element resisted,
    /// creature family, resource) and the two composite effects that feed other kinds. Filled on rebuild only; the
    /// hooks read plain arrays. Local player only, like the rest of the set.
    /// </summary>
    internal sealed partial class AggregateValues
    {
        public const int Undead = 0, Beasts = 1, Sea = 2, Boss = 3;
        public const int Health = 0, Stamina = 1, Eitr = 2;

        private static readonly string[] FamilyIds = { "undead", "beasts", "sea", "boss" };
        private static readonly string[] ResourceIds = { "health", "stamina", "eitr" };
        private static readonly List<HitData.DamageModPair>?[] ResistLists = new List<HitData.DamageModPair>?[16];

        /// <summary>Per damage type (<see cref="DamageSlots"/> order): the fraction taken off that part of an incoming hit.</summary>
        public readonly float[] DamageTaken = new float[DamageSlots.Count];
        public bool AnyDamageTaken;

        /// <summary>Per creature family: the fraction added to hits on it.</summary>
        public readonly float[] Slayer = new float[4];

        /// <summary>Per resource: flat amount restored on a kill, on a perfect block; fraction of a hit leeched.</summary>
        public readonly float[] KillRestore = new float[3];
        public readonly float[] ParryRestore = new float[3];
        public readonly float[] Leech = new float[3];

        /// <summary>Per resource: flat amount restored by any block that holds (Shield Mend).</summary>
        public readonly float[] BlockRestore = new float[3];

        /// <summary>Elements (fire, frost, lightning, poison) with the game's Resistant step from a bulwark flag.</summary>
        public DamageMask Resist;

        /// <summary>The Resistant pairs for <see cref="Resist"/>, built once per combination and shared; never mutated.</summary>
        public List<HitData.DamageModPair>? ResistMods => Resist == DamageMask.None ? null : ResistList(Resist);

        private void ClearParams()
        {
            Array.Clear(DamageTaken, 0, DamageTaken.Length);
            Array.Clear(Slayer, 0, Slayer.Length);
            Array.Clear(KillRestore, 0, KillRestore.Length);
            Array.Clear(ParryRestore, 0, ParryRestore.Length);
            Array.Clear(Leech, 0, Leech.Length);
            Array.Clear(BlockRestore, 0, BlockRestore.Length);
            AnyDamageTaken = false;
            Resist = DamageMask.None;
        }

        // Rebuild only. True when the kind was handled here.
        private bool AddParam(EffectKind kind, ChannelDef channel, float amount)
        {
            switch (kind)
            {
                case EffectKind.DamageTaken: AddTaken(channel, amount); return true;
                case EffectKind.ResistModifier: Resist |= channel.Sample.ParamDamage; return true;
                case EffectKind.Slayer: AddAt(Slayer, FamilyIds, channel, amount); return true;
                case EffectKind.OnKillRestore: AddAt(KillRestore, ResourceIds, channel, amount); return true;
                case EffectKind.ParryRestore: AddAt(ParryRestore, ResourceIds, channel, amount); return true;
                case EffectKind.Leech: AddAt(Leech, ResourceIds, channel, amount); return true;
                case EffectKind.BlockRestore: AddAt(BlockRestore, ResourceIds, channel, amount); return true;
                case EffectKind.HealthForRegen: AddHealthForRegen(amount); return true;
                case EffectKind.EitrForRegen: AddEitrForRegen(amount); return true;
                case EffectKind.GlassCannon: AddGlassCannon(amount); return true;
                default: return false;
            }
        }

        // A group param (physical, elemental, all) lowers each member type by the full amount, like damage dealt.
        private void AddTaken(ChannelDef channel, float fraction)
        {
            DamageMask mask = channel.Sample.ParamDamage;
            for (int i = 0; i < DamageSlots.Count; i++)
            {
                if ((mask & DamageSlots.Masks[i]) != 0)
                {
                    DamageTaken[i] += fraction;
                    AnyDamageTaken = true;
                }
            }
        }

        private static void AddAt(float[] table, string[] ids, ChannelDef channel, float amount)
        {
            int i = Array.IndexOf(ids, channel.Param);
            if (i >= 0)
            {
                table[i] += amount;
            }
        }

        // Stout Heart: +X maximum health and health regenerates X% slower (one stored number, affixes.md 1).
        private void AddHealthForRegen(float flat)
        {
            _byKind[(int)EffectKind.MaxHealth] += flat;
            _byKind[(int)EffectKind.HealthRecovery] -= flat / 100f;
        }

        // Restless Mind: +X% eitr regeneration; the kind's own slot holds the maximum-eitr loss, X/2 %.
        private void AddEitrForRegen(float fraction)
        {
            _byKind[(int)EffectKind.EitrRecovery] += fraction;
            _byKind[(int)EffectKind.EitrForRegen] += fraction / 2f;
        }

        // Glass Cannon: every damage type of your hits +X% (as damage_dealt:all); the kind's own slot holds the armour
        // loss, X% (Combat3/GlassCannon).
        private void AddGlassCannon(float fraction)
        {
            for (int i = 0; i < DamageSlots.Count; i++)
            {
                DamageDealt[i] += fraction;
            }
            AnyDamage = true;
            _byKind[(int)EffectKind.GlassCannon] += fraction;
        }

        private static List<HitData.DamageModPair> ResistList(DamageMask mask)
        {
            int key = ((mask & DamageMask.Fire) != 0 ? 1 : 0) | ((mask & DamageMask.Frost) != 0 ? 2 : 0)
                | ((mask & DamageMask.Lightning) != 0 ? 4 : 0) | ((mask & DamageMask.Poison) != 0 ? 8 : 0);
            List<HitData.DamageModPair>? list = ResistLists[key];
            if (list == null)
            {
                list = new List<HitData.DamageModPair>();
                AddPair(list, key, 1, HitData.DamageType.Fire);
                AddPair(list, key, 2, HitData.DamageType.Frost);
                AddPair(list, key, 4, HitData.DamageType.Lightning);
                AddPair(list, key, 8, HitData.DamageType.Poison);
                ResistLists[key] = list;
            }
            return list;
        }

        private static void AddPair(List<HitData.DamageModPair> list, int key, int bit, HitData.DamageType type)
        {
            if ((key & bit) != 0)
            {
                list.Add(new HitData.DamageModPair { m_type = type, m_modifier = HitData.DamageModifier.Resistant });
            }
        }
    }
}
