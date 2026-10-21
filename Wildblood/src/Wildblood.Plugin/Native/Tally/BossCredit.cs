// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; v2 late credit (retained authority
// proof and a bounded one-relay bounce).
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Bosses;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Tally
{
    /// <summary>
    /// Puts one hit's health loss on the right boss's board, from the owner of whatever was hit. A hit on the boss is
    /// written straight into its own tally. A hit on one of a Phantom boss's copies counts on that boss's board too, at
    /// the hit, so a copy killed early still counts - but the copy may be owned by another machine than its boss, and
    /// only the boss's owner may write the boss's ZDO. So the credit goes to the machine that owns the boss's ZDO, or,
    /// when this machine does not have that ZDO, to the server, which hands it on once to the owner it knows. A credit
    /// that lands on a machine that has just given the boss away is bounced back to the server once per relay (at most
    /// <see cref="BossCreditProofs.MaxHops"/> times), which hands it to the boss's new owner: counted once, never twice.
    /// A credit that reaches the server after its source was destroyed or handed over is accepted only against the
    /// server's retained proof of that source's owner and boss, for a short grace window.
    /// </summary>
    internal static class BossCredit
    {
        public const string Rpc = "ecr_boss_credit";

        private static ZRoutedRpc? _registeredOn;
        private static BossCreditProofs _proofs = new BossCreditProofs();
        private static long _sequence;

        /// <summary>Registers the handler once per routed-RPC bus. Called at world start on every machine.</summary>
        public static void EnsureRegistered() => TryEnsureRegistered();

        public static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || !NativeBossMessages.Active)
            {
                return false;
            }
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.BossCredit, () => {
                bus.Register<ZPackage>(Rpc, OnCredit);
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Rpc)) return false;
            if (!ReferenceEquals(_registeredOn, bus)) _proofs = new BossCreditProofs();
            _registeredOn = bus;
            return true;
        }

        /// <summary>On the owner of the character hit: the boss itself, or one of its Phantom copies.</summary>
        public static void Add(ZDO hit, Player player, float amount)
        {
            if (!NativeBossMessages.Active || !hit.IsOwner()) return;
            ZDOID boss = AspectStore.GetPhantomOf(hit);
            if (boss == ZDOID.None)
            {
                DamageTally.Add(hit, player.GetPlayerID(), player.GetPlayerName(), amount);
                return;
            }
            Route(hit, boss, player.GetPlayerID(), player.GetPlayerName(), amount);
        }

        /// <summary>
        /// On the owner of a Tethered boss as it falls first: its whole tally goes on to its partner's board, the same way
        /// a copy's hits reach their boss, and its own is emptied so nothing counts twice.
        /// </summary>
        public static void HandOver(ZDO fallen, ZDOID partner)
        {
            if (!TryEnsureRegistered()) return;
            foreach (DamageTally.Entry entry in DamageTally.Load(fallen))
            {
                Route(fallen, partner, entry.Id, entry.Name, entry.Damage);
            }
            DamageTally.Clear(fallen);
        }

        private static void Route(ZDO source, ZDOID boss, long playerId, string playerName, float amount)
        {
            if (!TryEnsureRegistered()) return;
            ZDO? zdo = ZDOMan.instance?.GetZDO(boss);
            if (zdo != null && zdo.IsOwner())
            {
                DamageTally.Add(zdo, playerId, playerName, amount);
                return;
            }
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || !NativeBossMessages.Active)
            {
                return;
            }
            // Native clients connect to the server, not every other creature owner. Authority validates and relays once.
            bus.InvokeRoutedRPC(Rpc, Pack(source.m_uid, source.GetOwner(), boss, playerId, playerName, amount, ++_sequence, 0));
        }

        private static ZPackage Pack(ZDOID source, long sourceOwner, ZDOID boss, long playerId, string playerName, float amount, long sequence, int hops)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(2); // Wildblood boss credit package version; peers must run this product minor version.
            pkg.Write(source);
            pkg.Write(sourceOwner);
            pkg.Write(boss);
            pkg.Write(playerId);
            pkg.Write(playerName);
            pkg.Write(amount);
            pkg.Write(sequence);
            pkg.Write(hops);
            return pkg;
        }

        private static NativeRef Ref(ZDOID id) => new NativeRef(id.UserID, id.ID);

        private static double Now => UnityEngine.Time.realtimeSinceStartupAsDouble;

        private static void OnCredit(long sender, ZPackage pkg) => Guard.Run("BossCredit.OnCredit", () => Receive(sender, pkg));

        // The boss's owner adds it. Only the server hands it on, and only to another machine, so it never goes round.
        private static void Receive(long sender, ZPackage pkg)
        {
            if (!NativeBossMessages.Current(_registeredOn, NativeRpcOwner.BossCredit) || !NativeBossMessages.Known(sender)
                || pkg.Size() > 2048 || pkg.ReadInt() != 2) return;
            ZDOID source = pkg.ReadZDOID(); long sourceOwner = pkg.ReadLong();
            ZDOID boss = pkg.ReadZDOID();
            long playerId = pkg.ReadLong();
            string playerName = pkg.ReadString();
            float amount = pkg.ReadSingle();
            long sequence = pkg.ReadLong();
            int hops = pkg.ReadInt();
            if (pkg.GetPos() != pkg.Size() || playerId == 0 || playerName.Length > 256 || hops < 0 || hops > BossCreditProofs.MaxHops
                || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;
            bool server = ZNet.instance != null && ZNet.instance.IsServer();
            bool serverRelay = NativeBossMessages.Server(sender) && !server;
            string key = BossCreditProofs.Key(Ref(source), sourceOwner, Ref(boss), playerId, amount, sequence);
            if (!serverRelay && !Admit(sender, source, sourceOwner, boss, key, hops, server)) return;
            ZDO? zdo = ZDOMan.instance?.GetZDO(boss);
            if (zdo == null) return;
            if (zdo.IsOwner())
            {
                DamageTally.Add(zdo, playerId, playerName, amount);
                return;
            }
            long owner = zdo.GetOwner();
            if (server && owner != 0L && owner != ZDOMan.GetSessionID())
            {
                _proofs.Relayed(key, owner, Now);
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, Rpc, Pack(source, sourceOwner, boss, playerId, playerName, amount, sequence, hops));
            }
            else if (serverRelay && BossCreditProofs.MayBounce(hops))
            {
                // This machine gave the boss away after the server relayed: hand it back once for the new owner.
                ZRoutedRpc.instance.InvokeRoutedRPC(Rpc, Pack(source, sourceOwner, boss, playerId, playerName, amount, sequence, hops + 1));
            }
        }

        // Live proof (the sender owns the source now, and the source belongs to the boss) is retained on the server; a late
        // credit is checked against that retained proof; a bounce only from the exact peer the server relayed it to.
        private static bool Admit(long sender, ZDOID source, long sourceOwner, ZDOID boss, string key, int hops, bool server)
        {
            if (sender == sourceOwner && NativeBossMessages.Source(source, sender, out var origin)
                && (AspectStore.GetPhantomOf(origin) == boss || AspectStore.GetTether(origin) == boss))
            {
                if (server) _proofs.Observe(Ref(source), sender, Ref(boss), Now);
                return true;
            }
            if (!server) return false;
            if (sender == sourceOwner && _proofs.ProvenLate(Ref(source), sender, Ref(boss), Now)) return true;
            return hops > 0 && _proofs.ConsumeBounce(key, sender, Now);
        }
    }
}
