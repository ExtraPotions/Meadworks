// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Kitchen/Fermenter/FermenterAddReceive.cs
// Changed by expDARE, 2026: namespace, Crucible configuration/lifecycle, owned keys and Larder-only feature dispatch.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Adding a mead base, on the barrel's ZDO owner. <see cref="Keys.RpcAddBase"/> is registered on every fermenter's
    /// ZNetView beside the game's own RPCs (Fermenter.Awake registers those only when the view has a ZDO, and so does
    /// this). The handler runs the game's RPC_AddItem, which fills the barrel only on the owner and only when it is empty
    /// and the item allowed. Whenever RPC_AddItem fills the barrel, the cook's level is written to its ZDO: the value
    /// from our RPC, or 0 when the game's own RPC_AddItem arrived from anything else.
    /// </summary>
    internal static class FermenterAddReceive
    {
        /// <summary>The level our RPC carried, while its handler runs the game's RPC_AddItem.</summary>
        private static float? receiving;

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Awake))]
        private static class Register
        {
            [HarmonyPostfix]
            private static void Postfix(Fermenter __instance)
            {
                RegisterOn(__instance);
            }
        }

        internal static void RegisterOn(Fermenter barrel)
        {
            if (!NativeCooking.Active || barrel == null) return;
            ZNetView view = barrel.m_nview;
            if (view == null || view.GetZDO() == null || view.m_functions.ContainsKey(Keys.RpcAddBase.GetStableHashCode())) return;
            view.Register<ZPackage>(Keys.RpcAddBase, (sender, package) => Receive(barrel, sender, package));
            NativeCooking.RememberRpc(view, Keys.RpcAddBase);
        }

        private static void Receive(Fermenter fermenter, long sender, ZPackage package)
        {
            NativeCooking.Run(Keys.RpcAddBase, () => AddBase(fermenter, sender, package));
        }

        private static void AddBase(Fermenter fermenter, long sender, ZPackage package)
        {
            if (fermenter == null || fermenter.m_nview == null || !fermenter.m_nview.IsValid() || !fermenter.m_nview.IsOwner())
                return;
            int nameHash = package.ReadInt();
            bool cheated = package.ReadBool();
            if (!NativeCooking.Active) { fermenter.RPC_AddItem(sender, nameHash, cheated); return; }
            receiving = package.ReadSingle();
            try
            {
                fermenter.RPC_AddItem(sender, nameHash, cheated);
            }
            finally
            {
                receiving = null;
            }
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.RPC_AddItem))]
        private static class WriteBase
        {
            [HarmonyPrefix]
            private static void Prefix(Fermenter __instance, out bool __state)
            {
                __state = __instance.m_nview != null && __instance.m_nview.IsOwner() && __instance.GetContent() == 0;
            }

            [HarmonyPostfix]
            private static void Postfix(Fermenter __instance, int nameHash, bool __state)
            {
                if (!__state || nameHash == 0 || __instance.GetContent() != nameHash)
                    return;
                FermenterBase.Write(__instance.m_nview.GetZDO(), receiving ?? 0f);
            }
        }
    }
}
