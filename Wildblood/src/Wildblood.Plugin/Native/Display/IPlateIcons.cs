// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// A row of icons a mutation draws on a creature's nameplate - <see cref="PouchIcons"/> for Thieving,
    /// <see cref="MealIcons"/> for Devouring - so the HUD patch adds either the same way, once per nameplate.
    /// </summary>
    internal interface IPlateIcons
    {
        /// <summary>Binds the row to its creature and the nameplate's health bar, right after it is added.</summary>
        void Init(Character character, RectTransform healthBar);
    }
}
