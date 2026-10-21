// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Tally
{
    /// <summary>
    /// Fetches the latest boss board from the server, for a player whose own machine has none - one who joined after the
    /// kill. The server received every board's broadcast and keeps the latest (<see cref="BossBoard"/>); the player asks
    /// it, and it answers the asking player alone, with its board or with none. A board that comes back is kept here as
    /// the latest too, unless one arrived from a boss's death while the question was out.
    /// </summary>
    internal static class BossBoardRecall
    {
        public const string Ask = "ecr_boss_board_ask";
        public const string Answer = "ecr_boss_board_answer";

        private static ZRoutedRpc? _registeredOn;
        private static Action<BossBoard?>? _pending;

        /// <summary>Registers both handlers once per routed-RPC bus. Called at world start on every machine.</summary>
        public static void EnsureRegistered() => TryEnsureRegistered();

        public static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || !NativeBossMessages.Active)
            {
                return false;
            }
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.BossRecall, () => {
                _pending = null;
                bus.Register(Ask, OnAsk);
                bus.Register<ZPackage>(Answer, OnAnswer);
            }, () => { if (ReferenceEquals(_registeredOn, bus)) { _registeredOn = null; _pending = null; } }, Ask, Answer)) return false;
            _registeredOn = bus;
            return true;
        }

        /// <summary>
        /// Player side: asks the server and hands its answer (null for none) to <paramref name="done"/>. False, asking
        /// nothing, when this machine is the server itself or no world is loaded: then there is nobody else to ask.
        /// </summary>
        public static bool Request(Action<BossBoard?> done)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null)
            {
                return false;
            }
            if (!TryEnsureRegistered()) return false;
            _pending = done;
            ZRoutedRpc.instance.InvokeRoutedRPC(Ask);
            return true;
        }

        internal static void CancelPending(Action<BossBoard?> expected)
        {
            if (ReferenceEquals(_pending, expected)) _pending = null;
        }

        private static void OnAsk(long sender) => Guard.Run("BossBoardRecall.OnAsk", () => Reply(sender));

        // Server side: the answer goes to the asking player alone.
        private static void Reply(long sender)
        {
            if (!NativeBossMessages.Current(_registeredOn, NativeRpcOwner.BossRecall) || !NativeBossMessages.Known(sender)) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }
            BossBoard? board = BossBoard.Latest;
            ZPackage pkg = new ZPackage();
            pkg.Write(1); pkg.Write(board != null);
            board?.Write(pkg);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, Answer, pkg);
        }

        private static void OnAnswer(long sender, ZPackage pkg) => Guard.Run("BossBoardRecall.OnAnswer", () => Receive(sender, pkg));

        private static void Receive(long sender, ZPackage pkg)
        {
            if (_pending == null || !NativeBossMessages.Current(_registeredOn, NativeRpcOwner.BossRecall)
                || !NativeBossMessages.Server(sender) || pkg.Size() > 65536 || pkg.ReadInt() != 1) return;
            BossBoard? board = pkg.ReadBool() ? BossBoard.Read(pkg) : null;
            if (pkg.GetPos() != pkg.Size()) return;
            if (board != null && BossBoard.Latest == null)
            {
                BossBoard.Remember(board);
            }
            Action<BossBoard?>? done = _pending;
            _pending = null;
            done?.Invoke(board);
        }
    }
}
