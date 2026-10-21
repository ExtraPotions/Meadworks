// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterAddSend.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Adding a mead base, on the client that adds it. The game's Fermenter.AddItem checks that the barrel is empty and
    /// the item allowed, removes one from the inventory, then sends "RPC_AddItem"(int nameHash, bool cheated) to the
    /// barrel's owner through ZNetView.InvokeRPC. While AddItem runs, that one send is swapped for
    /// <see cref="Keys.RpcAddBase"/>, which also carries the local Cooking level. Every check, the removal and the
    /// return value stay the game's (and any other mod's changes to them). A send that does not look like the game's
    /// (another name, view or payload) goes out untouched. The owner's side is <see cref="FermenterAddReceive"/>.
    /// </summary>
    internal static class FermenterAddSend
    {
        private const string VanillaRpc = "RPC_AddItem";

        /// <summary>The add in progress: the barrel's view and the cook's level.</summary>
        private sealed class Adding
        {
            public ZNetView View;
            public float Level;
        }

        private static Adding adding;

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.AddItem))]
        private static class AddItemScope
        {
            [HarmonyPrefix]
            private static void Prefix(Fermenter __instance)
            {
                adding = new Adding { View = __instance.m_nview, Level = CookLevel.Local() };
            }

            [HarmonyFinalizer]
            private static void Finalizer() => adding = null;
        }

        /// <summary>
        /// From <see cref="RpcSendSwap"/> (ZNetView.InvokeRPC): true when this send was the game's add and went out as
        /// ours instead, so the game's own send is skipped.
        /// </summary>
        internal static bool Swapped(ZNetView view, string method, object[] parameters)
        {
            Adding add = adding;
            if (!NativeCooking.Active || add == null || !ReferenceEquals(add.View, view) || method != VanillaRpc)
                return false;
            if (parameters == null || parameters.Length != 2 || !(parameters[0] is int nameHash) || !(parameters[1] is bool cheated))
                return false;
            adding = null;
            view.InvokeRPC(Keys.RpcAddBase, Package(nameHash, cheated, add));
            return true;
        }

        /// <summary>The payload in the order <see cref="Keys.RpcAddBase"/> documents.</summary>
        private static ZPackage Package(int nameHash, bool cheated, Adding add)
        {
            ZPackage package = new ZPackage();
            package.Write(nameHash);
            package.Write(cheated);
            package.Write(add.Level);
            return package;
        }
    }
}
