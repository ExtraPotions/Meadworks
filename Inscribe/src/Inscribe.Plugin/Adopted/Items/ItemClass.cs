// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ItemType = ItemDrop.ItemData.ItemType;
using SkillType = Skills.SkillType;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// One item class (classes-and-tiers.md section 1): what an item is for inscriptions (each inscription lists the
    /// classes it rolls on) and for gear drops. Built by the rules loader from the economy YAML's <c>classes:</c>
    /// entries, or by code and handed to <see cref="ItemClasses.RegisterClass"/>. Never changed once registered or
    /// loaded: treat every setter as builder-only.
    /// </summary>
    public sealed class ItemClass
    {
        /// <summary>The YAML keys of a class entry, in file order.</summary>
        internal static readonly string[] Keys = { "id", "group", "name", "rolls", "damage_scale", "drop_weight", "match", "items" };

        public string Id { get; internal set; } = "";

        /// <summary>Display grouping: onehand, twohand, ranged, magic, offhand, armour, jewel, tools, none.</summary>
        public string Group { get; internal set; } = "none";

        /// <summary>A <c>$key</c> (default <c>$ecf_class_&lt;id&gt;</c>) or literal text.</summary>
        public string Name { get; internal set; } = "";

        /// <summary>False: items of the class never become magic.</summary>
        public bool Rolls { get; internal set; } = true;

        /// <summary>Multiplies <c>scaled</c> inscriptions when they roll.</summary>
        public float DamageScale { get; internal set; } = 1f;

        /// <summary>Multiplies the class's bases in the gear drop pool; 0 = never drops pre-rolled.</summary>
        public float DropWeight { get; internal set; } = 1f;

        /// <summary>Any of these rules classifies an item into the class (step 3 of classification).</summary>
        public IReadOnlyList<ClassMatch> Match { get; internal set; } = Array.Empty<ClassMatch>();

        /// <summary>Prefab names that belong to the class whatever they are (step 1).</summary>
        public IReadOnlyList<string> Items { get; internal set; } = Array.Empty<string>();

        /// <summary>
        /// The keys a YAML entry named. A rules class with the id of a class registered by code changes only these
        /// fields of it (registered classes sit under the YAML layers, api.md section 1). Empty for registered classes.
        /// </summary>
        internal HashSet<string> Named { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>A class with every field at its default.</summary>
        public static ItemClass Create(string id) => new ItemClass { Id = id, Name = "$ecf_class_" + id };

        /// <summary>This rules class laid over a registered class of the same id: the fields this one named win.</summary>
        internal ItemClass Over(ItemClass under)
        {
            bool Has(string key) => Named.Contains(key);
            return new ItemClass
            {
                Id = Id,
                Group = Has("group") ? Group : under.Group,
                Name = Has("name") ? Name : under.Name,
                Rolls = Has("rolls") ? Rolls : under.Rolls,
                DamageScale = Has("damage_scale") ? DamageScale : under.DamageScale,
                DropWeight = Has("drop_weight") ? DropWeight : under.DropWeight,
                Match = Has("match") ? Match : under.Match,
                Items = Has("items") ? Items : under.Items,
                Named = Named,
            };
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// One <c>match</c> rule of a class: it holds when every key it has holds (a null list or flag = key absent).
    /// <c>types</c> and <c>skills</c> match the game's enum names, <c>two_handed</c> the TwoHandedWeapon(Left) types,
    /// <c>stackable</c> a max stack above 1, <c>prefixes</c> and <c>name_contains</c> the prefab name.
    /// </summary>
    public sealed class ClassMatch
    {
        public IReadOnlyList<ItemType>? Types { get; internal set; }
        public IReadOnlyList<SkillType>? Skills { get; internal set; }
        public bool? TwoHanded { get; internal set; }
        public bool? Stackable { get; internal set; }
        public IReadOnlyList<string>? Prefixes { get; internal set; }
        public IReadOnlyList<string>? NameContains { get; internal set; }

        /// <summary>Whether an item type meets the rule; <paramref name="prefab"/> null fails the name keys.</summary>
        public bool Meets(ItemDrop.ItemData.SharedData shared, string? prefab)
        {
            return (Types == null || Contains(Types, shared.m_itemType))
                && (Skills == null || Contains(Skills, shared.m_skillType))
                && (TwoHanded == null || TwoHanded.Value == IsTwoHanded(shared.m_itemType))
                && (Stackable == null || Stackable.Value == shared.m_maxStackSize > 1)
                && (Prefixes == null || AnyName(Prefixes, prefab, starts: true))
                && (NameContains == null || AnyName(NameContains, prefab, starts: false));
        }

        private static bool IsTwoHanded(ItemType type) => type == ItemType.TwoHandedWeapon || type == ItemType.TwoHandedWeaponLeft;

        private static bool Contains<T>(IReadOnlyList<T> list, T value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(list[i], value))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyName(IReadOnlyList<string> parts, string? prefab, bool starts)
        {
            for (int i = 0; prefab != null && i < parts.Count; i++)
            {
                bool hit = starts ? prefab.StartsWith(parts[i], StringComparison.Ordinal) : prefab.IndexOf(parts[i], StringComparison.Ordinal) >= 0;
                if (hit)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
