// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Integration;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime
{
    internal sealed class NativeCreatureRules : IDisposable
    {
        readonly CreatureRuleRegistry local = new CreatureRuleRegistry();
        readonly ConfigEntry<string> carrier;
        readonly ConfigFile config;
        readonly Action invalidate;
        IReadOnlyDictionary<string, CreatureRule> effective = Empty();
        string applied = "";
        long generation = -1;
        bool disposed;
        internal static NativeCreatureRules? Instance { get; private set; }
        static IReadOnlyDictionary<string, CreatureRule> Empty() => new Dictionary<string, CreatureRule>(StringComparer.Ordinal);

        internal NativeCreatureRules(ConfigFile config, ProductRegistration registration, Action invalidate)
        {
            this.config = config;
            this.invalidate = invalidate;
            var save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try { carrier = MeadworksConfig.BindHidden(config, "Meadworks", "CreatureDefaultsSnapshot", "",
                "Optional creature defaults snapshot, managed by Wildblood.", ConfigScope.ServerAuthoritative); }
            finally { config.SaveOnConfigSet = save; }
            registration.AddCapability<Func<string, string, bool>>(
                CreatureRulesContract.Id, CreatureRulesContract.Version, Register);
            config.SettingChanged += Changed;
            Instance = this;
        }

        bool Register(string prefab, string json)
        {
            if (disposed || !Plugin.Ready || !local.Register(prefab, json)) return false;
            Tick();
            return true;
        }

        void Changed(object sender, SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.ChangedSetting, carrier)) Tick();
        }

        internal void Tick()
        {
            if (disposed) return;
            var session = CruciblePlugin.Session;
            if (generation != session.Generation)
            {
                generation = session.Generation;
                Reset();
            }
            if (!Plugin.Ready || session.Role == SessionRole.Menu || !session.AuthorityReady
                || !MeadworksAuthority.ProductActive(Plugin.Guid, out _))
            {
                Reset();
                return;
            }
            var decision = MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative);
            if (decision.Allowed && !decision.RequiresServerAmendment)
            {
                var snapshot = local.Snapshot;
                if (carrier.Value != snapshot)
                {
                    var result = MeadworksConfig.Amend(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, snapshot);
                    if (!result.Applied) return;
                }
            }
            var received = MeadworksConfig.Effective(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, "");
            if (received == applied) return;
            if (received.Length == 0) { Reset(); return; }
            if (!CreatureRulesContract.TrySnapshot(received, out var rules)) return;
            effective = rules;
            applied = received;
            invalidate();
        }

        void Reset()
        {
            if (applied.Length == 0 && effective.Count == 0) return;
            effective = Empty();
            applied = "";
            invalidate();
        }

        internal static void Project(RuleSet rules)
        {
            var instance = Instance;
            if (instance == null || !instance.Available) return;
            foreach (var pair in instance.effective) rules.CreatureDefaults[pair.Key] = pair.Value;
        }

        bool Available => !disposed && Plugin.Ready && generation == CruciblePlugin.Session.Generation
            && CruciblePlugin.Session.Role != SessionRole.Menu && CruciblePlugin.Session.AuthorityReady
            && MeadworksAuthority.ProductActive(Plugin.Guid, out _);

        internal static CreatureRule? Find(string prefab) => Instance?.Available == true
            && Instance.effective.TryGetValue(prefab, out var rule) ? rule : null;
        internal static CreatureRule? Find(Character creature) => Find(Utils.GetPrefabName(creature.gameObject));
        internal static bool IsBoss(Character creature) => creature.IsBoss() || Find(creature)?.Boss == true;
        internal static int Barred(string prefab) => Find(prefab)?.BarredTraits ?? 0;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            config.SettingChanged -= Changed;
            Reset();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }
}
