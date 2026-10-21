// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the capabilities Inscribe publishes through Crucible (design 7, 9.2).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ExtraPotions.Inscribe.Interop;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Publishes <c>meadworks.items.classes</c> v1 in the exact delegate shape Bestiary resolves
    /// (<see cref="ItemClassesV1.ContractType"/>); Crucible matches a capability by its contract type, so the type argument
    /// below must stay that tuple. Registrations land in <see cref="Registry"/> under the YAML layers (R02b); refused
    /// entries are logged once per call at Verbose. Without a consumer nothing calls it.
    /// </summary>
    internal static class InscribeCapabilities
    {
        public static ItemRegistry Registry { get; private set; } = new ItemRegistry();

        public static void Publish(ProductRegistration product, MeadworksLog log)
        {
            Registry = new ItemRegistry();
            InscribeDecoration.Publish(product);
            InscribeSockets.Publish(product);
            LegendaryRewards.Publish(product);
            SecondaryCooldowns.Publish(product);
            GemRecoveryRuntime.Publish(product);
            AccessoryRarity.Install();
            var contract = ItemClassesV1.Create(Registry);
            var logged = Tuple.Create<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>>(
                (cls, prefab) => { if (!Report(contract.Item1(cls, prefab), log)) return false; Adopted.Items.ItemClasses.ClaimItems(cls, new[] { prefab }); Adopted.Api.RuleRebuild.Request(false); return true; },
                (prefab, level) => { if (!Report(contract.Item2(prefab, level), log)) return false; Adopted.Items.CodeLevels.Set(prefab, level); Adopted.Api.RuleRebuild.Request(false); return true; },
                json => { if (!Report(contract.Item3(json), log)) return false; var p = LootProfile.Parse(json, out _, out _)!; var bonus = p.Bonus.Select(b => new Adopted.Rules.DropBonus { Stone=b.Rune, Chance=b.Chance, Amount=b.Amount }).ToArray();
                    Adopted.Loot.CodeLoot.Set(p.Prefab, p.Boss ? new Adopted.Rules.BossDrop { Tier=p.Tier, StoneRolls=p.RuneRolls, GearRolls=p.GearRolls, ScrollRolls=p.ScrollRolls, Bonus=bonus } : null,
                        new Adopted.Rules.CreatureDrop { Tier=p.Tier, Multiplier=p.Multiplier, StoneMultiplier=p.RuneMultiplier, GearMultiplier=p.GearMultiplier, ScrollMultiplier=p.ScrollMultiplier, Bonus=p.Boss ? Array.Empty<Adopted.Rules.DropBonus>() : bonus });
                    Adopted.Api.RuleRebuild.Request(false); return true; });
            product.AddCapability<Tuple<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>>>(CapabilityIds.ItemClasses, CapabilityIds.Version1, logged);
            product.AddCapability<IReadOnlyDictionary<string, Delegate>>(ExtraPotions.Meadworks.Contracts.CraftingApiContract.Id,
                ExtraPotions.Meadworks.Contracts.CraftingApiContract.ContractVersion, CraftingApi());
        }

        /// <summary>
        /// <c>meadworks.crafting.api</c>: every public static <see cref="Adopted.Api.InscribeApi"/> method under its own name,
        /// as the plain <c>Func&lt;&gt;</c>/<c>Action&lt;&gt;</c> of its signature, so a consumer reads an endpoint with
        /// <c>as Func&lt;...&gt;</c> instead of reflecting into Inscribe. The endpoints keep their own guards.
        /// </summary>
        internal static IReadOnlyDictionary<string, Delegate> CraftingApi()
        {
            var api = new Dictionary<string, Delegate>(StringComparer.Ordinal);
            foreach (MethodInfo method in typeof(Adopted.Api.InscribeApi).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var shape = method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).ToArray();
                api.Add(method.Name, Delegate.CreateDelegate(Expression.GetDelegateType(shape), method)); // names are unique: no overloads
            }
            return api;
        }

        public static void Reset() => Registry = new ItemRegistry();

        private static bool Report(bool accepted, MeadworksLog log)
        {
            if (!accepted && Registry.LastErrors.Count > 0) log.Verbose("meadworks.items.classes refused: " + string.Join("; ", Registry.LastErrors));
            if (Registry.LastWarnings.Count > 0) log.Verbose("meadworks.items.classes warnings: " + string.Join("; ", Registry.LastWarnings));
            return accepted;
        }
    }
}
