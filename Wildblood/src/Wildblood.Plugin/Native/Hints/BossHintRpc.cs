// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Hints
{
    /// <summary>
    /// Boss hints over the game's routed-RPC bus. The boss's owner, as it dies, asks the server - the one machine whose
    /// world knows where every location is - naming the boss and where it fell. The server takes that boss's altar nearest
    /// the fall (the one it was summoned at; the fall itself when no altar of its kind is near, as for a boss spawned by
    /// command), finds the target nearest that altar, and sends the compass direction between the two, with the target's
    /// place, to every machine. Each player near the fall with hints on who has not yet found the target (its spot is
    /// still unexplored on their map, their own exploring and a cartography table's both count) shows it a few seconds
    /// later, after the game's and the tier's messages. A world without the target (one generated before that place came
    /// to the game) says nothing.
    /// </summary>
    internal static class BossHintRpc
    {
        public const string Ask = "ecr_boss_hint_ask";
        public const string Hint = "ecr_boss_hint";

        /// <summary>Metres from the fall within which a player feels the pull: the fight, as for the boss trophies.</summary>
        private const float PlayerRange = 100f;

        /// <summary>Metres from the fall within which an altar counts as the one the boss was summoned at.</summary>
        private const float AltarReach = 500f;

        /// <summary>Seconds in which another fall of the same boss at the same place says nothing: a Twin's partner.</summary>
        private const double RepeatSeconds = 30.0;

        /// <summary>Seconds from the fall to the message, so it is not drawn over the tier's or the game's own.</summary>
        private const float ShowDelay = 6f;

        private static readonly Dictionary<string, double> LastSent = new Dictionary<string, double>();
        private static ZRoutedRpc? _registeredOn;

        /// <summary>Registers both handlers once per routed-RPC bus. Called at world start on every machine.</summary>
        public static void EnsureRegistered() => TryEnsureRegistered();

        public static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || !NativeBossMessages.Active)
            {
                return false;
            }
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.BossHints, () => {
                LastSent.Clear();
                bus.Register<ZPackage>(Ask, OnAsk);
                bus.Register<Vector3, int, Vector3>(Hint, OnHint);
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Ask, Hint)) return false;
            _registeredOn = bus;
            return true;
        }

        /// <summary>Owner side, as a boss dies: asks the server for its hint, when the boss leaves one.</summary>
        public static void Request(ZDOID source, string boss, Vector3 fell)
        {
            if (ZRoutedRpc.instance == null || !BossHintTable.TryGet(boss, out _))
            {
                return;
            }
            if (!TryEnsureRegistered()) return;
            var package = new ZPackage(); package.Write(1); package.Write(source); package.Write(boss); package.Write(fell);
            ZRoutedRpc.instance.InvokeRoutedRPC(Ask, package);
        }

        private static void OnAsk(long sender, ZPackage package) => Guard.Run("BossHintRpc.OnAsk", () =>
        {
            if (!NativeBossMessages.Current(_registeredOn, NativeRpcOwner.BossHints) || !NativeBossMessages.Known(sender)
                || ZNet.instance == null || !ZNet.instance.IsServer() || package.Size() > 1024 || package.ReadInt() != 1) return;
            ZDOID source = package.ReadZDOID(); string boss = package.ReadString(); Vector3 fell = package.ReadVector3();
            if (package.GetPos() != package.Size() || boss.Length > 160 || !NativeBossMessages.Finite(fell)
                || !NativeBossMessages.Source(source, sender, out var origin) || origin.GetPrefab() != boss.GetStableHashCode()
                || AspectStore.GetPhantomOf(origin) != ZDOID.None || Utils.DistanceXZ(origin.GetPosition(), fell) > 10f) return;
            Resolve(boss, fell);
        });

        private static void Resolve(string boss, Vector3 fell)
        {
            if (!TryEnsureRegistered()) return;
            ZoneSystem zones = ZoneSystem.instance;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || zones == null
                || !BossHintTable.TryGet(boss, out BossHintTable.Entry entry))
            {
                return;
            }
            Vector3 origin = Origin(zones, entry.Altar, fell);
            if (!zones.FindClosestLocation(entry.Target, origin, out ZoneSystem.LocationInstance target))
            {
                Log.Info($"boss hint: this world has no {entry.Target}, so {boss} leaves no hint");
                return;
            }
            if (!Repeat(boss, origin))
            {
                int sector = BossHintTable.Sector(origin, target.m_position);
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Hint, fell, sector, target.m_position);
            }
        }

        private static Vector3 Origin(ZoneSystem zones, string altar, Vector3 fell)
        {
            bool found = zones.FindClosestLocation(altar, fell, out ZoneSystem.LocationInstance near);
            return found && Utils.DistanceXZ(near.m_position, fell) <= AltarReach ? near.m_position : fell;
        }

        // Server side. True when this boss's hint went out from the same zone moments ago; otherwise stamps it now.
        private static bool Repeat(string boss, Vector3 origin)
        {
            string key = boss + "@" + ZoneSystem.GetZone(origin);
            double now = ZNet.instance.GetTimeSeconds();
            if (LastSent.TryGetValue(key, out double last) && now - last < RepeatSeconds)
            {
                return true;
            }
            LastSent[key] = now;
            return false;
        }

        private static void OnHint(long sender, Vector3 fell, int sector, Vector3 target)
        {
            if (!NativeBossMessages.Current(_registeredOn, NativeRpcOwner.BossHints) || !NativeBossMessages.Server(sender)
                || !NativeBossMessages.Finite(fell) || !NativeBossMessages.Finite(target) || sector < 0 || sector > 7) return;
            Guard.Run("BossHintRpc.OnHint", () => Show(fell, sector, target));
        }

        // A dedicated server has no HUD and skips this, as does a player far from the fall, with hints off, or who has
        // already found the target.
        private static void Show(Vector3 fell, int sector, Vector3 target)
        {
            Player player = Player.m_localPlayer;
            if (MessageHud.instance == null || player == null || !Configuration.BossHints.Value
                || Utils.DistanceXZ(player.transform.position, fell) > PlayerRange || Found(target))
            {
                return;
            }
            string text = $"You feel a presence pulling you... {BossHintTable.Word(sector)}";
            MessageHud.instance.StartCoroutine(ShowLater(text, _registeredOn, ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation));
        }

        // Found: the target's spot is explored on this player's map, by their own travels or a cartography table.
        private static bool Found(Vector3 target) => Minimap.instance != null && Minimap.instance.IsExplored(target);

        private static IEnumerator ShowLater(string text, ZRoutedRpc? bus, long generation)
        {
            yield return new WaitForSeconds(ShowDelay);
            if (MessageHud.instance != null && Configuration.BossHints.Value
                && generation == ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation
                && ReferenceEquals(bus, _registeredOn) && NativeBossMessages.Current(bus, NativeRpcOwner.BossHints))
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
            }
        }
    }
}
