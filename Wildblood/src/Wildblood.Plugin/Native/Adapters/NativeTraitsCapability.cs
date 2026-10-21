// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses the existing wb1/legacy codec and ECR mutation inheritance carried by Wildblood.Core.
using System;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using CoreTraits = ExtraPotions.Wildblood.Traits;

namespace ExtraPotions.Wildblood.Adopted.Adapters
{
    /// <summary>Real ZDO trait state for Shepherd; never installs birth, sire, keeper, star-roll or egg hooks.</summary>
    public static class NativeTraitsCapability
    {
        public const string Id = "meadworks.creatures.traits";
        public static readonly System.Version Version = new System.Version(1, 0);
        // Tuple order is the five design 9.1 members: Read, Inherit, Apply, EggEncode, EggDecode.
        public static readonly Tuple<Func<object, string>, Func<string, string, int, float, int, string>,
            Action<object, string>, Func<string, string>, Func<string, string>> Provider = Tuple.Create(
                (Func<object, string>)Read, (Func<string, string, int, float, int, string>)Inherit,
                (Action<object, string>)Apply, (Func<string, string>)EggEncode, (Func<string, string>)EggDecode);

        public static void Register(ProductRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            registration.AddCapability(Id, Version, Provider);
            registration.AddCapability<Func<object,string>>("meadworks.creatures.trait_labels", Version, TraitLabels);
        }
        private static ZDO? Data(object target) => target is ZDO zdo ? zdo
            : target is Character creature && creature != null && creature.m_nview != null && creature.m_nview.IsValid() ? creature.m_nview.GetZDO()
            : target is ZNetView view && view != null && view.IsValid() ? view.GetZDO() : null;

        // Optional presentation-only query; ReadSet does not migrate or write the native ZDO.
        public static string TraitLabels(object target)
        {
            try
            {
                if (!WildbloodModules.Active(WildbloodModule.Traits)) return "";
                var zdo = Data(target); if (zdo == null) return "";
                var saved = TraitStore.ReadSet(zdo);
                var words = MutationCatalog.InOrder.Where(m => (saved.Mutations & (1 << (int)m)) != 0)
                    .Select(m => WildbloodText.Text("mutation_" + m.ToString().ToLowerInvariant()))
                    .Concat(saved.Modifiers.Where(m => Enum.IsDefined(typeof(CoreTraits.Modifier), m))
                    .Select(m => WildbloodText.Text("modifier_" + m.ToString().ToLowerInvariant()))).Distinct().Take(49);
                var labels = string.Join(", ", words);
                return labels.Length <= 1024 ? labels : labels.Substring(0,1024);
            }
            catch (Exception error) { Report("trait labels", error); return ""; }
        }

