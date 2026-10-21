// OpenKeep. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: adapted pin 3bb778330f23645ccddb42fbc0c6ffab72c842b6; Timber lifecycle/C01 and captured center identity.
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Timber.Camera;
using UnityEngine;
using ExtraPotions.Timber.Runtime;
using Plugin = ExtraPotions.Timber.Runtime.Plugin;

namespace Timber.Repair
{
    internal static class AreaRepairModule
    {
        internal const int MaxRepairs = 64;
        static CameraBindings? bindings;
        static CameraValue<bool>? enabled;

        internal static void Initialize(ConfigFile config)
        {
            Reset();
            bindings = new CameraBindings(config);
            enabled = bindings.Bind("Repair", "Area Repair", false,
                "After one successful hammer repair, repair damaged pieces touching that piece (at most 64), without spreading through the building. "
                + "Each neighbor needs its native crafting station and ward access. Only the original swing costs stamina, eitr and hammer wear. "
                + "A healthy or refused center repairs no neighbors. Timber steps aside while OpenKeep's Area Repair is active.");
        }

        internal static void Reset()
        {
            bindings?.Dispose(); bindings = null; enabled = null;
            RepairNeighbours.Reset();
        }

        internal static bool Allowed(Player player)
        {
            if (!Plugin.Active || !Plugin.AllowsUi || enabled == null || !enabled.Value
                || !player || player != Player.m_localPlayer || player.IsDead() || player.InCutscene() || player.IsTeleporting()) return false;
            var view = player.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner()) return false;
            if (!Chainloader.PluginInfos.TryGetValue("milkyteam.openkeep", out var source)) return true;
            var plugin = source.Instance as BaseUnityPlugin;
            return SourceAllows(plugin ? plugin.Config : null);
        }

        internal static bool SourceAllows(ConfigFile? source)
            => source != null && source.TryGetEntry<bool>(new ConfigDefinition("8. Homestead", "Area Repair"), out var own) && !own.Value;

        internal static void Run(Player player, WearNTear center)
        {
            int repaired = 0;
            foreach (var wear in RepairNeighbours.Around(center))
            {
                if (repaired >= MaxRepairs) break;
                if (!wear || !wear.m_piece || !wear.m_nview || !wear.m_nview.IsValid()) continue;
                var piece = wear.m_piece;
                if (!StationAllows(player, piece)
                    || !PrivateArea.CheckAccess(piece.transform.position, 0f, flash: false, wardCheck: false)
                    || !wear.Repair()) continue;
                piece.m_placeEffect?.Create(piece.transform.position, piece.transform.rotation, null, 1f, -1, player.GetZDOID());
                repaired++;
            }
            if (repaired > 0) player.Message(MessageHud.MessageType.TopLeft, TimberText.Format("area_repair_done", repaired));
        }

        static bool StationAllows(Player player, Piece piece)
            => player.PlacementCostDisabled || !piece.m_craftingStation
                || CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, player.transform.position)
                || (ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench));
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
    internal static class AreaRepairPatch
    {
        internal struct Attempt { internal WearNTear Center; internal float Stamp; }

        [HarmonyPrefix]
        static void Prefix(Player __instance, out Attempt __state)
        {
            __state = default;
            if (!AreaRepairModule.Allowed(__instance)) return;
            var piece = __instance.GetHoveringPiece();
            var wear = piece ? piece.GetComponent<WearNTear>() : null;
            if (wear) __state = new Attempt { Center = wear, Stamp = wear.m_lastRepair };
        }

        [HarmonyPostfix]
        static void Postfix(Player __instance, Attempt __state, bool __runOriginal)
        {
            if (!__runOriginal || !__state.Center || !AreaRepairModule.Allowed(__instance)) return;
            var piece = __instance.GetHoveringPiece();
            if (!piece || piece.GetComponent<WearNTear>() != __state.Center || __state.Center.m_lastRepair <= __state.Stamp) return;
            try { AreaRepairModule.Run(__instance, __state.Center); }
            catch (System.Exception error) { Debug.LogWarning("Timber area repair: " + error.GetType().Name + ": " + error.Message); }
        }
    }
}
