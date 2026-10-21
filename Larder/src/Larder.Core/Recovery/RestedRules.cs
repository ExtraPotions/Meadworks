// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1, FeastMaster 4.7.4:
// FeastMaster/FeastMaster/FeastMasterCore/Rested.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Larder; the documented duration rule (base duration plus duration per comfort
// level above 1) became a pure function; the asset writes stay in the plugin.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Recovery
{
    /// <summary>
    /// The Rested effect (design C7): base duration at comfort level 1 plus the duration per comfort level above 1.
    /// Multipliers apply at once; a new duration from the next rest.
    /// </summary>
    public static class RestedRules
    {
        public static float Duration(float baseDuration, float perComfort, int comfort)
        {
            int above = comfort > 1 ? comfort - 1 : 0;
            return baseDuration + perComfort * above;
        }
    }
}
