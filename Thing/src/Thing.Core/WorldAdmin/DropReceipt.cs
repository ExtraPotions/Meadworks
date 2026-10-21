// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: classify actual-owner cleanup receipts without mistaking partial or unknown mutation for refusal.
namespace ExtraPotions.Thing.WorldAdmin
{
    public enum DropReceiptOutcome { Continue, Refused, Recovery }

    public static class DropReceipt
    {
        public static DropReceiptOutcome Decide(string status, int removed, int approved, int previousRemoved)
        {
            if (removed < 0 || approved < 0 || previousRemoved < 0 || removed > approved || (status != "ok" && status != "refused"))
                return DropReceiptOutcome.Recovery;
            if (status == "ok") return DropReceiptOutcome.Continue;
            return previousRemoved > 0 || removed > 0 ? DropReceiptOutcome.Recovery : DropReceiptOutcome.Refused;
        }
    }
}
