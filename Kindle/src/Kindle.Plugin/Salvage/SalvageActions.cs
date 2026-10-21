// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries; the salvage rune (SalvageRuneLink) and
// socketed gems (SalvageGemLink).
using System;
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Kindle.Runtime.Salvage;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// The public face of the module, used by the Salvage tab, the hotkey and other modules (Stow's
    /// "Trash Uses Salvage"): can an item be salvaged, what does it return, salvage it, or ask first.
    /// </summary>
    public static class SalvageActions
    {
        /// <summary>True when the local player could salvage this stack right now.</summary>
        public static bool CanSalvage(ItemDrop.ItemData item) => SalvageRules.Blocker(Player.m_localPlayer, item) == null;

        /// <summary>The "$ok_..." word that says why the local player cannot salvage the item, or null when they can.</summary>
        public static string WhyNot(ItemDrop.ItemData item) => SalvageRules.Blocker(Player.m_localPlayer, item);

        /// <summary>
        /// The materials the whole stack returns, then the rune an inscribed item may give back with its chance; empty
        /// when the item has no usable recipe.
        /// </summary>
        public static List<SalvageReturn> Returns(ItemDrop.ItemData item)
        {
            List<SalvageReturn> returns = SalvageReturns.Compute(SalvageRules.FindRecipe(item), item);
            if (returns.Count > 0)
            {
                SalvageRuneLink.AddRune(item, returns);
                SalvageGemLink.AddGems(item, returns);
            }
            return returns;
        }

        /// <summary>
        /// Salvages the whole stack: removes it from the player's inventory and adds the returns. False, with a
        /// centre message saying why, when the item cannot be salvaged or the returns would not fit; then nothing
        /// changed. The fit is checked before anything is removed; should an add still fail, the adds are rolled
        /// back and the stack is put back into its slot.
        /// </summary>
        public static bool Salvage(Player player, ItemDrop.ItemData item)
        {
            if (player == null)
                return false;
            string blocker = SalvageRules.Blocker(player, item);
            if (blocker != null)
                return Refuse(blocker);
            Inventory inventory = player.GetInventory();
            if (!inventory.ContainsItem(item))
                return Refuse(SalvageWords.Cannot);
            List<SalvageReturn> returns = Returns(item);
            if (returns.Count == 0)
                return Refuse(SalvageWords.Nothing);
            if (!SalvageInventory.Fits(inventory, item, returns))
                return Refuse(SalvageWords.NoFit);
            // The fit counted the rune and the gems as if they came; each chance is rolled once, now.
            List<SalvageReturn> rolled = SalvageRuneLink.Roll(returns);
            int lostGems = SalvageGemLink.Lost(item, rolled);
            if (!Exchange(inventory, item, rolled))
                return false;
            SalvageGemLink.ReportLost(lostGems);
            return true;
        }

        private static bool Exchange(Inventory inventory, ItemDrop.ItemData item, List<SalvageReturn> returns)
        {
            var snapshot = new SalvageInventory.Snapshot(inventory);
            string summary = Summary(item, returns);
            bool complete = false;
            try
            {
                if (!inventory.RemoveItem(item) || !SalvageInventory.Add(inventory, returns))
                    return Refuse(SalvageWords.NoFit);
                complete = true;
            }
            catch { return Refuse(SalvageWords.NoFit); }
            finally { if (!complete) snapshot.Restore(inventory); }
            Messages.TopLeft(summary);
            return true;
        }

        private static bool Refuse(string word)
        {
            Messages.Center(word);
            return false;
        }

        /// <summary>"Salvaged Iron sword ×2: 6 Iron, 2 Wood" with $tokens left for the message to localize.</summary>
        public static string Summary(ItemDrop.ItemData item, List<SalvageReturn> returns)
        {
            StringBuilder text = new StringBuilder();
            text.Append(SalvageWords.Done).Append(' ').Append(item.m_shared.m_name);
            if (item.m_stack > 1)
                text.Append(" ×").Append(item.m_stack);
            text.Append(": ").Append(ReturnList(returns));
            return text.ToString();
        }

        /// <summary>"6 $item_iron, 2 $item_wood, 1 $item_rune (25%)".</summary>
        public static string ReturnList(List<SalvageReturn> returns)
        {
            StringBuilder text = new StringBuilder();
            foreach (SalvageReturn entry in returns)
            {
                if (text.Length > 0)
                    text.Append(", ");
                text.Append(entry.Amount).Append(' ').Append(entry.Name);
                if (!entry.IsCertain)
                    text.Append(" (").Append(Percent(entry.Chance)).Append(')');
            }
            return text.ToString();
        }

        /// <summary>A chance as a whole percent, "25%".</summary>
        public static string Percent(float chance) => UnityEngine.Mathf.RoundToInt(chance * 100f) + "%";

        /// <summary>
        /// Asks with the game's yes/no popup (gamepad works), salvages on yes and calls <paramref name="afterwards"/>
        /// when the salvage succeeded. Without the popup, refuse; no unconfirmed item mutation.
        /// </summary>
        public static void Confirm(Player player, ItemDrop.ItemData item, Action afterwards)
        {
            string blocker = SalvageRules.Blocker(player, item);
            if (blocker != null)
            {
                Messages.Center(blocker);
                return;
            }
            if (!UnifiedPopup.IsAvailable())
            {
                Messages.Center(SalvageWords.Cannot);
                return;
            }
            string text = AskText(item);
            var source = Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active)
            {
                if (KitDialogs.Confirm(source, SalvageWords.Salvage, text, () => Accept(player, item, afterwards)) != null) return;
            }
            bool answered = false;
            UnifiedPopup.Push(new YesNoPopup(SalvageWords.Salvage, text, () =>
            {
                if (answered) return;
                answered = true; UnifiedPopup.Pop(); Accept(player, item, afterwards);
            }, () => { if (answered) return; answered = true; UnifiedPopup.Pop(); }));
        }

        private static void Accept(Player player, ItemDrop.ItemData item, Action afterwards)
        {
            if (Salvage(player, item))
                afterwards?.Invoke();
        }

        private static string AskText(ItemDrop.ItemData item)
        {
            StringBuilder text = new StringBuilder();
            text.Append(ItemNames.DisplayName(item));
            if (item.m_stack > 1)
                text.Append(" ×").Append(item.m_stack);
            string decoration=InscribeDetails.Lines(item);
            if(decoration.Length>0)text.Append('\n').Append(decoration);
            text.Append("\n\n").Append(SalvageWords.Ask).Append('\n').Append(ReturnList(Returns(item)));
            return text.ToString();
        }
    }
}
