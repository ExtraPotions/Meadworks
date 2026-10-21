// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a character ZDO is written only by the peer that owns that character.
namespace ExtraPotions.Edge.Secondaries
{
    public static class OwnedCharacterWrite
    {
        public static bool Allowed(bool viewValid, bool isOwner) => viewValid && isOwner;
    }
}