        public static string Read(object target)
        {
            try
            {
                ZDO? zdo = Data(target);
                if (zdo != null) return CoreTraits.TraitBlob.Encode(TraitStore.ReadSet(zdo));
                // Legacy source eggs travel as ItemData. Read their traits without touching quality/custom data.
                if (target is ItemDrop.ItemData item)
                {
                    if (item.m_customData.TryGetValue(TraitKeys.Blob, out string canonical) && CoreTraits.TraitBlob.TryDecode(canonical, out var saved))
                        return CoreTraits.TraitBlob.Encode(saved);
                    if ((item.m_customData.TryGetValue(TraitKeys.EggTraits, out string legacy) || item.m_customData.TryGetValue(TraitKeys.Legacy(TraitKeys.EggTraits), out legacy))
                        && TryLegacyEgg(legacy, out string carried)) return carried;
                }
                return "";
            }
            catch (Exception error) { Report("read", error); return ""; }
        }
        public static string Inherit(string mother, string sire, int newbornLevel, float mutationChance, int seed)
        {
            try
            {
                if (!WildbloodModules.ProductActive(out _) ||
                    ExtraPotions.Wildblood.Runtime.Plugin.Settings?.Get(WildbloodSettings.General.Enabled) != true ||
                    !RuleState.Active.Breeding.Enabled) return "";
                float sourceChance = RuleState.Active.Breeding.MutationChance;
                mutationChance = float.IsNaN(mutationChance) || float.IsInfinity(mutationChance)
                    || float.IsNaN(sourceChance) || float.IsInfinity(sourceChance) ? 0f
                    : Math.Max(0f, Math.Min(100f, Math.Min(mutationChance, sourceChance)));
                int enabled = 0;
                if (WildbloodModules.Active(WildbloodModule.Traits))
                    foreach (Mutation mutation in MutationCatalog.InOrder)
                        if (RuleState.Active.IsEnabled(mutation)) enabled |= 1 << (int)mutation;
                int cap = CoreTraits.Modifiers.Count(ExtraPotions.Wildblood.Runtime.Plugin.Settings?.Get(WildbloodSettings.Traits.CreatureModifiers) ?? CoreTraits.ModifierCap.Off);
                // This data contract has no body/prefab argument; Apply filters the actual newborn's body afterward.
                var inherited = CoreTraits.TraitInheritance.Inherit(mother, sire, mutationChance, enabled, 0,
                    _ => WildbloodModules.Active(WildbloodModule.Traits) && cap > 0, new SeededDice(seed));
                CoreTraits.TraitBlob.TryDecode(inherited, out var traits);
                return CoreTraits.TraitBlob.Encode(new CoreTraits.TraitSet(traits.Mutations,
                    traits.Modifiers.GroupBy(CoreTraits.Modifiers.GroupOf).Select(group => group.First()).Take(cap), traits.Biome));
            }
            catch (Exception error) { Report("inherit", error); return ""; }
        }
        public static void Apply(object target, string blob)
        {
            try
            {
                ZDO? zdo = Data(target);
                if (zdo == null || !zdo.IsOwner() || !WildbloodModules.ProductActive(out _) ||
                    ExtraPotions.Wildblood.Runtime.Plugin.Settings?.Get(WildbloodSettings.General.Enabled) != true ||
                    !CoreTraits.TraitBlob.TryDecode(blob, out var saved)) return;
                Character? creature = target as Character;
                if (creature == null && target is ZNetView view) creature = view.GetComponent<Character>();
                if (creature == null && ZNetScene.instance != null) creature = ZNetScene.instance.GetPrefab(zdo.GetPrefab())?.GetComponent<Character>();
                int barred = creature != null ? MutationBars.Of(creature) : 0;
                var allowed = new CoreTraits.TraitSet(saved.Mutations & ~barred, saved.Modifiers, saved.Biome);
                TraitStore.ApplyBlob(zdo, CoreTraits.TraitBlob.Encode(allowed));
            }
            catch (Exception error) { Report("apply", error); }
        }
        public static string EggEncode(string blob)
        {
            // Pass unknown versions through the existing escape codec so carried records survive a downgrade.
            try { return CoreTraits.TraitBlob.EggEncode(blob); } catch (Exception error) { Report("egg encode", error); return ""; }
        }
        public static string EggDecode(string escaped)
        {
            try
            {
                string decoded = CoreTraits.TraitBlob.EggDecode(escaped);
                return TryLegacyEgg(decoded, out string legacy) ? legacy : decoded;
            }
            catch (Exception error) { Report("egg decode", error); return ""; }
        }
        private static bool TryLegacyEgg(string record, out string blob)
        {
            blob = "";
            string[] parts = record.Split(',');
            if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stars) || stars < 0
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mask) || mask < 0
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int biome) || biome < 0) return false;
            blob = CoreTraits.TraitBlob.Encode(new CoreTraits.TraitSet(mask, Array.Empty<CoreTraits.Modifier>(), TraitStore.PortableBiome((Heightmap.Biome)biome)));
            return true;
        }
        private static void Report(string operation, Exception error) => ExtraPotions.Wildblood.Adopted.Util.Log.Warn("Wildblood traits " + operation + ": " + error.GetType().Name);
    }
}
