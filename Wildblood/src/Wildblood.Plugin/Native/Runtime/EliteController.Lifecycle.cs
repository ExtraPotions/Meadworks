// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: live module/session gates around adopted ECR state and exact owned component instances
// (module flags 1/2/4/8 and 16 = Tamed Keep Traits off for a tamed creature).
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    public sealed partial class EliteController
    {
        private long _sessionGeneration = -1;
        private int _activeFlags;
        private float _lifecycleWait;
        private bool _baseCaptured;
        private bool _movementOwned;
        private float _devourBonus, _swiftBonus;
        private BaseSpeeds _appliedSpeeds;
        private bool _healthOwned;
        private float _baseHealth, _appliedHealth;
        private bool _scaleOwned;
        private Vector3 _baseScale, _appliedScale;
        private readonly List<MonoBehaviour> _ownedBehaviours = new List<MonoBehaviour>();
        private readonly HashSet<MonoBehaviour> _suspendedBehaviours = new HashSet<MonoBehaviour>();
        private readonly List<GameObject> _ownedVisuals = new List<GameObject>();
        private readonly HashSet<GameObject> _suspendedVisuals = new HashSet<GameObject>();

        private int CurrentFlags => (WildbloodModules.Active(WildbloodModule.Stars) ? 1 : 0)
            | (WildbloodModules.Active(WildbloodModule.Traits) ? 2 : 0)
            | (WildbloodModules.Active(WildbloodModule.Bosses) ? 4 : 0)
            | (WildbloodModules.Active(WildbloodModule.Loot) ? 8 : 0)
            | (TraitsWithheld ? 16 : 0)
            | (_nview != null && _nview.IsValid() && ExtraPotions.Wildblood.Adopted.Horde.HordeFactors.Weakened(_character, _nview.GetZDO()) ? 32 : 0);
        // Tamed Keep Traits off: a tamed creature's mutations, modifiers and aspects are withheld, stars stay, stored traits are untouched.
        // The flag rides the module flags, so taming or a setting change suspends and re-resolves the controller through the normal path.
        // Tamed is the cheap test and the only creatures that need the setting read.
        private bool TraitsWithheld => _character != null && _character.IsTamed() && ExtraPotions.Wildblood.Stars.TamedTraits.Stripped(
            Plugin.Settings == null || Plugin.Settings.Get(ExtraPotions.Wildblood.Config.WildbloodSettings.Traits.TamedKeepTraits), true);
        internal bool HordeOnly => !_isBoss && (_activeFlags & 3) == 0 && (_activeFlags & 32) != 0;
        internal bool LootOnly => (_activeFlags & (_isBoss ? 6 : 3)) == 0 && (_activeFlags & 8) != 0 && !HordeOnly;
        internal bool LootReady => _ready && RuntimeAvailable;
        private bool StarsActive => (_activeFlags & (_isBoss ? 4 : 1)) != 0;
        private bool RuntimeAvailable => _sessionGeneration == CruciblePlugin.Session.Generation
            && NativeModifierRules.Ready && _activeFlags == CurrentFlags
            && (_activeFlags & (_isBoss ? 14 : 43)) != 0;

        internal bool ProducersAvailable => RuntimeAvailable;
        // Exact post-ECR baselines permit the separate CM overlay to follow a deliberate ECR change.
        internal bool ReapingHealthBaselineOwned => _character != null && OwnsCurrentView()
            && ((_healthOwned && _character.GetMaxHealth() == _appliedHealth)
                || (KeepsNativeLevel && CreatureLevelManager.MatchesPreservedNativeHealthBaseline(_character)));
        internal bool ReapingScaleBaselineOwned => _character != null && ReapingScaleReady
            && _character.transform.localScale == _appliedScale;

        // Settlement/teardown paths may still own this creature after new producers are disabled.
        internal bool OwnsCurrentView() => _sessionGeneration == CruciblePlugin.Session.Generation
            && _nview != null && _nview.IsValid() && _nview.IsOwner();

        internal T InstallBehaviour<T>() where T : MonoBehaviour
        {
            foreach (MonoBehaviour owned in _ownedBehaviours)
                if (owned is T existing) return existing;
            T created = gameObject.AddComponent<T>();
            _ownedBehaviours.Add(created);
            return created;
        }

        internal void OwnVisual(GameObject? visual)
        {
            if (visual == null || _ownedVisuals.Contains(visual)) return;
            _ownedVisuals.Add(visual);
            if (!Ready && visual.activeSelf)
            {
                _suspendedVisuals.Add(visual);
                visual.SetActive(false);
            }
        }

        private void ProjectTraits()
        {
            // Load produced the canonical identity. Only this in-memory copy is filtered; never write it back.
            CreatureTraits stored = _traits;
            bool withheld = (_activeFlags & 16) != 0;
            _traits = new CreatureTraits(stored.Stars, (_activeFlags & 2) != 0 && !withheld ? stored.Mask : 0)
            {
                Aspect = (_activeFlags & 4) != 0 && !withheld ? stored.Aspect : Aspect.None,
                ExtraAspects = (_activeFlags & 4) != 0 && !withheld ? stored.ExtraAspects : 0,
                Decoy = stored.Decoy, PhantomCopy = stored.PhantomCopy, Tier = stored.Tier
            };
        }

        private void RefreshLifecycle()
        {
            if (_character == null || _nview == null || !_nview.IsValid()) return;
            // Producer/consumer gates close immediately; the bounded poll only waits for readiness/resume.
            if (_ready && !RuntimeAvailable) SuspendRuntime();
            if ((_lifecycleWait -= Time.deltaTime) > 0f) return;
            _lifecycleWait = 0.25f;
            if (_sessionGeneration != CruciblePlugin.Session.Generation)
            {
                SuspendRuntime();
                enabled = false; // an old world's actor never adopts a new session's authority
                return;
            }
            int flags = CurrentFlags;
            if (!NativeModifierRules.Ready || (flags & (_isBoss ? 14 : 43)) == 0)
            {
                SuspendRuntime();
                _activeFlags = flags;
                _pending = true;
                return;
            }
            if (_ready && flags == _activeFlags)
            {
                RefreshSwiftMovement(); // modifiers can arrive or change after Apply, on the owner or by ZDO sync
                return;
            }
            SuspendRuntime();
            _activeFlags = flags;
            _pending = true; // PollResolve uses the existing source roll-once/read path
        }

        private void CaptureMovement()
        {
            if (!_baseCaptured)
            {
                _baseSpeeds = BaseSpeeds.Capture(_character);
                _baseCaptured = true;
            }
        }

        private void RememberScale(Vector3 before)
        {
            _baseScale = before;
            _appliedScale = _character.transform.localScale;
            _scaleOwned = _appliedScale != before;
        }

        private void RestoreScale()
        {
            if (_scaleOwned && _character != null && _character.transform.localScale == _appliedScale)
                _character.transform.localScale = _baseScale;
            _scaleOwned = false;
        }

        internal void BeginOwnedHealthApply()
        {
            if (_character == null || !OwnsCurrentView()) return;
            float current = _character.GetMaxHealth();
            if (!_healthOwned || current != _appliedHealth) _baseHealth = current;
        }

        internal float OwnedHealthBase => _healthOwned && _character.GetMaxHealth() == _appliedHealth
            ? _baseHealth : _character.GetMaxHealth();

        internal void RememberOwnedHealth()
        {
            if (_character == null || !OwnsCurrentView()) return;
            _appliedHealth = _character.GetMaxHealth();
            _healthOwned = _baseHealth > 0f && _appliedHealth > 0f
                && !float.IsNaN(_baseHealth) && !float.IsInfinity(_baseHealth)
                && !float.IsNaN(_appliedHealth) && !float.IsInfinity(_appliedHealth);
        }

        private void RestoreHealth()
        {
            if (_healthOwned && _character != null && OwnsCurrentView()
                && _character.GetMaxHealth() == _appliedHealth)
            {
                float health = _character.GetHealth();
                float fraction = Mathf.Clamp01(health / _appliedHealth);
                _character.SetMaxHealth(_baseHealth);
                _character.SetHealth(_baseHealth * fraction); // preserve wounds/death; never remove s_health to refill
            }
            _healthOwned = false;
        }

        internal void SuspendForShutdown()
        {
            SuspendRuntime();
            _pending = _dressPending = false;
            enabled = false;
        }

        private void SuspendRuntime()
        {
            _ready = false;
            _dressPending = false;
            ReapingScaleReady = false;
            // Unwind the separate CM overlay before the ECR layer it was applied over.
            CreatureModifierManager.SuspendReapingRuntime(_character);
            RestoreHealth();
            SwingSpeedFactor = 1f;
            SwingRegistry.Forget(_character);
            ReadyElites.Forget(_character);
            // Keep Devourers' behavior binding: the same component resumes without another Start.
            if (_movementOwned && _character != null)
            {
                if (_appliedSpeeds.Matches(_character)) _baseSpeeds.ApplyTo(_character, 1f);
                else _baseCaptured = false; // a foreign writer now owns the baseline; capture it on resume
            }
            _movementOwned = false;
            _devourBonus = _swiftBonus = 0f;
            RestoreScale();
            ReconcileComponents();
        }

        private void ReconcileComponents()
        {
            bool active = Ready;
            bool behaviours = active && (_activeFlags & (_isBoss ? 4 : 2)) != 0 && (_activeFlags & 16) == 0;
            foreach (MonoBehaviour owned in _ownedBehaviours)
            {
                if (owned == null) continue;
                // A decoy remains hollow and finishes its existing removal, even when new tricks are disabled.
                if (owned is ExtraPotions.Wildblood.Adopted.Mutations.CloneDecoy) continue;
                if (!behaviours && owned.enabled)
                {
                    _suspendedBehaviours.Add(owned);
                    owned.enabled = false;
                }
                else if (behaviours && _suspendedBehaviours.Remove(owned)) owned.enabled = true;
            }
            foreach (GameObject visual in _ownedVisuals)
            {
                if (visual == null) continue;
                if (!behaviours && visual.activeSelf)
                {
                    _suspendedVisuals.Add(visual);
                    visual.SetActive(false);
                }
                else if (behaviours && _suspendedVisuals.Remove(visual)) visual.SetActive(true);
            }
        }
    }
}
