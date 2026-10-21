// Adopted from OpenKeep 2.0.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: OpenKeep/OpenKeep/src/Capacity/ContainersModel.cs at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026-10-10 (parity choice #8): sizes may be the keywords double / vanilla or WIDTHxROWS, and a top-level
// chests: line sizes every buildable chest (rules in Burrow.Core ContainerSizeSpec / ContainerSizeDefaults).
using System;
using System.Collections.Generic;
using ExtraPotions.Burrow.Embedded.YamlConfig;
using ExtraPotions.Burrow;

namespace Burrow.Capacity
{
    /// <summary>The parsed Burrow.Containers*.yml files: prefab name to grid size.</summary>
    public sealed class ContainersModel : YamlModel
    {
        /// <summary>Wider than this and the container panel cannot show every column.</summary>
        public const int PanelWidth = 8;

        /// <summary>Each listed prefab's entry: an exact size, double or vanilla.</summary>
        public Dictionary<string, ContainerSizeSpec> Specs { get; } = new Dictionary<string, ContainerSizeSpec>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The <c>chests:</c> line: the size of every chest a player can build that has no line of its own; null when absent.</summary>
        public ContainerSizeSpec? Chests { get; private set; }

        protected override void Read(YamlNode root)
        {
            YamlNode chests = root.Get(ContainerSizeDefaults.ChestsKey);
            if (chests.Kind != YamlNodeKind.Missing)
            {
                if (ContainerSizeSpec.TryParse(chests.Kind == YamlNodeKind.Scalar ? chests.Text : null, out ContainerSizeSpec spec, out string error))
                    Chests = spec;
                else
                    chests.Error("chests: " + error);
            }
            YamlNode containers = root.Get("containers");
            if (containers.Kind == YamlNodeKind.Map)
                foreach (KeyValuePair<string, YamlNode> entry in containers.Entries)
                    ReadSize(entry.Key, entry.Value);
            ReadMisplaced(root);
        }

        /// <summary>An entry uncommented without its indentation lands at the top of the file; read it and say so.</summary>
        private void ReadMisplaced(YamlNode root)
        {
            foreach (KeyValuePair<string, YamlNode> entry in root.Entries)
            {
                if (string.Equals(entry.Key, "containers", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Key, ContainerSizeDefaults.ChestsKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (entry.Value.Kind != YamlNodeKind.Map)
                {
                    entry.Value.Warn("unknown key");
                    continue;
                }
                entry.Value.Warn("this entry sits at the top of the file; indent it two spaces so it is inside 'containers:' (applied anyway)");
                ReadSize(entry.Key, entry.Value);
            }
        }

        private void ReadSize(string prefab, YamlNode node)
        {
            if (node.Kind == YamlNodeKind.Scalar)
            {
                if (ContainerSizeSpec.TryParse(node.Text, out ContainerSizeSpec keyword, out string error))
                {
                    if (keyword.Kind == ContainerSizeKind.Exact && keyword.Width > PanelWidth)
                        node.Warn($"width {keyword.Width} is wider than the container panel ({PanelWidth} columns); the extra columns may not be visible");
                    Specs[prefab] = keyword;
                }
                else
                    node.Error(error);
                return;
            }
            bool hasWidth = node.Get("width").TryInt(out int width);
            bool hasHeight = node.Get("height").TryInt(out int height);
            if (!hasWidth || !hasHeight)
            {
                node.Error("a container needs width and height");
                return;
            }
            if (width < 1 || height < 1)
            {
                node.Error("width and height must be at least 1");
                return;
            }
            if (width > PanelWidth)
                node.Warn($"width {width} is wider than the container panel ({PanelWidth} columns); the extra columns may not be visible");
            Specs[prefab] = ContainerSizeSpec.Exact(width, height);
        }
    }
}
