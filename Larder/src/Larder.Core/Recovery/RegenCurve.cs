// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/StaminaRegen.cs (RegenCurve).
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; Mathf replaced by LarderMath.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Recovery
{
    /// <summary>
    /// The bar-level regeneration curve shared by stamina and eitr (design C4, C6): piecewise linear in the bar
    /// fraction s, strength M at s = 0, 1 at the pivot P, 1/M at s = 1. Strength 1 (or 0 and below) is off.
    /// </summary>
    public static class RegenCurve
    {
        public static float Factor(float fraction, float strength, float pivot)
        {
            if (strength <= 0f || LarderMath.Approximately(strength, 1f))
                return 1f;
            fraction = LarderMath.Clamp01(fraction);
            pivot = LarderMath.Clamp01(pivot);
            if (fraction < pivot)
                return LarderMath.Lerp(strength, 1f, fraction / pivot);
            float above = 1f - pivot;
            return above <= 0f ? 1f : LarderMath.Lerp(1f, 1f / strength, (fraction - pivot) / above);
        }
    }
}
