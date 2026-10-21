// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Selectively adapted from aedenthorn/ValheimMods 83690e8ef9a37c4694604416667d252b4ff5f645:
// BuildPieceTweaks/BepInExPlugin.cs, support-wear field assignment only (line 142).
// Upstream root public-domain dedication retained in THIRD_PARTY_NOTICES/aedenthorn-public-domain.txt.
// Changed by expDARE, 2026: isolate the setter as a scoped, reversible per-instance operation; no prefab mutation.
namespace ExtraPotions.Timber.Adopted
{
    public static class SupportWearKernel
    {
        public static bool Swap(ref bool field, bool noSupportWear)
        {
            bool original = field;
            field = noSupportWear;
            return original;
        }

        public static void Restore(ref bool field, bool original)
        {
            // Our temporary value is false. Preserve a distinct value written by another patch.
            if (!field) field = original;
        }
    }
}
