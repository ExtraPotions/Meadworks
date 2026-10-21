// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; the Distance Scaling multipliers are stored at the roll.
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// One per creature and the single home of its resolved identity at runtime. The multiplayer rule it enforces is
    /// "rolled once by the owner, stored in the ZDO, read by everyone": the owner rolls and writes; every machine loads
    /// and applies. A machine that meets a creature its owner has not rolled yet stays pending and keeps checking, so it
    /// updates itself exactly once, cleanly, the moment the state arrives - and a creature handed to a machine before it
    /// was ever rolled is rolled by that new owner. It publishes its stars as vanilla level = stars + 1 (unless stars are
    /// off for its kind, <see cref="RuleSet.KeepsLevel"/>), applies the fixed scaling,
    /// and installs the per-frame behaviours (which self-gate on ownership). Patches read the traits and rules from here.
    /// </summary>
    public sealed partial class EliteController : MonoBehaviour
    {
        private Character _character = null!;
        private ZNetView _nview = null!;
        private BaseSpeeds _baseSpeeds;
        private bool _ready;
        private bool _pending;
        private bool _dressPending;
        internal bool ReapingScaleReady { get; private set; }
        private bool _movementClamped;
        private CreatureTraits? _forced;
        private BossDraw? _forcedDraw;
        private Heightmap.Biome _forcedBiome;
        private bool _isBoss;

        private CreatureTraits _traits = new CreatureTraits(0, 0);
        private static readonly CreatureTraits InactiveTraits = new CreatureTraits(0, 0);
        public CreatureTraits Traits { get => RuntimeAvailable ? _traits : InactiveTraits; private set => _traits = value; }
        public BiomeRules Rules { get; private set; } = RuleState.Active.Defaults;
        public bool FreshlyResolved { get; private set; }
        public float SwingSpeedFactor { get; private set; } = 1f;
        public Character Creature => _character;
        public ZNetView View => _nview;
        public bool Ready => _ready && RuntimeAvailable && !LootOnly && !HordeOnly;
        /// <summary>Imported CM creatures retain native levels and their source stat baseline.</summary>
        public bool KeepsNativeLevel => LootOnly || HordeOnly || _nview != null && _nview.IsValid() && TraitStore.KeepsNativeLevel(_nview.GetZDO());

        /// <summary>The decorated name the hover name shows, kept while the name it decorates stays the same.</summary>
        public NameMemo Name { get; } = new NameMemo();

        private void Start() => Guard.Run("EliteController.Start", Setup);

        private void Setup()
        {
            _character = GetComponent<Character>();
            _nview = _character != null ? _character.GetComponent<ZNetView>() : null!;
            if (_character == null || _nview == null || !_nview.IsValid() || _character.IsPlayer())
            {
                enabled = false;
                return;
            }
            _isBoss = _character.IsBoss();
            _sessionGeneration = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            _activeFlags = CurrentFlags;
            RuleState.Changed += OnRulesChanged;
            if (!TryResolve())
            {
                _pending = true; // a non-owner meeting an unrolled creature: wait, then apply exactly once
                return;
            }
            Apply();
        }

        // While pending it polls the ZDO until the owner's roll arrives (or this machine becomes owner).
        // Resolved controllers keep a bounded lifecycle poll to suspend/resume the same source components. No flicker: the creature is plain until this fires.
        // It also runs while a disguised creature (a dormant Elite Creatures Pack mimic) waits to be dressed, and dresses it once it wakes.
        private void Update()
        {
            Guard.Run("EliteController.Lifecycle", RefreshLifecycle);
            if (!RuntimeAvailable) return;
            if (_pending)
            {
                Guard.Run("EliteController.Resolve", static self => self.PollResolve(), this);
            }
            else if (_dressPending && !Disguise.Holds(_character))
            {
                _dressPending = false;
                Guard.Run("EliteController.Dress", Dress);
            }
        }

        private void OnDestroy()
        {
            SuspendRuntime();
            RuleState.Changed -= OnRulesChanged;
            SwingRegistry.Forget(_character);
            Devourers.Forget(_character);
            ReadyElites.Forget(_character);
        }

        // An edited or newly synced rule file reaches creatures already loaded: every power read from Rules as it acts
        // (Warding's reflect and its ceiling, Leeching, knockback...) takes the new value at once. What was applied
        // from the rules when it loaded (health, size, speed) stays until the creature next loads.
        private void OnRulesChanged() => Guard.Run("EliteController.Rules", () =>
        {
            ZDO? zdo = Ready && _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
            if (zdo != null)
            {
                Rules = ResolveRules(TraitStore.GetBiome(zdo));
            }
        });

        private void PollResolve()
        {
            if (_nview == null || !_nview.IsValid() || !TryResolve())
            {
                return;
            }
            _pending = false;
            Apply();
        }

        /// <summary>Owner rolls if unrolled; every machine then loads. False when a non-owner meets it before the roll.</summary>
        private bool TryResolve()
        {
            // Both adopted sections must be committed for this exact session before any first roll or migration.
            if (!RuntimeAvailable) return false;
            ZDO zdo = _nview.GetZDO();
            if (zdo == null)
            {
                return false;
            }
            if (LootOnly || HordeOnly)
            {
                // Loot is an independent module: read native identity, never roll/migrate or apply creature stats.
                RefreshLootIdentity();
                return true;
            }
            if (_nview.IsOwner())
            {
                ExtraPotions.Wildblood.AdoptedCm.CreatureKarmaManager.MigrateOwnedPressureState(zdo);
                if (!TraitStore.CanUpdateModifiers(zdo)) return false; // preserve opaque future state without a replacement roll
                ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.MigrateOwnedState(zdo);
                TraitStore.MigrateNativeLevel(zdo);
                if (!TraitStore.IsResolved(zdo)) RollFresh(zdo);
            }
            if (!TraitStore.IsResolved(zdo))
            {
                return false;
            }
            Traits = TraitStore.Load(zdo);
            if (_nview.IsOwner())
            {
                PublishNativeLevel(zdo);
                ExtraPotions.Wildblood.AdoptedCm.CreatureLevelManager.SynchronizePreservedNativeState(_character, zdo);
                ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.TryRollModifiers(_character);
                Traits = TraitStore.Load(zdo); // canonical source results include merged mutations before behaviour installation
            }
            ProjectTraits();
            Traits.Mask &= ~MutationBars.OfKind(PrefabName); // a cloaked Deathsquito saved before the bar loads plain
            Rules = ResolveRules(TraitStore.GetBiome(zdo));
            return true;
        }

        /// <summary>
        /// A boss scales on the boss table; every other creature on its biome's, with its own `creatures:` entry's
        /// mutation keys on top. Every machine resolves the same: the biome is in the ZDO, the prefab is the object's.
        /// A creature that keeps its game level takes no star power: the game's level scales it instead.
        /// </summary>
        private BiomeRules ResolveRules(Heightmap.Biome biome)
        {
            RuleSet set = RuleState.Active;
            bool keeps = KeepsNativeLevel || !StarsActive || set.KeepsLevel(Traits.Stars, _isBoss);
            if (_isBoss)
            {
                return BossView.For(set.Boss, unstarred: keeps);
            }
            BiomeRules rules = set.For(biome, PrefabName);
            return keeps ? set.Unstarred(rules) : rules;
        }

        private string PrefabName => Utils.GetPrefabName(gameObject);

        // OWNER ONLY: the single roll, written to the ZDO for everyone. Reached here only when this machine owns it. When
        // a console spawn has forced exact traits, those are written verbatim instead - bypassing every chance and cap.
        // Stars stay visible through the native level contract. Health, attack and loot compensate for
        // native growth at their own narrow seams; keep-existing creatures retain their supplied level.
        private void RollFresh(ZDO zdo)
        {
            Heightmap.Biome biome = _forced != null ? _forcedBiome : Heightmap.FindBiome(_character.transform.position);
            CreatureTraits fresh = _forced ?? RollFor(biome);
            // Only the two approved legacy merges survive a fresh ECR roll; birth blobs are already resolved and never enter here.
            int merged = ExtraPotions.Wildblood.Traits.Mutations.Bit(ExtraPotions.Wildblood.Traits.Mutation.Blinking)
                | ExtraPotions.Wildblood.Traits.Mutations.Bit(ExtraPotions.Wildblood.Traits.Mutation.Corrodent);
            fresh.Mask |= TraitStore.ReadSet(zdo).Mutations & merged;
            fresh.Mask &= ~MutationBars.OfKind(PrefabName);
            TraitStore.Save(zdo, fresh);
            TraitStore.SetBiome(zdo, biome);
            // Distance Scaling: the rule file's distanceScaling band, read once where the creature is rolled (nothing when the setting is off).
            if (StarsActive && ExtraPotions.Wildblood.AdoptedCm.CreatureLevelManager.TrySelectStatDistanceScaling(_character, out float distanceDamage, out float distanceHealth))
            {
                TraitStore.SetDistanceScaling(zdo, distanceDamage, distanceHealth);
            }
            if (StarsActive && !KeepsNativeLevel && !RuleState.Active.KeepsLevel(fresh.Stars, _isBoss))
            {
                _character.SetLevel(Mathf.Clamp(fresh.Stars, 0, 8) + 1);
            }
            FreshlyResolved = true;
            _forced = null;
            _forcedDraw = null;
        }

        // A boss draws its stars from the boss distribution and an aspect - both the altar's, locked at the offering,
        // when it was summoned at one, else its own roll - and never a mutation, so the roll cannot be shared with the
        // creature path.
        private CreatureTraits RollFor(Heightmap.Biome biome)
        {
            if (_isBoss)
            {
                return (_forcedDraw ?? BossDraw.Roll(PrefabName)).ToTraits(); // a Bountiful roll brings its extras with it
            }
            return TraitRoller.Roll(RuleState.Active.For(biome, PrefabName), RuleState.Active, WorldTier.Current(), biome,
                MutationBars.Of(_character)); // a large body never rolls Gilded or Relentless, a Deathsquito never Cloaked
        }

        /// <summary>
        /// Console-spawn hook: force exact traits and biome, bypassing every roll and cap. Called on the machine that
        /// just instantiated (and therefore owns) the creature, in the same frame, before this component's Start runs, so
        /// the forced traits are written as a fresh roll and the creature fills to its scaled health.
        /// </summary>
        public void ForceTraits(CreatureTraits traits, Heightmap.Biome biome)
        {
            _forced = traits;
            _forcedBiome = biome;
        }

        /// <summary>
        /// Altar hook: the stars and aspects locked in at the offering (a Bountiful one's extras with them), handed over in
        /// the same frame the altar instantiates the boss, so the boss is exactly what the bowl showed. Ignored once the
        /// boss has been rolled.
        /// </summary>
        public void ForceDraw(BossDraw draw) => _forcedDraw = draw;

        // The numbers apply at once, so a hit that wakes a disguised creature is already scaled and its health is not
        // refilled on waking. What can be seen - size, the star look, the mutation and aspect behaviours - waits while a
        // disguise holds (Update dresses it on waking): a dormant mimic must look exactly like the chest it copies.
        private void PublishNativeLevel(ZDO zdo)
        {
            // Supplied breeding and imported CM native levels are authoritative, including levels above nine.
            if (!StarsActive || KeepsNativeLevel || TraitKeys.Int(zdo, TraitKeys.Source) ==
                (int)ExtraPotions.Wildblood.Stars.SpawnSource.Breeding || RuleState.Active.KeepsLevel(Traits.Stars, _isBoss)) return;
            int level = Mathf.Clamp(Traits.Stars, 0, 8) + 1;
            if (_character.GetLevel() != level) _character.SetLevel(level);
        }

        internal Heightmap.Biome LootBiome { get; private set; }

        internal void RefreshLootIdentity(int? nativeLevel = null)
        {
            if (!(LootOnly || HordeOnly) || !RuntimeAvailable || _character == null) return;
            Traits = new CreatureTraits(Mathf.Max(0, (nativeLevel ?? _character.GetLevel()) - 1), 0);
            LootBiome = Heightmap.FindBiome(_character.transform.position);
            Rules = ResolveRules(LootBiome);
            FreshlyResolved = false;
        }

        private void Apply()
        {
            if (LootOnly || HordeOnly)
            {
                if (HordeOnly && _nview.IsOwner()) StatApplier.ApplyHealth(_character, Rules, Traits, freshlyResolved: false);
                _ready = true;
                FreshlyResolved = false;
                return;
            }
            EliteRpc.EnsureRegistered();
            CreatureRpc.Register(_nview);
            ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.SuspendReapingRuntime(_character);
            ReapingScaleReady = false;
            if (_nview.IsOwner()) PublishNativeLevel(_nview.GetZDO());
            CaptureMovement();
            SwingSpeedFactor = StatMath.SwingSpeedMultiplier(Rules, Traits);
            RefreshMovement(0f);
            if (_nview.IsOwner() && (!KeepsNativeLevel || ExtraPotions.Wildblood.Adopted.Horde.HordeSettings.Enabled))
            {
                StatApplier.ApplyHealth(_character, Rules, Traits, FreshlyResolved); // owner writes s_maxHealth; others read it
            }
            _ready = true;
            ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.ResumeReapingRuntime(_character, health: true, scale: false);
            ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.ApplyStoredRuntimeState(_character);
            CreatureRpc.RegisterCommands(this, _isBoss); // on every machine, so a routed command reaches the owner
            ReadyElites.Track(this); // so the nameplate finds it without a component search
            SwingRegistry.Track(this); // the swing speed is fixed from here on, so AnimSpeedPatch skips a creature at 1
            Devourers.Track(this); // so the enmity patch finds a devourer without a component search
            if (Disguise.Holds(_character))
            {
                _dressPending = true; // keep polling in Update until it wakes
                return;
            }
            Dress();
            FreshlyResolved = false; // Arrive is consumed once, including across module suspension/resume.
        }

        private void Dress()
        {
            ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.SuspendReapingRuntime(_character, health: false, scale: true);
            ReapingScaleReady = false;
            RestoreScale();
            Vector3 before = _character.transform.localScale;
            StatApplier.ApplySize(_character, Rules, Traits);
            RememberScale(before); // local, deterministic: every machine scales its own copy
            ReapingScaleReady = true;
            ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.ResumeReapingRuntime(_character, health: false, scale: true);
            StarLook.Apply(_character, Traits.Stars); // the game's tint for the stars, before any mutation reads materials
            if (!_isBoss)
            {
                BehaviourInstaller.Install(this); // mutation behaviours; a boss has no mutations to install
            }
            else
            {
                AspectInstaller.Install(this); // a boss's aspect behaviours, and on its first roll the twin or phantoms
            }
            ReconcileComponents();
            FreshlyResolved = false;
        }

        /// <summary>
        /// Re-derives the creature's speeds from its captured base plus live additive bonuses (Devouring's slow and the
        /// Swift modifier), so a repeated call replaces the previous result instead of stacking on it.
        /// </summary>
        public void RefreshMovement(float devourBonus)
        {
            if (_character != null && RuntimeAvailable)
            {
                _devourBonus = devourBonus;
                _swiftBonus = ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.GetSwiftMovementBonus(_character);
                float factor = StatMath.MoveMultiplier(Rules, Traits, devourBonus, _swiftBonus);
                _baseSpeeds.ApplyTo(_character, ClampToPlayer(factor));
                _appliedSpeeds = BaseSpeeds.Capture(_character);
                _movementOwned = true;
            }
        }

        /// <summary>Re-applies movement once when the Swift modifier was added, removed or retuned after the last apply.</summary>
        private void RefreshSwiftMovement()
        {
            if (!_movementOwned || _character == null) return;
            float current = ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.GetSwiftMovementBonus(_character);
            if (ExtraPotions.Wildblood.Traits.SwiftMovement.NeedsReapply(_swiftBonus, current)) RefreshMovement(_devourBonus);
        }

        /// <summary>No creature may outrun an unburdened player, however the multipliers land; clamp and log, not obey.</summary>
        private float ClampToPlayer(float factor)
        {
            float baseRun = _baseSpeeds.RunSpeed;
            float cap = PlayerSpeed.Reference();
            if (baseRun <= 0f || baseRun * factor <= cap)
            {
                return factor;
            }
            if (!_movementClamped)
            {
                _movementClamped = true;
                Log.Info($"{name}: run speed clamped to the player's ({cap:0.#}) so it stays outrunnable");
            }
            return cap / baseRun;
        }

        /// <summary>Re-derives maximum health from the current traits and accumulated Devouring, on the owner.</summary>
        public void RefreshHealth()
        {
            if (_character != null && IsOwner())
            {
                StatApplier.ApplyHealth(_character, Rules, Traits, freshlyResolved: false);
            }
        }

        public bool IsOwner() => Ready && OwnsCurrentView();
    }
}
