// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Lore/GrowthLore.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: active Shepherd growth factors, C05 strings and stored young/egg lineage; native egg warmth text retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Breeding;
using ExtraPotions.Shepherd.Runtime.Integration;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public static class GrowthLore
    {
        private static double Factor(UnityEngine.Vector3 position) => NativeBreeding.PaceActive
            ? BreedingPace.GrowthFactor(HusbandryBreedingSettings.GrowthSpeed.Value, Keeper.BestLevel(position)) : 1;
        public static string YoungLine(Character young)
        {
            var growup = young != null ? young.GetComponent<Growup>() : null;
            var ai = young != null ? young.GetBaseAI() : null;
            if (growup == null || ai == null || !young!.IsTamed()) return "";
            double left = growup.m_growTime / Factor(young.transform.position) - ai.GetTimeSinceSpawned().TotalSeconds;
            string line = left > 0 ? ShepherdLocalization.Format("lore_growing", LoreText.Duration(left)) : ShepherdLocalization.Text("lore_grown_due");
            var view = young.m_nview;
            return view != null && view.IsValid() ? LoreText.Add(line, BreedingLore.LineageLines(BreedingStore.ReadLineage(new ZdoKeyStore(view.GetZDO())))) : line;
        }
        public static string EggLine(EggGrow egg)
        {
            var view = egg.m_nview;
            if (view == null || !view.IsValid() || egg.m_item == null || egg.m_item.m_itemData.m_stack > 1) return "";
            string line = "";
            float start = view.GetZDO().GetFloat(ZDOVars.s_growStart);
            if (start > 0 && ZNet.instance != null)
            {
                double left = start + egg.m_growTime / Factor(egg.transform.position) - ZNet.instance.GetTimeSeconds();
                line = left > 0 ? ShepherdLocalization.Format("lore_hatching", LoreText.Duration(left)) : ShepherdLocalization.Text("lore_hatched_due");
            }
            if (egg.m_item.m_itemData.m_customData.TryGetValue(BreedingKeys.Egg, out var data) && EggCodec.TryDecode(data, out var record) && record != null)
                line = LoreText.Add(line, BreedingLore.LineageLines(Lineage.Hatch(record, "")));
            return line;
        }
    }
}
