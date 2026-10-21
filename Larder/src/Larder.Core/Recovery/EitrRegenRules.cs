// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/EitrRegen.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the SEMan.ModifyEitrRegen postfix became a pure function.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Recovery
{
    /// <summary>
    /// Eitr regeneration (design C6): Eitr Vigor is added to the status effect multiplier, then the eitr curve and the
    /// blocking factor (relative to the game's 0.8) scale it. The game adds its equipment eitr regen afterwards.
    /// </summary>
    public static class EitrRegenRules
    {
        public static float HookMultiplier(float statusEffectMultiplier, float eitrVigorPercent, float curveFactor, bool blocking, float blockingFactor)
        {
            float multiplier = statusEffectMultiplier + eitrVigorPercent / 100f;
            multiplier *= curveFactor;
            if (blocking)
                multiplier *= blockingFactor / StaminaRegenRules.GameBlockingRegenFactor;
            return multiplier;
        }
    }
}
