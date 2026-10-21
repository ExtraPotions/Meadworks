// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Foraging/Forage/ForageModel.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The parsed Saga.Forage*.yml files: under <c>forage:</c> one entry per item prefab, a map holding its
    /// <c>experience</c>. A later file replaces an earlier file's entry for the same item. A negative experience is an
    /// error, so the file is rejected and the previous one stays. Item names are not checked here: the item database may
    /// not exist yet.
    /// </summary>
    public sealed class ForageModel : YamlModel
    {
        public Dictionary<string, ForageEntry> Items { get; } = new Dictionary<string, ForageEntry>(StringComparer.OrdinalIgnoreCase);

        protected override void Read(YamlNode root)
        {
            YamlNode forage = root.Get("forage");
            if (forage.Kind == YamlNodeKind.Missing || forage.Kind == YamlNodeKind.Null)
                return;
            foreach (KeyValuePair<string, YamlNode> entry in forage.Entries)
                ReadItem(entry.Key.Trim(), entry.Value);
        }

        protected override void Verify()
        {
            if (Errors.Count == 0 && Items.Count == 0)
                Warnings.Add("no items are listed under 'forage'; no pick trains Foraging");
        }

        private void ReadItem(string item, YamlNode node)
        {
            if (item.Length == 0)
            {
                node.Error("an item needs its prefab name as the key, for example 'Raspberry:'");
                return;
            }
            if (node.Kind == YamlNodeKind.Null)
            {
                Items[item] = new ForageEntry(item, 1f);
                return;
            }
            if (node.Kind != YamlNodeKind.Map)
            {
                node.Error("an item is a map like { experience: 1 }");
                return;
            }
            // Keys older files still carry, read and ignored so those files load without unknown-key warnings.
            node.Get("stars");
            node.Get("best");
            Items[item] = new ForageEntry(item, ReadExperience(node.Get("experience")));
        }

        /// <summary>1 when not given; a negative factor is an error.</summary>
        private static float ReadExperience(YamlNode node)
        {
            if (!node.TryFloat(out float experience))
                return 1f;
            if (experience >= 0f)
                return experience;
            node.Error("experience cannot be negative");
            return 0f;
        }
    }
}
