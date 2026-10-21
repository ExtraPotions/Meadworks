using System.Collections.Generic;
using HarmonyLib;
using ExtraPotions.Burrow.Embedded.PatchGuard;
using UnityEngine;

namespace Burrow.Core
{
    /// <summary>OpenKeep handover adoption: the owner sends its current ZDO before granting an unused chest.</summary>
    public static class HandOver
    {
        public const string Rpc = "Burrow_HandOver";
        public const float ActionSeconds = 1f;
        public const float BackgroundSeconds = 30f;
        private static readonly Dictionary<Container, float> asked = new Dictionary<Container, float>();
        public static bool Ask(Container chest, float every)
        {
            if (!ContainerScan.IsRemote(chest) || ContainerScan.IsShip(chest) || ContainerScan.IsCart(chest)) return false;
            if (asked.TryGetValue(chest, out float at) && Time.time - at < every) return false;
            if (asked.Count > 256) asked.Clear();
            asked[chest] = Time.time;
            chest.m_nview.InvokeRPC(Rpc);
            return true;
        }
        private static void Handle(Container chest, long sender)
        {
            ZNetView view = chest.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner() || sender == ZDOMan.GetSessionID()) return;
            if (InUse(chest) || ContainerClaim.Blocked(chest) || ContainerScan.IsShip(chest) || ContainerScan.IsCart(chest)) return;
            // Match the native RPC sender to its real character ZDO; never accept a claimed actor ID.
            long actor = SenderActor(sender);
            if (!ContainerScan.MayHandOver(chest, actor)) return;
            ZDO zdo = view.GetZDO();
            ZDOMan.instance.ForceSendZDO(sender, zdo.m_uid);
            zdo.SetOwner(sender);
            Plugin.Log.LogDebug($"Burrow: handed {ContainerScan.PrefabName(chest)} over to peer {sender}");
        }
        private static long SenderActor(long sender)
        {
            if (!ZNet.instance || ZDOMan.instance == null || !ZNetScene.instance || sender == 0) return 0;
            foreach (var info in ZNet.instance.GetPlayerList())
            {
                ZDO character = ZDOMan.instance.GetZDO(info.m_characterID);
                if (character == null || character.GetOwner() != sender || character.GetBool(ZDOVars.s_dead, false)) continue;
                GameObject prefab = ZNetScene.instance.GetPrefab(character.GetPrefab());
                if (!prefab || !prefab.GetComponent<Player>()) continue;
                return character.GetLong(ZDOVars.s_playerID, 0);
            }
            return 0;
        }
        private static bool InUse(Container chest) => chest.IsInUse() || chest.m_wagon != null && chest.m_wagon.InUse() || chest.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
        [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
        private static class RegisterPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Container __instance)
            {
                ZNetView view = __instance.m_nview;
                if (view == null || view.GetZDO() == null || view.m_functions == null || view.m_functions.ContainsKey(Rpc.GetStableHashCode())) return;
                Container chest = __instance;
                view.Register(Rpc, sender => Guard.Run("hand over", () => Handle(chest, sender)));
            }
        }
    }
}
