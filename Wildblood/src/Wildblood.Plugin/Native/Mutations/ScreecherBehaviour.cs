// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Screecher: one hit that takes at least `threshold` percent of its maximum health makes it shriek, at most once
    /// every `cooldown` seconds. Every player within `radius` metres that it counts as an enemy is deafened for `mute
    /// time` seconds on their own machine (<see cref="ShriekDeafness"/>): the world falls near-silent under a ringing,
    /// and spells will not cast. Decided on the owner, where the game resolves the hit (<c>ShriekDamagePatch</c> hands
    /// it the health the hit actually took): it stamps the shriek in the ZDO on the shared clock, so a new owner keeps
    /// the cooldown, sends each deafened player's own machine one routed message (<see cref="ShriekRpc"/>), and
    /// broadcasts the tell over the creature's own ZNetView, so only the clients holding the creature draw it
    /// (<see cref="ShriekLook"/>). A killing blow ends it rather than making it shriek. Attached on every machine: each
    /// one needs the tell's handler, and only the live owner's gate opens.
    /// </summary>
    public sealed class ScreecherBehaviour : MonoBehaviour
    {
        private static readonly int ShriekAtHash = TraitKeys.ShriekAt.GetStableHashCode();

        /// <summary>The tell, to every client holding the creature: where it shrieked and how far the shriek carries.</summary>
        public const string Rpc = "ecr_shriek";

        private Character _character = null!;
        private EliteController? _controller;
        private readonly List<Player> _near = new List<Player>();

        /// <summary>True on the live owner of a living, set-up Screecher: the one machine that decides a shriek.</summary>
        public bool Deciding => _controller != null && _controller.IsOwner() && !_character.IsDead()
            && NativeRpcOwnership.OwnsView(_controller.View, NativeRpcOwner.CreatureShriek) && ShriekRpc.TryEnsureRegistered();

        private void Start() => Guard.Run("ScreecherBehaviour.Start", Setup);

        private void Setup()
        {
            Character character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            if (character == null || controller == null || controller.View == null || !controller.View.IsValid())
            {
                return;
            }
            _character = character;
            _controller = controller;
            ShriekRpc.EnsureRegistered();
            if (!NativeRpcOwnership.View(controller.View, NativeRpcOwner.CreatureShriek, () => controller.View.Register<Vector3, float>(Rpc, OnTell), Rpc))
            {
                enabled = false;
                return;
            }
        }

        /// <summary>Owner side, once a hit has resolved: <paramref name="lost"/> is the health it actually took.</summary>
        public void Hurt(float lost)
        {
            if (!Deciding || lost <= 0f || _character.GetHealth() <= 0f)
            {
                return;
            }
            BiomeRules rules = _controller!.Rules;
            float threshold = rules.PowerOf(Mutation.Screecher, Fields.Threshold); // never enhanced: it is the trigger
            if (lost >= _character.GetMaxHealth() * threshold / 100f && Rested(rules))
            {
                Shriek(rules);
            }
        }

        /// <summary>The cooldown, read from the shared-clock stamp in the ZDO, so a new owner honours the old one's.</summary>
        private bool Rested(BiomeRules rules)
        {
            long last = _controller!.View.GetZDO().GetLong(ShriekAtHash);
            return last <= 0L || NetTime.SecondsSince(last) >= rules.PowerOf(Mutation.Screecher, Fields.Cooldown);
        }

        // Reach and mute time are its gains, so a large star enhances both; the threshold and the cooldown never.
        private void Shriek(BiomeRules rules)
        {
            EliteController controller = _controller!;
            controller.View.GetZDO().Set(TraitKeys.ShriekAt, NetTime.NowMs());
            float radius = Enhance.Magnitude(rules, controller.Traits, Mutation.Screecher, Fields.Radius);
            float seconds = Enhance.Magnitude(rules, controller.Traits, Mutation.Screecher, Fields.MuteTime);
            controller.View.InvokeRPC(ZRoutedRpc.Everybody, Rpc, _character.GetCenterPoint(), radius);
            int deafened = Deafen(radius, seconds);
            if (Log.Diagnostics)
            {
                Log.Diag($"{name} shrieked: {deafened} player(s) within {radius:0}m deafened for {seconds:0.0}s");
            }
        }

        /// <summary>
        /// Every living player in reach that it counts as an enemy - so a tamed Screecher, or a peaceful dverger, deafens
        /// no one - each told on their own machine. An admin's ghost is never in reach.
        /// </summary>
        private int Deafen(float radius, float seconds)
        {
            if (seconds <= 0f || radius <= 0f)
            {
                return 0;
            }
            _near.Clear();
            Player.GetPlayersInRange(transform.position, radius, _near);
            int count = 0;
            foreach (Player player in _near)
            {
                if (player != null && !player.IsDead() && !player.InGhostMode() && BaseAI.IsEnemy(_character, player))
                {
                    ShriekRpc.Send(player, seconds);
                    count++;
                }
            }
            return count;
        }

        private void OnTell(long sender, Vector3 at, float radius) =>
            Guard.Run("ScreecherBehaviour.Tell", () =>
            {
                if (isActiveAndEnabled && _controller != null && _controller.Ready)
                    ShriekLook.Play(_character, ShriekSound(), at, radius);
            });

        private string ShriekSound() =>
            _controller != null ? _controller.Rules.PrefabOf(Mutation.Screecher, Fields.ShriekSound) : "";
    }
}
