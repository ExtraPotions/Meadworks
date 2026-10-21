// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Items;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The running rules, and the one place they change. Every reader takes <see cref="Current"/> (an immutable
    /// snapshot of both YAML families) and, if it precomputes anything from it, listens to <see cref="RulesChanged"/>.
    /// Whether the rules come from this machine's files or from the server is decided by <see cref="RuleFamily{T}"/>.
    /// </summary>
    public static class ActiveRules
    {
        private static RuleFamily<AffixRules>? _affixes;
        private static RuleFamily<EconomyRules>? _economy;
        private static int _generation;

        /// <summary>The active snapshot. <see cref="RuleSet.Empty"/> only before plugin Awake has loaded the rules.</summary>
        public static RuleSet Current { get; private set; } = RuleSet.Empty;

        public static int Generation => Current.Generation;

        /// <summary>Raised on the main thread after <see cref="Current"/> was replaced, on every peer.</summary>
        public static event Action? RulesChanged;
        internal static void Shutdown() { RulesChanged = null; _affixes = null; _economy = null; Current = RuleSet.Empty; _generation = 0; }

        /// <summary>Plugin Awake: write missing main files, read the local files, bind to the server via Charter.</summary>
        internal static void Initialise(BepInEx.Configuration.ConfigFile config)
        {
            EffectRegistry.Freeze();
            _affixes = new RuleFamily<AffixRules>(FamilySpec.Affixes, AffixFamilyParser.Parse);
            _economy = new RuleFamily<EconomyRules>(FamilySpec.Economy, EconomyParser.Parse);
            _affixes.Adopted += Compose;
            _economy.Adopted += Compose;
            _affixes.Setup(config);
            _economy.Setup(config);
            RuleReload.Attach(Poll);
        }

        /// <summary>
        /// Re-reads this machine's files now (<c>ecraft reload</c>) and says per family what happened; a bound player
        /// keeps the server's rules. An applied family raises <see cref="RulesChanged"/> as usual.
        /// </summary>
        public static (FamilyReload Affixes, FamilyReload Economy) ReloadLocal()
        {
            FamilyReload affixes = _affixes?.ReloadLocal() ?? FamilyReload.NoFiles;
            FamilyReload economy = _economy?.ReloadLocal() ?? FamilyReload.NoFiles;
            return (affixes, economy);
        }

        /// <summary>
        /// The texts the active rules of a family were built from (<see cref="FamilySpec.Affixes"/> or
        /// <see cref="FamilySpec.Economy"/>): the server's files on a bound player, this machine's otherwise.
        /// </summary>
        internal static RuleSources SourcesInForce(FamilySpec family) =>
            (family == FamilySpec.Affixes ? _affixes?.InForce : _economy?.InForce) ?? RuleSources.None;

        /// <summary>
        /// After API registrations made once the rules had loaded (at most once per frame, <c>Api/RuleRebuild</c>):
        /// builds the inscription family again from the texts it was last given when <paramref name="inscriptions"/>
        /// (registered inscriptions, pool additions, external effects), and in any case publishes a new
        /// <see cref="Current"/> with a new generation, so every cache built from the rules, item classes, item levels
        /// or loot profiles is dropped. Nothing before the rules first loaded: that load reads every registration.
        /// </summary>
        internal static void RebuildForApi(bool inscriptions)
        {
            if (_affixes?.Active == null || _economy?.Active == null)
            {
                return;
            }
            int before = _generation;
            if (inscriptions)
            {
                _affixes.Rebuild();
            }
            if (_generation == before)
            {
                Compose();
            }
        }

        private static void Poll()
        {
            _affixes?.PollDisk();
            _economy?.PollDisk();
        }

        private static void Compose()
        {
            if (_affixes?.Active == null || _economy?.Active == null)
            {
                return;
            }
            Current = new RuleSet(_affixes.Active, _economy.Active, ++_generation);
            Log.Info($"rules applied (generation {_generation}): {Current.Affixes.Affixes.Count} inscriptions, "
                + $"{Current.Economy.Rarities.Count} rarities, {Current.Economy.Stones.Count} runes, {Current.Economy.Classes.Count} item classes");
            ClassChecks.UnknownClasses(Current, id => ItemClasses.Get(id) != null).ForEach(Log.Warn);
            Raise();
        }

        // Each handler runs on its own: one feature's exception must not keep the others on stale rules.
        private static void Raise()
        {
            Delegate[] handlers = RulesChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (Delegate handler in handlers)
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Log.Error($"a RulesChanged handler threw: {e}");
                }
            }
        }
    }
}
