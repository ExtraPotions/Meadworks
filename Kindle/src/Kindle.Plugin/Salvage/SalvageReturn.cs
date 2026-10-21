// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries; a chance for a salvage rune.
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// One material a salvage returns: the item prefab's ItemDrop and the whole amount, and its chance (1 for a
    /// material; below 1 for a salvage rune, rolled once when the stack is salvaged, see <see cref="SalvageRuneLink"/>).
    /// </summary>
    public readonly struct SalvageReturn
    {
        public SalvageReturn(ItemDrop item, int amount, float chance = 1f)
        {
            Item = item;
            Amount = amount;
            Chance = chance;
        }

        public float Chance { get; }

        public bool IsCertain => Chance >= 1f;

        public ItemDrop Item { get; }

        public int Amount { get; }

        /// <summary>The shared name token, the stacking identity of the material.</summary>
        public string Name => Item.m_itemData.m_shared.m_name;

        public int MaxStack => Item.m_itemData.m_shared.m_maxStackSize < 1 ? 1 : Item.m_itemData.m_shared.m_maxStackSize;
    }
}
