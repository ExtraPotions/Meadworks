// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Jotunn.Managers;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Whose UI an object is, for styling: the game's own, MeadWorks', or another mod's (<see cref="KitThemeRules.UiOwner"/>).
    /// MeadWorks styling restyles only the first two. Other mods' panels that reuse the game's wood sprites are foreign
    /// even under the game's canvases: Jotunn's CustomGUI roots, other mods' components, or no game component above them.
    /// </summary>
    public static class KitUiOwnership
    {
        static readonly Dictionary<Assembly, int> kinds = new Dictionary<Assembly, int>();
        static readonly List<MonoBehaviour> buffer = new List<MonoBehaviour>();
        static readonly HashSet<Assembly> meadworks = new HashSet<Assembly>();
        static int pluginCount = -1;
        const int Neutral = 0, Valheim = 1, Meadworks = 2, Foreign = 3;

        public static KitUiOwner Of(Transform transform) => transform == null ? KitUiOwner.Foreign : KitThemeRules.UiOwner(Chain(transform));

        /// <summary>The game's own or MeadWorks UI; false for any other mod's.</summary>
        public static bool Styleable(Transform transform) => Of(transform) != KitUiOwner.Foreign;

        static IEnumerable<KitUiNode> Chain(Transform transform)
        {
            RefreshPlugins();
            var front = GUIManager.CustomGUIFront != null ? GUIManager.CustomGUIFront.transform : null;
            var back = GUIManager.CustomGUIBack != null ? GUIManager.CustomGUIBack.transform : null;
            for (var at = transform; at != null; at = at.parent)
            {
                bool valheim = false, ours = false, foreign = false;
                buffer.Clear();
                at.GetComponents(buffer);
                foreach (var component in buffer)
                {
                    if (component == null) continue;
                    switch (Kind(component.GetType().Assembly))
                    {
                        case Valheim: valheim = true; break;
                        case Meadworks: ours = true; break;
                        case Foreign: foreign = true; break;
                    }
                }
                buffer.Clear();
                bool container = (front != null && at == front) || (back != null && at == back);
                yield return new KitUiNode(at.name, valheim, ours, foreign, container);
            }
        }

        static void RefreshPlugins()
        {
            if (Chainloader.PluginInfos.Count == pluginCount) return;
            pluginCount = Chainloader.PluginInfos.Count;
            meadworks.Clear();
            foreach (var info in Chainloader.PluginInfos.Values)
                if (info?.Instance != null && info.Metadata.GUID.StartsWith("com.meadworks.", StringComparison.Ordinal))
                    meadworks.Add(info.Instance.GetType().Assembly);
            kinds.Clear();
        }

        static int Kind(Assembly assembly)
        {
            if (kinds.TryGetValue(assembly, out int kind)) return kind;
            string name = assembly.GetName().Name;
            if (assembly == typeof(Menu).Assembly || name == "Assembly-CSharp") kind = Valheim;
            else if (meadworks.Contains(assembly) || name.StartsWith("Crucible", StringComparison.Ordinal)) kind = Meadworks;
            // Every assembly the game ships in its Managed folder (Unity, gui_framework, assembly_utils, Splatform, ...)
            // is part of the game, not another mod.
            else if (name.StartsWith("Unity", StringComparison.Ordinal) || name == "gui_framework" || name.StartsWith("assembly_", StringComparison.Ordinal) || ShippedWithGame(assembly)) kind = Neutral;
            else kind = Foreign;
            kinds[assembly] = kind;
            return kind;
        }

        static bool ShippedWithGame(Assembly assembly)
        {
            try
            {
                string location = assembly.Location;
                if (string.IsNullOrEmpty(location)) return false;
                return string.Equals(Path.GetFullPath(Path.GetDirectoryName(location) ?? ""), Path.GetFullPath(Paths.ManagedPath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
