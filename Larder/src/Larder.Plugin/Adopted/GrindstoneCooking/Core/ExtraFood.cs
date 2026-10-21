// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Core/ExtraFood.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// The game rolls its bonus food from InventoryGui.m_craftBonusChance times the skill factor, adding
    /// m_craftBonusAmount; the workbench's Crafting bonus reads the same two fields. At a kitchen they are replaced
    /// for one call (a cooking station's OnInteract, or DoCrafting at a kitchen crafting station) so the chance
    /// becomes "Extra Food Chance At Level 100" times the skill factor, then put back. Call Apply in a prefix and
    /// Restore in a finalizer or postfix with the returned state.
    /// </summary>
    public static class ExtraFood
    {
        public struct State
        {
            public bool Applied;
            public float Chance;
            public int Amount;
        }

        public static State Apply()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null)
                return default;
            State state = new State { Applied = true, Chance = gui.m_craftBonusChance, Amount = gui.m_craftBonusAmount };
            gui.m_craftBonusChance = UnityEngine.Mathf.Clamp01(KitchenSettings.ExtraFoodChance.Value / 100f);
            gui.m_craftBonusAmount = Perks.ExtraFoodAmount;
            return state;
        }

        public static void Restore(State state)
        {
            InventoryGui gui = InventoryGui.instance;
            if (!state.Applied || gui == null)
                return;
            gui.m_craftBonusChance = state.Chance;
            gui.m_craftBonusAmount = state.Amount;
        }
    }
}
