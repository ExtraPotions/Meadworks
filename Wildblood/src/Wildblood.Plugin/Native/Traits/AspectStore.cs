// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// The boss-aspect state that is not part of the rolled traits: an altar's current stars and aspect (with a Bountiful
    /// one's extras) and shift time, a twin's or tethered boss's partner, a phantom copy's boss, a phantom boss's split
    /// count and a summoner's wave count. The single seam between that state and the ZDO, as <see cref="TraitStore"/> is
    /// for the traits. Reads work anywhere; every write is made by the object's owner.
    /// </summary>
    public static class AspectStore
    {
        private const int Unrolled = -1;

        public static bool AltarRolled(ZDO zdo) => ReadInt(zdo, TraitKeys.AltarAspect, Unrolled) != Unrolled;

        public static Aspect GetAltarAspect(ZDO zdo)
        {
            int value = ReadInt(zdo, TraitKeys.AltarAspect, Unrolled);
            return value == Unrolled ? Aspect.None : (Aspect)value;
        }

        /// <summary>Everything the altar offers: its aspect and, beside Bountiful, the extras the boss will carry.</summary>
        public static BossAspects GetAltarAspects(ZDO zdo) =>
            new BossAspects(GetAltarAspect(zdo), ReadInt(zdo, TraitKeys.AltarExtra));

        /// <summary>The world time (whole milliseconds) the altar shifts next; 0 before its first shift.</summary>
        public static long GetAltarShiftAt(ZDO zdo) => ReadLong(zdo, TraitKeys.AltarShiftAt);

        public static void SetAltarAspects(ZDO zdo, BossAspects aspects)
        {
            if (!Owned(zdo)) return;
            zdo.Set(TraitKeys.AltarAspect, (int)aspects.Headline);
            if (ReadInt(zdo, TraitKeys.AltarExtra) != aspects.Extras)
            {
                zdo.Set(TraitKeys.AltarExtra, aspects.Extras); // 0 clears it; an altar never Bountiful never gets the key
            }
        }

        public static void SetAltarShiftAt(ZDO zdo, long shiftAtMs) { if (Owned(zdo)) zdo.Set(TraitKeys.AltarShiftAt, shiftAtMs); }

        /// <summary>False until the altar's stars are first rolled - and on an altar rolled before stars came to altars.</summary>
        public static bool AltarStarsRolled(ZDO zdo) => ReadInt(zdo, TraitKeys.AltarStars, Unrolled) != Unrolled;

        public static int GetAltarStars(ZDO zdo)
        {
            int value = ReadInt(zdo, TraitKeys.AltarStars, Unrolled);
            return value < 0 ? 0 : value;
        }

        public static void SetAltarStars(ZDO zdo, int stars) { if (Owned(zdo)) zdo.Set(TraitKeys.AltarStars, stars); }

        public static ZDOID GetTwin(ZDO zdo) => ReadId(zdo, TraitKeys.TwinPartner);

        public static void SetTwin(ZDO zdo, ZDOID partner) { if (Owned(zdo)) zdo.Set(TraitKeys.TwinPartner, partner); }

        public static ZDOID GetTether(ZDO zdo) => ReadId(zdo, TraitKeys.TetherPartner);

        public static void SetTether(ZDO zdo, ZDOID partner) { if (Owned(zdo)) zdo.Set(TraitKeys.TetherPartner, partner); }

        public static ZDOID GetPhantomOf(ZDO zdo) => ReadId(zdo, TraitKeys.PhantomOf);

        public static void SetPhantomOf(ZDO zdo, ZDOID boss) { if (Owned(zdo)) zdo.Set(TraitKeys.PhantomOf, boss); }

        /// <summary>The boss an echo replays; None for anything that is not an echo.</summary>
        public static ZDOID GetEchoOf(ZDO zdo) => ReadId(zdo, TraitKeys.EchoOf);

        public static void SetEchoOf(ZDO zdo, ZDOID boss) { if (Owned(zdo)) zdo.Set(TraitKeys.EchoOf, boss); }

        public static int GetWaves(ZDO zdo) => ReadInt(zdo, TraitKeys.SummonWaves);

        public static void SetWaves(ZDO zdo, int waves) { if (Owned(zdo)) zdo.Set(TraitKeys.SummonWaves, waves); }

        public static float GetPhantomOrder(ZDO zdo) => ReadFloat(zdo, TraitKeys.PhantomOrder);

        public static void SetPhantomOrder(ZDO zdo, float order) { if (Owned(zdo)) zdo.Set(TraitKeys.PhantomOrder, order); }

        public static int GetSplits(ZDO zdo) => ReadInt(zdo, TraitKeys.PhantomSplits);

        public static void SetSplits(ZDO zdo, int splits) { if (Owned(zdo)) zdo.Set(TraitKeys.PhantomSplits, splits); }

        private static bool Owned(ZDO zdo) { if (zdo == null || !zdo.IsOwner()) return false; TraitKeys.MigrateAuxiliary(zdo); return true; }
        private static int ReadInt(ZDO zdo, string key, int fallback = 0) { TraitKeys.MigrateAuxiliary(zdo); return TraitKeys.Int(zdo, key, fallback); }
        private static long ReadLong(ZDO zdo, string key) { TraitKeys.MigrateAuxiliary(zdo); return TraitKeys.Long(zdo, key); }
        private static float ReadFloat(ZDO zdo, string key) { TraitKeys.MigrateAuxiliary(zdo); return TraitKeys.Float(zdo, key); }
        private static ZDOID ReadId(ZDO zdo, string key) { TraitKeys.MigrateAuxiliary(zdo); return TraitKeys.Id(zdo, key); }
    }
}
