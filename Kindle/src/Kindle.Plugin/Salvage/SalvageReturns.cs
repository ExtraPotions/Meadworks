// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// The materials a stack returns. Per material: n / recipe yield x requirement amount, plus n x the cost of
    /// every upgrade level up to the item's quality when Upgrade Materials is on, times the fraction, then the
    /// rounding mode, capped at the full cost rounded up, then At Least One. Durability plays no part.
    /// </summary>
    public static class SalvageReturns
    {
        public static List<SalvageReturn> Compute(Recipe recipe, ItemDrop.ItemData item)
        {
            List<SalvageReturn> result = new List<SalvageReturn>();
            if (recipe == null || item == null)
                return result;
            float fraction = SalvageRules.Fraction(item);
            List<KeyValuePair<ItemDrop, float>> costs = new List<KeyValuePair<ItemDrop, float>>();
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement.m_resItem == null || requirement.m_upgraderResource)
                    continue;
                Accumulate(costs, requirement.m_resItem, FullCost(recipe, requirement, item));
            }
            foreach (KeyValuePair<ItemDrop, float> cost in costs)
            {
                int amount = Shrink(cost.Value, fraction);
                if (amount > 0)
                    result.Add(new SalvageReturn(cost.Key, amount));
            }
            return result;
        }

        /// <summary>The whole cost of the stack in this material, fractional when the stack is not a multiple of the yield.</summary>
        private static float FullCost(Recipe recipe, Piece.Requirement requirement, ItemDrop.ItemData item)
        {
            int yield = Mathf.Max(1, recipe.m_amount);
            float cost = (float)item.m_stack / yield * Mathf.Max(0, requirement.m_amount);
            if (SalvageSettings.UpgradeMaterials.Value)
            {
                for (int level = 2; level <= item.m_quality; level++)
                    cost += item.m_stack * Mathf.Max(0, requirement.GetAmount(level));
            }
            return cost;
        }

        private static void Accumulate(List<KeyValuePair<ItemDrop, float>> costs, ItemDrop drop, float cost)
        {
            string name = drop.m_itemData.m_shared.m_name;
            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i].Key.m_itemData.m_shared.m_name != name)
                    continue;
                costs[i] = new KeyValuePair<ItemDrop, float>(costs[i].Key, costs[i].Value + cost);
                return;
            }
            costs.Add(new KeyValuePair<ItemDrop, float>(drop, cost));
        }

        /// <summary>Fraction, rounding, the cap at the full cost, then At Least One.</summary>
        public static int Shrink(float fullCost, float fraction)
        {
            if (fullCost <= 0f)
                return 0;
            float share = fullCost * fraction;
            int amount = RoundBy(share, SalvageSettings.Rounding.Value);
            amount = Mathf.Min(amount, Mathf.CeilToInt(fullCost - 0.0001f));
            if (SalvageSettings.AtLeastOne.Value && share > 0f && amount < 1)
                amount = 1;
            return amount < 0 ? 0 : amount;
        }

        private static int RoundBy(float value, RoundingMode mode)
        {
            switch (mode)
            {
                case RoundingMode.Floor:
                    return Mathf.FloorToInt(value + 0.0001f);
                case RoundingMode.Ceil:
                    return Mathf.CeilToInt(value - 0.0001f);
                default:
                    return (int)Math.Floor(value + 0.5f);
            }
        }
    }
}
