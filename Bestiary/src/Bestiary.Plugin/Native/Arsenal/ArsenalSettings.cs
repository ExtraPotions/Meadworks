// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;

namespace ExtraPotions.Bestiary.Runtime.Native.Arsenal
{
    /// <summary>
    /// Section 8: the arsenal skeletons (the skeleton units that carry the skeleton arsenal's weapons; the Executioner is a
    /// boss of its own, section 25), a master on/off switch for all of them and one for each, the Skeleton
    /// Crossbowman's among them (the user, 2026-09-30: it is an arsenal skeleton; its shot's numbers stay in section 6).
    /// Everything else about them and the bone weapons is fixed (see <c>features/skeleton-arsenal.md</c>). Synced; read
    /// at each spawn, so a reload takes effect at once.
    /// </summary>
    public static class ArsenalSettings
    {
        public const string Section = "8 - Skeleton Arsenal";

        private static readonly Dictionary<ArsenalWeapon, GatedEntry<bool>> spawns = new Dictionary<ArsenalWeapon, GatedEntry<bool>>();
        private static GatedEntry<bool> enabled = null!, crossbowman = null!;

        /// <summary>Whether new skeletons with this weapon may spawn: the master switch and its own. Ones already in the world stay.</summary>
        public static bool Spawns(ArsenalWeapon weapon) => enabled.Value && spawns.TryGetValue(weapon, out var on) && on.Value;

        /// <summary>Whether new Skeleton Crossbowmen may spawn: the master switch and its own.</summary>
        public static bool CrossbowmanSpawns => enabled.Value && crossbowman.Value;

        public static void Initialize(NativeConfiguration config)
        {
            enabled = config.Bind(Section, "Enabled", true,
                "All the arsenal skeletons, the crossbowman too, the master switch. Off: none of them spawns, whatever their own switches below say; "
                + "ones already in the world stay.");
            foreach (ArsenalWeapon weapon in ArsenalWeapon.All)
            {
                spawns[weapon] = config.Bind(Section, "Skeleton " + weapon.Title, true,
                    $"The Skeleton {weapon.Title} (bone {weapon.Key.ToLowerInvariant()}) spawns in place of some of the game's "
                    + "skeletons, wherever and whenever they spawn. Off: no new ones; ones already in the world stay.");
            }
            crossbowman = config.Bind(Section, "Skeleton Crossbowman", true,
                "The Skeleton Crossbowman (bone crossbow, blunt bone bolts; its shot's numbers are in section 6) spawns in place "
                + "of some of the game's skeletons that could be archers, wherever and whenever they spawn. Off: no new ones; "
                + "ones already in the world stay.");
        }
    }
}
