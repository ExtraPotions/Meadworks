// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what the sea gate modules (adapted from Wayfare) need from Threshold: settings, log, words, the
// sender of a routed call, gate access by Threshold's portal rules and keys, the world's portal blocks, the Harmony
// install and the per-frame driver.
using System;
using System.Collections.Generic;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>One setting as the sea gate modules read it.</summary>
    public sealed class SeaGateValue<T>
    {
        readonly Func<T> read;
        internal SeaGateValue(Func<T> read, T fallback)
        {
            this.read = read;
            DefaultValue = fallback;
        }

        public T Value
        {
            get
            {
                try { return read(); }
                catch (Exception) { return DefaultValue; }
            }
        }

        public T DefaultValue { get; }
    }

    /// <summary>Section "8 - Sea Gates" (default off, U8) and the cheat. <see cref="On"/>: Threshold routes and sea gates
    /// are enabled (the server's value in multiplayer).</summary>
    public static class SeaGateConfig
    {
        static T Get<T>(ThresholdSetting<T> setting) => Plugin.Settings != null ? Plugin.Settings.Get(setting) : setting.Default;

        public static bool On => ThresholdModules.Active() && Get(ThresholdSettings.SeaGates.Enabled);

        public static readonly SeaGateValue<string> PillarRecipe = new SeaGateValue<string>(() => Get(ThresholdSettings.SeaGates.PillarRecipe), ThresholdSettings.SeaGates.PillarRecipe.Default);
        public static readonly SeaGateValue<float> MinGateWidth = new SeaGateValue<float>(() => Math.Min(Get(ThresholdSettings.SeaGates.MinGateWidth), Get(ThresholdSettings.SeaGates.MaxGateWidth)), 10f);
        public static readonly SeaGateValue<float> MaxGateWidth = new SeaGateValue<float>(() => Math.Max(Get(ThresholdSettings.SeaGates.MinGateWidth), Get(ThresholdSettings.SeaGates.MaxGateWidth)), 15f);
        public static readonly SeaGateValue<float> MinWaterDepth = new SeaGateValue<float>(() => Get(ThresholdSettings.SeaGates.MinWaterDepth), 1f);
        public static readonly SeaGateValue<float> ProtectionSeconds = new SeaGateValue<float>(() => Get(ThresholdSettings.SeaGates.ProtectionSeconds), 5f);
        public static readonly SeaGateValue<float> CrewWaitSeconds = new SeaGateValue<float>(() => Get(ThresholdSettings.SeaGates.CrewWaitSeconds), 20f);

        /// <summary>The Ignore Teleport Restrictions cheat in effect: restricted cargo passes a sea gate too.</summary>
        public static readonly SeaGateValue<bool> AllowRestrictedCargo = new SeaGateValue<bool>(() => Plugin.Settings != null && Plugin.Settings.IgnoreTeleportRestrictions, false);
    }

    /// <summary>Threshold's log for the sea gate modules: informational lines are verbose (low console spam).</summary>
    public static class SeaGateLog
    {
        public static void LogInfo(string message) => Plugin.Log?.Verbose("Threshold sea gates: " + message);
        public static void LogWarning(string message) => Plugin.Log?.Warning("Threshold sea gates: " + message);
        public static void LogError(string message) => Plugin.Log?.Error("Threshold sea gates: " + message);
    }

    /// <summary>The sea gate words: Threshold catalog tokens (<c>$meadworks_threshold_sg_*</c>, seven languages). A
    /// Wayfare key <c>wf_sg_x</c> maps to the Threshold key <c>sg_x</c>; the English text lives in the catalogs.</summary>
    public static class Language
    {
        /// <summary>The language revision text cached once compares against; the catalog follows language changes itself.</summary>
        public static int Revision => 0;

        public static string Add(string key, string english) => "$" + ThresholdCore.TokenPrefix + Key(key);

        public static string Key(string wayfareKey) => wayfareKey.StartsWith("wf_", StringComparison.Ordinal) ? wayfareKey.Substring(3) : wayfareKey;
    }

    /// <summary>A word localized for text drawn every frame.</summary>
    internal sealed class LocalWord
    {
        readonly string token;
        internal LocalWord(string token) => this.token = token;
        internal string Text => global::Localization.instance != null ? global::Localization.instance.Localize(token) : token;
    }

    /// <summary>Who sent a routed call (the same resolution as Threshold's portal edits): a connected peer's player
    /// (read from the player's own ZDO), this machine's own player for a call it routed to itself, else nobody.</summary>
    public static class SenderIdentity
    {
        public static long PlayerId(long sender)
        {
            var peer = ZNet.instance != null ? ZNet.instance.GetPeer(sender) : null;
            if (peer != null) return PeerPlayerId(peer);
            bool self = ZRoutedRpc.instance != null && sender == ZRoutedRpc.instance.m_id;
            return self && Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;
        }

        public static long PeerPlayerId(ZNetPeer peer)
        {
            if (peer.m_playerID != 0L) return peer.m_playerID;
            if (peer.m_characterID.IsNone() || ZDOMan.instance == null) return 0L;
            var character = ZDOMan.instance.GetZDO(peer.m_characterID);
            return character != null ? character.GetLong(ZDOVars.s_playerID) : 0L;
        }

        public static bool IsFromServer(long sender)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return false;
            if (ZNet.instance.IsServer()) return sender == ZRoutedRpc.instance.m_id;
            var server = ZNet.instance.GetServerPeer();
            return server != null && sender == server.m_uid;
        }

        public static bool IsAdmin(long sender)
        {
            if (ZNet.instance == null) return false;
            var peer = ZNet.instance.GetPeer(sender);
            if (peer != null) return peer.m_socket != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
            bool self = ZRoutedRpc.instance != null && sender == ZRoutedRpc.instance.m_id;
            return self && ZNet.instance.LocalPlayerIsAdminOrHost();
        }
    }

    /// <summary>A gate's access mode and owner live on its anchor pillar under Threshold's portal keys
    /// (<c>threshold.mode</c>, <c>threshold.owner</c>) and follow the portal rules (<see cref="PortalAccessRules"/>):
    /// Public, Private, Admin; admins may target every gate (U9); an owned gate keeps its owner when an admin cycles it
    /// (runtime decision 5).</summary>
    public static class GateAccess
    {
        static readonly int ModeHash = PortalFields.ModeKey.GetStableHashCode();
        static readonly int OwnerHash = PortalFields.OwnerKey.GetStableHashCode();

        static bool ModesOn => Plugin.Settings == null || Plugin.Settings.Get(ThresholdSettings.Access.AccessModesEnabled);

        public static AccessMode GetMode(ZDO zdo) => zdo == null ? AccessMode.Public : PortalFields.Mode(zdo.GetInt(ModeHash, PortalFields.NoMode));

        public static long GetOwner(ZDO zdo) => zdo != null ? zdo.GetLong(OwnerHash, 0L) : 0L;

        public static bool MayTarget(ZDO zdo, long playerId, bool isAdmin) => MayTarget(GetMode(zdo), GetOwner(zdo), playerId, isAdmin);

        public static bool MayTarget(AccessMode mode, long owner, long playerId, bool isAdmin) =>
            PortalAccessRules.MayTarget(mode, owner, new AccessViewer(playerId, isAdmin), ModesOn);

        public static bool MayCycle(ZDO zdo, long playerId, bool isAdmin) =>
            PortalAccessRules.MayCycle(GetMode(zdo), GetOwner(zdo), new AccessViewer(playerId, isAdmin));

        public static AccessMode NextMode(AccessMode mode, bool isAdmin) => PortalAccessRules.NextMode(mode, new AccessViewer(0L, isAdmin));

        public static bool MaySet(long raw, bool isAdmin) =>
            raw >= 0 && raw <= int.MaxValue && PortalAccessRules.MaySet((AccessMode)(int)raw, new AccessViewer(0L, isAdmin));

        /// <summary>The cycled mode; an unowned gate is claimed by the player, an owned one keeps its owner.</summary>
        public static void SetModeAndOwner(ZDO zdo, AccessMode mode, long playerId)
        {
            zdo.Set(ModeHash, (int)mode);
            if (GetOwner(zdo) == 0L && playerId != 0L) zdo.Set(OwnerHash, playerId);
        }

        /// <summary>The world's no-portals key or a boss key with a boss alive: no sea gate either (the cheat never lifts it).</summary>
        public static bool Blocked(out string reason)
        {
            reason = TravelRules.TextKey(TravelRefusal.WorldBlocked);
            var zones = ZoneSystem.instance;
            if (zones == null) return false;
            if (zones.GetGlobalKey(GlobalKeys.NoPortals)) return true;
            float bosses = 0f;
            if (TravelRules.BossBlocked(zones.GetGlobalKey(GlobalKeys.NoBossPortals),
                    RandEventSystem.instance != null && RandEventSystem.instance.GetBossEvent() != null,
                    zones.GetGlobalKey(GlobalKeys.activeBosses, out bosses) ? bosses : 0f))
            {
                reason = TravelRules.TextKey(TravelRefusal.BossBlocked);
                return true;
            }
            return false;
        }

        /// <summary>The Alt hover line of a gate: the game's alternative use key and the gate's access mode.</summary>
        public static string CycleLine(AccessMode mode) =>
            "[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] " + ThresholdText.Text(ThresholdTextKeys.Mode(mode));
    }

    /// <summary>Installs the sea gate patches and runs the per-frame modules (from the plugin's Update), each guarded
    /// on its own. The pillar prefab is installed whether sea gates are on or not, so pillars already built load.</summary>
    public static class SeaGates
    {
        static readonly HashSet<string> failed = new HashSet<string>();
        static bool surfacesShown;

        static readonly Type[] PatchClasses =
        {
            typeof(SeaGatePiece.SceneAwake), typeof(SeaGatePiece.DatabaseAwake), typeof(SeaGateFootingPlacePatch), typeof(PillarGhostPatch),
            typeof(PillarDestroyPatch), typeof(PillarIndexPatch), typeof(SeaGateGrantTeardownPatch), typeof(ShipJumpAwakePatch),
            typeof(ShipFreezeFixedUpdatePatch), typeof(ShipFreezeDamagePatch), typeof(CrewHoldDeathPatch), typeof(CrewHoldDestroyPatch),
            typeof(CrewLogoutPatch), typeof(JumpProtectionHitPatch), typeof(JumpProtectionDamagePatch), typeof(SeaGateSurfaceTeardownPatch),
            typeof(SeaGateRpcPatch),
        };

        public static void Patch(Harmony harmony)
        {
            foreach (var type in PatchClasses) harmony.CreateClassProcessor(type).Patch();
            SeaGatePiece.Install();
            SeaGateRpcPatch.Postfix();
        }

        /// <summary>Every frame on every machine. Jumps under way run to their release whatever the settings say.</summary>
        public static void Tick()
        {
            if (ZNet.instance == null) return;
            bool on = SeaGateConfig.On;
            Run("recipe", SeaGatePieceRecipe.Tick);
            Run("ship jump", ShipJump.Tick);
            if (on)
            {
                Run("pairing", SeaGatePairing.Tick);
                Run("index", SeaGateIndexServer.Tick);
            }
            if (!on || Player.m_localPlayer == null)
            {
                if (surfacesShown) Run("surface", SeaGateSurface.Clear);
                surfacesShown = false;
                return;
            }
            surfacesShown = true;
            Run("surface", SeaGateSurface.Tick);
            Run("cargo", CargoCheck.Tick);
        }

        static void Run(string name, Action tick)
        {
            try { tick(); }
            catch (Exception e)
            {
                if (failed.Add(name)) SeaGateLog.LogError(name + " failed (logged once): " + e);
            }
        }

        public static void Reset()
        {
            try { SeaGateSurface.Clear(); } catch (Exception) { }
            SeaGateGrant.Clear();
            surfacesShown = false;
        }
    }

    /// <summary>Every new session's <c>ZRoutedRpc</c> gets the sea gate calls at once, on every machine, a dedicated
    /// server included, before the first client can send one.</summary>
    [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, new[] { typeof(bool) })]
    public static class SeaGateRpcPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SeaGateIndex.EnsureRegistered();
            SeaGateGrant.EnsureRegistered();
            CrewJump.EnsureRegistered();
        }
    }
}
