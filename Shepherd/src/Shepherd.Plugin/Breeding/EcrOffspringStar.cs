// Adapted from GrindstoneSkills/GrindstoneSkills/Husbandry/Breeding/OffspringStar.cs, Begin/Added/End EcrKey route.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: ECR route only, caller-owned birth scope; preserve a pre-existing foreign key.
// SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    internal static class EcrOffspringStar
    {
        const string Key = "grindstone_star_up";
        internal static bool Begin(Procreation parent)
        {
            var view = parent.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner() || view.GetZDO().GetInt(Key) != 0) return false;
            view.GetZDO().Set(Key, 1); return true;
        }
        internal static bool Added(Procreation parent, bool armed) => armed && parent.m_nview != null && parent.m_nview.IsValid()
            && parent.m_nview.GetZDO().GetInt(Key) == 1;
        internal static void End(Procreation parent, bool armed)
        {
            if (armed && parent.m_nview != null && parent.m_nview.IsValid() && parent.m_nview.IsOwner()) parent.m_nview.GetZDO().Set(Key, 0);
        }
    }
}
