// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Barter.Runtime
{
    internal static class BarterOperations
    {
        internal const string ReserveId = "meadworks.barter.reserve";
        internal const string FinishId = "meadworks.barter.finish";
        internal const string EditListId = "meadworks.barter.edit_list";
        internal const string RecoverId = "meadworks.barter.recover";

        private static readonly System.Version Protocol = new System.Version(1, 0);
        internal static readonly OperationDescriptor Reserve = new OperationDescriptor(ReserveId, Protocol);
        internal static readonly OperationDescriptor Finish = new OperationDescriptor(FinishId, Protocol);
        internal static readonly OperationDescriptor EditList = new OperationDescriptor(EditListId, Protocol);
        internal static readonly OperationDescriptor Recover = new OperationDescriptor(RecoverId, Protocol);
    }
}
