// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Phantom's shuffle: at each split the boss leaves the spot the group has been fighting it on and takes a place in the
    /// ring with its copies, drawn at random, so nobody can tell by where it stands which of them is the boss - they have
    /// to fight them all to find it. On the boss's owner the split sends one message with every place and then moves the
    /// boss (<see cref="BlinkMove"/>, the jump Blinking makes). Every machine holding the boss draws the same puff where the
    /// boss stood and at every place in the ring, boss and copies alike, and hides the boss's body until its synced
    /// position has landed (<see cref="BlinkVeil"/>), so no client sees it slide across. Attached to a Phantom boss on
    /// every machine, never to a copy.
    /// </summary>
    public sealed class PhantomShuffle : MonoBehaviour
    {
        public const string Rpc = "ecr_phantom_shuffle";

        private const string Effect = "vfx_ghost_spawn";
        private static readonly string[] Puff = { "ghost", "spawn", "puff", "smoke", "poof" };

        private readonly BlinkVeil _veil = new BlinkVeil();
        private EliteController? _controller;

        /// <summary>
        /// On the boss's owner, before the move: one message to every machine holding the boss - where it goes, and where
        /// each copy appears. <c>Everybody</c> through its own ZNetView is drawn here at once.
        /// </summary>
        public static void Send(EliteController boss, Vector3 dest, List<Vector3> copies)
        {
            if (!NativeRpcOwnership.OwnsView(boss.View, NativeRpcOwner.CreaturePhantom)) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(dest);
            pkg.Write(copies.Count);
            foreach (Vector3 place in copies)
            {
                pkg.Write(place);
            }
            boss.View.InvokeRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        /// <summary>On the boss's owner, after the message: the boss takes its place in the ring, facing the middle.</summary>
        public static void Move(EliteController boss, Vector3 dest, Vector3 centre)
        {
            if (NativeRpcOwnership.OwnsView(boss.View, NativeRpcOwner.CreaturePhantom)) BlinkMove.Jump(boss.Creature, dest, centre);
        }

        private void Start() => Guard.Run("PhantomShuffle.Start", Setup);

        private void Setup()
        {
            _controller = GetComponent<EliteController>();
            if (_controller == null || _controller.View == null || !_controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            if (!NativeRpcOwnership.View(_controller.View, NativeRpcOwner.CreaturePhantom, () => _controller.View.Register<ZPackage>(Rpc, OnShuffle), Rpc))
            {
                enabled = false;
                return;
            }
        }

        private void Update() => Guard.Run("PhantomShuffle.Update", static self => self.Tick(), this);

        private void Tick()
        {
            if (_controller != null && _controller.View != null)
            {
                _veil.Tick(_controller.View, transform, Time.deltaTime);
            }
        }

        private void OnShuffle(long sender, ZPackage pkg) => Guard.Run("PhantomShuffle.Draw", () => Draw(pkg));

        /// <summary>Every machine with a screen: a puff where the boss stood and at every place, then the veil.</summary>
        private void Draw(ZPackage pkg)
        {
            Character? boss = _controller != null ? _controller.Creature : null;
            if (boss == null || BlinkEffects.Headless())
            {
                return;
            }
            Vector3 dest = pkg.ReadVector3();
            float radius = BlinkEffects.Radius(boss);
            GameObject? puff = EffectResolver.Resolve(Effect, Puff, "Phantom shuffle effect");
            CosmeticClone.Flash(puff, transform.position, radius);
            CosmeticClone.Flash(puff, dest, radius);
            int copies = pkg.ReadInt();
            for (int i = 0; i < copies; i++)
            {
                CosmeticClone.Flash(puff, pkg.ReadVector3(), radius);
            }
            _veil.Drop(gameObject, dest);
        }
    }
}
