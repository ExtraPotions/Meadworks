// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Brutal: its heavy blows (<see cref="HeavyAttack"/>, the same blows that shake the ground for Colossal) throw every
    /// player they hit `launch` metres away from it, peaking `lift` metres up. The hit deals its damage as usual; the
    /// landing deals none. The game runs a boss's attacks on its owner, the only machine that knows a blow is heavy, so
    /// that is where each hit of a heavy blow is marked as it is sent (<see cref="BrutalBlow"/>, through
    /// <see cref="Patches.BrutalBlowPatch"/>); the mark travels inside the hit itself to the struck player's own client,
    /// which owns their body and alone knows whether the hit was dodged or blocked. There the hit is judged and the
    /// player thrown (<see cref="BrutalHit"/>) and carried to a soft landing (<see cref="BrutalFlight"/>). The thrown
    /// player's client then sends one message through the boss's own network view, the point they were thrown from, so
    /// every machine holding the boss hears the throw; the flight itself everyone sees through the player's own
    /// position sync. A hand-over needs nothing: a blow is marked only where it is dealt, and the tell is registered
    /// everywhere.
    /// </summary>
    public sealed class BrutalBehaviour : MonoBehaviour
    {
        /// <summary>The tell, to every machine holding the boss: the point a player was thrown from.</summary>
        public const string Rpc = "ecr_brutal_throw";

        private const string ThrowSound = "sfx_frozenking_charge_whoosh";

        // Stand-ins should a game update rename the sound: the nearest heavy rush of air, the first keyword first.
        private static readonly string[] Rush = { "whoosh", "swoosh", "swing" };

        private Character _character = null!;
        private EliteController? _controller;

        private void Start() => Guard.Run("BrutalBehaviour.Start", Setup);

        private void Setup()
        {
            _character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            if (_character == null || controller == null || controller.View == null || !controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _controller = controller;
            if (!NativeRpcOwnership.View(controller.View, NativeRpcOwner.CreatureBrutal, () => controller.View.Register<Vector3>(Rpc, OnThrow), Rpc))
            {
                enabled = false;
                return;
            }
        }

        /// <summary>Owner side, as a blow begins: true when its hits throw the players they land on.</summary>
        public bool Throws(Attack attack) =>
            _controller != null && NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureBrutal)
            && _controller.IsOwner() && !_character.IsDead() && BrutalHit.Launch() > 0f
            && HeavyAttack.IsHeavy(attack);

        /// <summary>On the thrown player's machine: tell every machine holding the boss, so all of them hear it.</summary>
        public void Tell(Vector3 from)
        {
            if (_controller != null && NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureBrutal))
            {
                _controller.View.InvokeRPC(ZNetView.Everybody, Rpc, from); // here at once, and to every peer
            }
        }

        private void OnThrow(long sender, Vector3 from) => Guard.Run("BrutalBehaviour.Throw", () =>
        {
            if (isActiveAndEnabled && _controller != null && _controller.Ready) Whoosh(from);
        });

        // A heavy rush of air where the player left the ground. A dedicated server has no speakers: it plays nothing.
        private static void Whoosh(Vector3 from)
        {
            if (ZNet.instance == null || ZNet.instance.IsDedicated())
            {
                return;
            }
            CosmeticClone.Sound(EffectResolver.ResolveSound(ThrowSound, Rush, "Brutal throw sound"), from);
        }
    }
}
