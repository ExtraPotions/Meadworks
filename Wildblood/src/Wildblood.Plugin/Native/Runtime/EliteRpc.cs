// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// The two world-event broadcasts that cannot ride a creature's ZDO, because by the time they happen the creature
    /// (and its ZDO, and its ZNetView) is gone: a Bloated death has no per-creature channel left to scope to, so both go
    /// out over the game's routed-RPC bus to <c>Everybody</c>.
    /// <list type="bullet">
    /// <item><b>Bloat</b> fires at death: every client spawns a warning that rides its own copy of the dying creature's
    /// corpse, named by id, for the fuse.</item>
    /// <item><b>Blast</b> fires when the owner's fuse ends: every client draws the blast at the owner's corpse, and only
    /// the machine that sent it (the owner) deals the damage there - so what everyone sees and what actually hurts agree,
    /// even though ragdoll physics settles the corpse in a slightly different spot on every machine. It names the corpse,
    /// and whichever machine owns that corpse bursts it (see <see cref="CorpseBurst"/>).</item>
    /// </list>
    /// Per-creature effects that only the clients holding the creature need (the Warding tell) go through
    /// <see cref="CreatureRpc"/> instead: every player still receives those, but only those clients handle them. Handlers
    /// are registered lazily once the network is up, keyed on the bus instance so a new world re-registers.
    /// </summary>
    public static class EliteRpc
    {
        public const string Bloat = "ecr_bloat";
        public const string Blast = "ecr_blast";

        private static ZRoutedRpc? _registeredOn;

        /// <summary>
        /// Registers the global handlers on the current routed-RPC bus, once per bus. Keyed on the ZRoutedRpc instance,
        /// not a bool, so leaving a world and joining another (which builds a fresh bus) re-registers rather than going
        /// silent. Safe to call from every creature's setup; the network is always up by then.
        /// </summary>
        public static void EnsureRegistered() => TryEnsureRegistered();

        public static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null)
            {
                return false;
            }
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.EliteEffects, () => {
                bus.Register<ZPackage>(Bloat, OnBloat);
                bus.Register<ZPackage>(Blast, OnBlast);
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Bloat, Blast)) return false;
            _registeredOn = bus;
            return true;
        }

        /// <summary>
        /// Owner-side, at death: announce the Bloated death so every client wears a warning on its own copy of the corpse,
        /// named by id (<c>ZDOID.None</c> when the death left no ragdoll).
        /// </summary>
        public static void FireBloat(Vector3 pos, ZDOID corpse, float delay, string warningEffect, BlastSpec blast)
        {
            if (!TryEnsureRegistered()) return;
            if (ZRoutedRpc.instance == null)
            {
                return;
            }
            ZPackage pkg = new ZPackage();
            pkg.Write(pos.x); pkg.Write(pos.y); pkg.Write(pos.z);
            pkg.Write(corpse);
            pkg.Write(delay); pkg.Write(warningEffect ?? "");
            blast.Write(pkg);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Bloat, pkg);
        }

        /// <summary>
        /// Owner-side, at fuse end: announce the blast at the owner's corpse so all clients agree on its place, naming the
        /// corpse (<c>ZDOID.None</c> when there is none) so the blast can burst it.
        /// </summary>
        public static void FireBlast(Vector3 pos, ZDOID corpse, BlastSpec blast)
        {
            if (!TryEnsureRegistered()) return;
            if (ZRoutedRpc.instance == null)
            {
                return;
            }
            ZPackage pkg = new ZPackage();
            pkg.Write(pos.x); pkg.Write(pos.y); pkg.Write(pos.z);
            pkg.Write(corpse);
            blast.Write(pkg);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Blast, pkg);
        }

        private static void OnBloat(long sender, ZPackage pkg) => Guard.Run("EliteRpc.OnBloat", () => ReadBloat(sender, pkg));

        // Runs on every client. The sender (the creature's owner) owns the fuse and will fire the blast; only its corpse
        // rider is marked as the one that broadcasts the blast, so the blast position is the owner's corpse alone.
        private static void ReadBloat(long sender, ZPackage pkg)
        {
            Vector3 pos = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
            ZDOID corpse = pkg.ReadZDOID();
            float delay = pkg.ReadSingle();
            string warning = pkg.ReadString();
            BloatedCorpse.Spawn(pos, corpse, delay, warning, BlastSpec.Read(pkg), isOwner: sender == ZNet.GetUID());
        }

        private static void OnBlast(long sender, ZPackage pkg) => Guard.Run("EliteRpc.OnBlast", () => ReadBlast(sender, pkg));

        // Runs on every client. Only the sender (the owner) lets the blast deal damage; the rest only draw it. Only the
        // corpse's owner - normally the sender too - bursts it, and its removal reaches everyone else from there.
        private static void ReadBlast(long sender, ZPackage pkg)
        {
            Vector3 pos = new Vector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
            ZDOID corpse = pkg.ReadZDOID();
            BloatedBlast.Detonate(pos, BlastSpec.Read(pkg), damaging: sender == ZNet.GetUID());
            CorpseBurst.Burst(corpse);
        }
    }
}
