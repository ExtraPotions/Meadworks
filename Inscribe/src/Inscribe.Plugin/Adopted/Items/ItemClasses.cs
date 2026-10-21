// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExtraPotions.Inscribe.Adopted.Rules;
using SkillType = Skills.SkillType;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Item classes (classes-and-tiers.md section 1): which class an item is, and the magic-base question. Classes are
    /// data: the economy YAML's <c>classes:</c> section, plus what code registers through the internal methods below
    /// (the public API, <c>Api/InscribeApi</c>, wraps them). The result is cached per <c>SharedData</c>, which the game shares between
    /// every copy of an item type, and recomputed after a rules change or a registration, so a lookup is one table hit
    /// and two compares.
    /// </summary>
    public static class ItemClasses
    {
        private sealed class Entry
        {
            public int Generation = -1;
            public int Version = -1;
            public ClassInfo Info = ClassInfo.None;
        }

        private static readonly ConditionalWeakTable<ItemDrop.ItemData.SharedData, Entry> Cache =
            new ConditionalWeakTable<ItemDrop.ItemData.SharedData, Entry>();

        /// <summary>Prefix of every rune prefab name (<c>ECF_Awakening</c>).</summary>
        public const string StonePrefabPrefix = "ECF_";

        /// <summary>Prefix of every rune's shared name token (<c>$ecf_stone_awakening</c>).</summary>
        public const string StoneNamePrefix = "$ecf_stone_";

        /// <summary>The item's class with its hands, traits and governing skills; <see cref="ClassInfo.None"/> when it has none.</summary>
        public static ClassInfo Classify(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
            {
                return ClassInfo.None;
            }
            Entry entry = Cache.GetOrCreateValue(item.m_shared);
            int generation = ActiveRules.Generation, version = ClassRegistry.Version;
            if (entry.Generation == generation && entry.Version == version)
            {
                return entry.Info;
            }
            string? prefab = ItemTier.PrefabName(item);
            ClassInfo info = ClassClassifier.Classify(item, prefab, ClassRegistry.Index);
            if (prefab != null)
            {
                // Without a prefab name the name rules could not run: answer, but ask again once the database knows it.
                entry.Generation = generation;
                entry.Version = version;
                entry.Info = info;
            }
            return info;
        }

        /// <summary>The item's class, or null.</summary>
        public static ItemClass? ClassOf(ItemDrop.ItemData? item) => Classify(item).Class;

        /// <summary>A class by id among the effective classes (the YAML's and the registered ones), or null.</summary>
        public static ItemClass? Get(string? id) =>
            id != null && ClassRegistry.Index.ById.TryGetValue(id, out ItemClass found) ? found : null;

        /// <summary>Every effective class, in classification order.</summary>
        public static IReadOnlyList<ItemClass> All => ClassRegistry.Index.Classes;

        /// <summary>Bumped by every registration (caches built from classes compare it, with the rules generation).</summary>
        public static int Version => ClassRegistry.Version;

        /// <summary>One of our runes: by prefab name or by shared name token. Runes never carry item state.</summary>
        public static bool IsStone(ItemDrop.ItemData? item)
        {
            if (item?.m_shared == null)
            {
                return false;
            }
            return IsStoneName(item.m_dropPrefab != null ? item.m_dropPrefab.name : null, item.m_shared.m_name);
        }

        /// <summary>A rune's prefab name (<c>ECF_</c>) or shared name token (<c>$ecf_stone_</c>).</summary>
        internal static bool IsStoneName(string? prefab, string? sharedName) =>
            (prefab != null && prefab.StartsWith(StonePrefabPrefix, StringComparison.Ordinal))
            || (sharedName != null && sharedName.StartsWith(StoneNamePrefix, StringComparison.Ordinal));

        /// <summary>
        /// May carry inscriptions: a class with <c>rolls: true</c>, a max stack of 1, not a rune, and no API magic-base
        /// filter vetoes it (<see cref="MagicBaseFilters"/>). Read live, since another mod may change stack sizes at runtime.
        /// </summary>
        public static bool IsMagicBase(ItemDrop.ItemData? item)
        {
            return item?.m_shared != null
                && item.m_shared.m_maxStackSize == 1
                && Classify(item).Rolls
                && !IsStone(item)
                && MagicBaseFilters.Allows(item);
        }

        /// <summary>Single-instance equipment for carried/table runes; drop eligibility stays unchanged.</summary>
        internal static bool IsRuneBase(ItemDrop.ItemData? item) => item?.m_shared != null
            && item.m_stack == 1 && item.m_shared.m_maxStackSize == 1
            && ClassifyForRune(item).Rolls && MagicBaseFilters.Allows(item);

        internal static ClassInfo ClassifyForRune(ItemDrop.ItemData? item) => item?.m_shared == null ? ClassInfo.None
            : ClassClassifier.ForRune(item, ItemTier.PrefabName(item), ClassRegistry.Index);

        /// <summary>Whether an affix's <c>requires</c> block accepts this item type.</summary>
        public static bool Satisfies(ClassInfo info, AffixRequirements requires)
        {
            if (requires.Hands != Hands.None && info.Hands != requires.Hands)
            {
                return false;
            }
            if (!info.HasTraits(requires.Traits))
            {
                return false;
            }
            return requires.Skills.Count == 0 || AnyGoverned(info, requires.Skills);
        }

        private static bool AnyGoverned(ClassInfo info, IReadOnlyList<SkillType> skills)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (info.IsGovernedBy(skills[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- registration (internal: the public API facade wraps these; api.md section 2)

        /// <summary>
        /// Adds a class, or replaces the registered one with its id. It sits under the YAML: a YAML class with the same
        /// id changes only the fields it names. False for an invalid id.
        /// </summary>
        internal static bool RegisterClass(ItemClass def)
        {
            if (def == null || !Core.Ids.IsValid(def.Id))
            {
                return false;
            }
            ClassRegistry.Register(def);
            return true;
        }

        /// <summary>Puts prefabs in a class (step 1, after the YAML <c>items</c> lists; the first claim of a prefab wins).</summary>
        internal static bool ClaimItems(string classId, IEnumerable<string> prefabs)
        {
            if (!Core.Ids.IsValid(classId) || prefabs == null)
            {
                return false;
            }
            ClassRegistry.Claim(classId, prefabs);
            return true;
        }

        /// <summary>
        /// Adds a classifier (step 2, registration order, the first non-null class id wins), or replaces the one with
        /// this id. It is asked once per item type and must decide from the type (shared data, prefab), not the instance.
        /// </summary>
        internal static bool AddClassifier(string id, Func<ItemDrop.ItemData, string?> classify)
        {
            if (string.IsNullOrEmpty(id) || classify == null)
            {
                return false;
            }
            ClassRegistry.AddClassifier(id, classify);
            return true;
        }

        internal static bool RemoveClassifier(string id) => ClassRegistry.RemoveClassifier(id);
    }
}
