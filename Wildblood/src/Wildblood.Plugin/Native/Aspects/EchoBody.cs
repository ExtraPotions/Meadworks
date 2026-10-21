// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What an echo is on every machine: the boss's white ghost (<see cref="EchoLook"/>) with no body to touch and no mind
    /// of its own. Veiled the moment a machine knows it for an echo - on its owner as it is made
    /// (<see cref="EchoSpawner"/>), everywhere else as it wakes from its ZDO (<see cref="Patches.CharacterLifecyclePatch"/>)
    /// - so no frame ever shows a second boss: its look, its colliders off (nothing hits it, nothing bumps into it), its
    /// AI off (it only does what its boss did, <see cref="EchoDriver"/>), and nothing in the world that hurts it.
    /// Once its controller has dressed it, it is hollow the way a Phantom copy is (<see cref="PhantomBody"/>) and no longer
    /// counts as a boss, so no boss bar, music or boss check ever finds it; parts the dressing showed are filmed too. It
    /// keeps its boss's size (a Colossal boss's included), and its owner takes it away once the boss is dead or gone.
    /// </summary>
    public sealed class EchoBody : MonoBehaviour
    {
        private const float CheckEvery = 0.5f;

        /// <summary>Seconds to wait for the controller before calming the echo anyway.</summary>
        private const float DressWait = 5f;

        private Character _echo = null!;
        private ZNetView _view = null!;
        private EliteController? _controller;
        private ZDOID _boss;
        private float _born;
        private float _next;
        private bool _calm;
        private System.Action? _step;

        /// <summary>Every machine, as soon as it is known for an echo: veiled, stilled, and found by <see cref="EchoLink"/>.</summary>
        public static void Veil(Character echo, ZDOID boss)
        {
            if (EchoLink.IsEcho(echo))
            {
                return;
            }
            EchoLink.Join(echo, boss);
            Still(echo);
            EchoLook.Apply(echo);
            EchoBody body = echo.gameObject.AddComponent<EchoBody>();
            body._echo = echo; // so its OnDestroy lets the link go even if it never starts
            body._boss = boss;
        }

        /// <summary>The wake-up check for any creature: an echo by its ZDO mark is veiled at once.</summary>
        public static void VeilIfEcho(Character character)
        {
            ZNetView view = character.GetComponent<ZNetView>();
            ZDO? zdo = view != null ? view.GetZDO() : null;
            ZDOID boss = zdo != null ? AspectStore.GetEchoOf(zdo) : ZDOID.None;
            if (boss != ZDOID.None)
            {
                Veil(character, boss);
            }
        }

        private static void Still(Character echo)
        {
            BaseAI ai = echo.GetComponent<BaseAI>();
            if (ai != null)
            {
                ai.enabled = false;
            }
            foreach (Collider collider in echo.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            echo.m_disableWhileSleeping = false;
            echo.m_tolerateFire = echo.m_tolerateSmoke = echo.m_tolerateWater = echo.m_tolerateTar = true;
            Rigidbody body = echo.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
            }
        }

        private void Start() => Guard.Run("EchoBody.Start", Setup);

        private void Setup()
        {
            _echo = GetComponent<Character>();
            _view = GetComponent<ZNetView>();
            _controller = GetComponent<EliteController>();
            _born = Time.time;
            PhantomBody.Hollow(_echo);
        }

        private void Update() => Guard.Run("EchoBody.Update", _step ??= Step);

        private void Step()
        {
            if (!_calm && (_controller == null || _controller.Ready || Time.time > _born + DressWait))
            {
                Calm();
            }
            if (Time.time < _next || _echo == null)
            {
                return;
            }
            _next = Time.time + CheckEvery;
            Character? boss = EchoLink.BossOf(_echo);
            if (boss != null && transform.localScale != boss.transform.localScale)
            {
                transform.localScale = boss.transform.localScale;
            }
            if (_view != null && _view.IsValid() && _view.IsOwner() && Orphaned(boss))
            {
                ZNetScene.instance.Destroy(gameObject);
            }
        }

        /// <summary>After the controller has dressed it (its stars' look may show parts of its own): no boss, all filmed.</summary>
        private void Calm()
        {
            _calm = true;
            _echo.m_boss = false;
            EchoLook.Apply(_echo);
        }

        private bool Orphaned(Character? boss) =>
            ZDOMan.instance == null || ZDOMan.instance.GetZDO(_boss) == null || (boss != null && boss.IsDead());

        private void OnDestroy() => EchoLink.Leave(_echo);
    }
}
