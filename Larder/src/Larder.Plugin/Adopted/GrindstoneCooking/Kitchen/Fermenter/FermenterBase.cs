// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterBase.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The mead base in a fermenter as GrindstoneSkills records it: the Cooking level of the player who put it in
    /// (<see cref="Keys.BaseLevel"/>), in the barrel's own ZDO. The game stores only the content's prefab hash and the
    /// start time there, so the key travels with the same ZDO to every peer and outlives the owner leaving. Written by
    /// the owner when a base goes in, cleared when the batch is tapped or dropped. It reads 0 when unset, which is what
    /// a vanilla add means.
    /// </summary>
    internal static class FermenterBase
    {
        private static readonly int LevelHash = Keys.BaseLevel.GetStableHashCode();

        public static float GetLevel(ZDO zdo) => zdo == null ? 0f : Sanitize(Keys.ReadFloat(zdo, Keys.BaseLevel));

        /// <summary>Writes the level, touching the ZDO only when it changes, so a vanilla barrel gains no data.</summary>
        public static void Write(ZDO zdo, float level)
        {
            if (zdo == null)
                return;
            level = Sanitize(level);
            if (Keys.ReadFloat(zdo, Keys.BaseLevel) != level)
                zdo.Set(LevelHash, level);
        }

        public static void Clear(ZDO zdo) => Write(zdo, 0f);

        /// <summary>A level that came over the network: never negative, NaN or infinite.</summary>
        public static float Sanitize(float level) => float.IsNaN(level) || float.IsInfinity(level) ? 0f : Mathf.Max(0f, level);
    }
}
