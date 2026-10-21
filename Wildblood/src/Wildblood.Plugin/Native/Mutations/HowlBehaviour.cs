// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: EliteCreaturesReborn 3.22's Howling mutation, built from its described behavior only.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Howling: the first time it fights a player it howls, and up to `count` creatures within `radius` meters that count
    /// that player an enemy and are not already fighting come for them. Once in its life: the howl is stamped in its ZDO,
    /// so a reload or a new owner never howls again. Decided on the owner; each called creature is woken through the
    /// game's own near-hit message, which its own owner answers by alerting it and taking the player as its target, so a
    /// creature owned by another machine answers too. The howl is heard on every client holding the creature. Tamed,
    /// bosses and players are never called; a tamed one never howls.
    /// </summary>
    public sealed class HowlBehaviour : MonoBehaviour
    {
        public const string Rpc = "wildblood_howl";

        private const float CheckEvery = 0.5f;
        private const int MostCalled = 16;

        private static readonly int HowledHash = TraitKeys.Howled.GetStableHashCode();
        private static readonly string[] HowlWords = { "howl", "wolf", "screech", "roar" };

        private Character _character = null!;
        private EliteController? _controller;
        private MonsterAI? _ai;
        private float _nextCheck;
        private readonly List<Character> _called = new List<Character>();

        /// <summary>How many creatures the last howl on this machine called (the proving check reads it).</summary>
        public int LastCalled { get; private set; } = -1;

        private void Start() => Guard.Run("HowlBehaviour.Start", Setup);

        private void Setup()
        {
            Character character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            if (character == null || controller == null || controller.View == null || !controller.View.IsValid())
            {
                enabled = false;
                return;
            }
            _character = character;
            _controller = controller;
            _ai = GetComponent<MonsterAI>();
            if (!NativeRpcOwnership.View(controller.View, NativeRpcOwner.CreatureHowl,
                    () => controller.View.Register<Vector3>(Rpc, OnHowl), Rpc))
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (_controller != null && Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + CheckEvery;
                Guard.Run("HowlBehaviour.Update", Step);
            }
        }

        private void Step()
        {
            EliteController controller = _controller!;
            if (!controller.Ready || !controller.IsOwner() || _character.IsDead() || _character.IsTamed() || _ai == null
                || !NativeRpcOwnership.OwnsView(controller.View, NativeRpcOwner.CreatureHowl))
            {
                return;
            }
            ZDO zdo = controller.View.GetZDO();
            if (zdo.GetBool(HowledHash) || !(_ai.GetTargetCreature() is Player target) || target.IsDead())
            {
                return;
            }
            zdo.Set(TraitKeys.Howled, true);
            Howl(controller, target);
        }

        private void Howl(EliteController controller, Player target)
        {
            BiomeRules rules = controller.Rules;
            int count = Mathf.Clamp(Mathf.RoundToInt(rules.PowerOf(Mutation.Howling, Fields.Count)), 0, MostCalled);
            float radius = Mathf.Max(0f, Enhance.Magnitude(rules, controller.Traits, Mutation.Howling, Fields.Radius));
            Vector3 here = transform.position;
            _called.Clear();
            foreach (Character other in Character.GetAllCharacters())
            {
                if (Callable(other, target, here, radius))
                {
                    _called.Add(other);
                }
            }
            _called.Sort((a, b) => (a.transform.position - here).sqrMagnitude.CompareTo((b.transform.position - here).sqrMagnitude));
            if (_called.Count > count)
            {
                _called.RemoveRange(count, _called.Count - count);
            }
            ZDOID targetId = target.GetZDOID();
            foreach (Character other in _called)
            {
                // The game's own near-hit message: its owner alerts it and takes the player as its target.
                other.m_nview.InvokeRPC("OnNearProjectileHit", here, radius, targetId);
            }
            LastCalled = _called.Count;
            controller.View.InvokeRPC(ZNetView.Everybody, Rpc, _character.GetCenterPoint());
            if (Log.Diagnostics)
            {
                Log.Diag($"{name} howled: {_called.Count} creature(s) within {radius:0}m called to {target.GetPlayerName()}");
            }
        }

        private bool Callable(Character other, Player target, Vector3 here, float radius)
        {
            if (other == null || other == _character || other.IsPlayer() || other.IsBoss() || other.IsDead() || other.IsTamed()
                || other.m_nview == null || !other.m_nview.IsValid()
                || (other.transform.position - here).sqrMagnitude > radius * radius)
            {
                return false;
            }
            MonsterAI ai = other.GetComponent<MonsterAI>();
            return ai != null && ai.GetTargetCreature() == null && BaseAI.IsEnemy(other, target);
        }

        private void OnHowl(long sender, Vector3 at) =>
            Guard.Run("HowlBehaviour.Howl", () =>
            {
                if (isActiveAndEnabled && _controller != null && _controller.Ready && !BlinkEffects.Headless())
                {
                    CosmeticClone.Sound(EffectResolver.ResolveSound(HowlSound(), HowlWords, "Howling howl sound"), at);
                }
            });

        private string HowlSound() =>
            _controller != null ? _controller.Rules.PrefabOf(Mutation.Howling, Fields.HowlSound) : "";
    }
}
