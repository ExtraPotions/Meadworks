// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Behavior reference only (Valheim_Admin_Tool 0.5.8 "Virtual workbenches"); no source is copied.
using System;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>
    /// Virtual Workbench: the key reopens the real crafting station the player used last, from anywhere that station is
    /// still loaded. Nothing is built or copied: the open menu is that station's own, so its level, extensions,
    /// CheckUsable roof/fire rules and the game's crafting path (materials from the player's inventory only) are unchanged.
    /// Only the use-distance check is relaxed, and only for that one station while its menu was opened this way.
    /// </summary>
    public static class KindleVirtualStation
    {
        static KindleConfig? settings;
        static Harmony? patches;
        static ZDOID remembered = ZDOID.None;
        static CraftingStation? active;

        internal static bool Enabled => Plugin.GameplayAvailable && settings != null && settings.Get(KindleSettings.VirtualWorkbench.Enabled);

        public static void Initialize(KindleConfig config)
        {
            Shutdown(); settings = config;
            patches = new Harmony(Plugin.Guid + ".virtualstation");
            patches.CreateClassProcessor(typeof(RememberPatch)).Patch();
            patches.CreateClassProcessor(typeof(DistancePatch)).Patch();
        }

        public static void Shutdown()
        {
            Release(true);
            patches?.UnpatchSelf(); patches = null;
            settings = null; remembered = ZDOID.None;
        }

        public static void Tick()
        {
            if (settings == null) return;
            var player = Player.m_localPlayer;
            if (active != null)
            {
                // The game clears the station when the menu closes; host turning the option off closes it at once.
                if (!player || player.GetCurrentCraftingStation() != active) active = null;
                else if (!Enabled) Release(true);
            }
            if (!player || InventoryGui.IsVisible() || !MeadworksInput.Allows(InputContext.Gameplay)) return;
            if (!settings.Shortcut(KindleSettings.VirtualWorkbench.Key).IsDown()) return;
            if (!Enabled) { player.Message(MessageHud.MessageType.Center, KindleText.Text("virtualoff")); return; }
            Open(player);
        }

        static void Open(Player player)
        {
            if (player.IsDead() || player.IsTeleporting() || player.InCutscene() || player.InPlaceMode()) return;
            var station = Find(remembered);
            if (!station) { player.Message(MessageHud.MessageType.Center, KindleText.Text("virtualnone")); return; }
            // The station's own roof and fire rules still decide, with the game's own message.
            if (!station!.CheckUsable(player, true)) return;
            active = station;
            player.SetCraftingStation(station);
            InventoryGui.instance.Show(null, 3);
        }

        static CraftingStation? Find(ZDOID id)
        {
            if (id == ZDOID.None || ZNetScene.instance == null) return null;
            var instance = ZNetScene.instance.FindInstance(id);
            return instance ? instance.GetComponentInChildren<CraftingStation>() : null;
        }

        static void Release(bool close)
        {
            var player = Player.m_localPlayer;
            if (close && active && player && player.GetCurrentCraftingStation() == active)
            {
                player.SetCraftingStation(null);
                if (InventoryGui.instance && InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            }
            active = null;
        }

        /// <summary>Remembers a world station the local player really stood at; Kindle's cart stations are excluded.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.SetCraftingStation))]
        static class RememberPatch
        {
            [HarmonyPostfix]
            static void Postfix(Player __instance, CraftingStation station)
            {
                try
                {
                    if (__instance != Player.m_localPlayer || !station || station == active || KindleCarts.IsCart(station)) return;
                    var view = station.GetComponentInParent<ZNetView>();
                    var zdo = view && view.IsValid() ? view.GetZDO() : null;
                    if (zdo != null) remembered = zdo.m_uid;
                }
                catch (Exception error) { KindleSalvage.Warn("Virtual Workbench: " + error.GetType().Name + ": " + error.Message); }
            }
        }

        /// <summary>Use distance passes only for the station opened through the key, only for the local player.</summary>
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.InUseDistance))]
        static class DistancePatch
        {
            [HarmonyPrefix]
            static bool Prefix(CraftingStation __instance, Humanoid human, ref bool __result)
            {
                if (active == null || __instance != active || human != Player.m_localPlayer || !Enabled) return true;
                __result = true; return false;
            }
        }
    }
}
