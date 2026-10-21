// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the native modifiers `wildblood spawn` forces, through CreatureModifierManager.TryApplyForcedModifiers.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// The native (CreatureManager) modifiers named on <c>wildblood spawn</c>. <c>mod:&lt;key&gt;</c> always names one; a bare
    /// word does too when it is not a mutation (a boss's bare words stay aspects). They go on only after the controller has
    /// written the forced traits, so the trait-overlap rule sees the mutations: a modifier with the same effect as one of
    /// them is dropped and reported, and the mutation wins. At most four, as the native path allows.
    /// </summary>
    public static class SpawnModifiers
    {
        const string Prefix = "mod:";
        const float ResolveSeconds = 10f;

        /// <summary>The trait words from <paramref name="first"/> on, with the modifier words moved to <paramref name="modifiers"/>
        /// as canonical keys. An unknown <c>mod:</c> word is warned and skipped.</summary>
        public static List<string> Split(Terminal.ConsoleEventArgs args, int first, bool boss, out List<string> modifiers)
        {
            List<string> words = new List<string>();
            modifiers = new List<string>();
            for (int i = first; i < args.Length; i++)
            {
                string word = args[i];
                bool prefixed = word.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
                string name = prefixed ? word.Substring(Prefix.Length) : word;
                if (!prefixed && (boss || MutationCatalog.FromName(word).HasValue))
                {
                    words.Add(word);
                }
                else if (Key(name) is string key)
                {
                    if (!modifiers.Contains(key)) modifiers.Add(key);
                }
                else if (prefixed)
                {
                    CommandReply.Send(args, $"wildblood spawn: unknown modifier '{name}', skipped.");
                }
                else
                {
                    words.Add(word); // reported as an unknown mutation where the words are read
                }
            }
            return words;
        }

        static string? Key(string name) =>
            name.Length > 0 && CreatureModifierManager.TryNormalizeForcedModifierKeys(new[] { name }, out List<string> keys, out _) && keys.Count == 1
                ? keys[0] : null;

        /// <summary>Applies <paramref name="modifiers"/> once <paramref name="controller"/> is ready and reports what stayed.</summary>
        public static void ApplyWhenResolved(Terminal.ConsoleEventArgs args, EliteController controller, List<string> modifiers)
        {
            if (modifiers.Count > 0) controller.StartCoroutine(Apply(args, controller, modifiers));
        }

        static IEnumerator Apply(Terminal.ConsoleEventArgs args, EliteController controller, List<string> modifiers)
        {
            float until = Time.time + ResolveSeconds;
            while (controller != null && !controller.Ready && Time.time < until) yield return null;
            Character? creature = controller != null && controller.Ready ? controller.Creature : null;
            if (creature == null)
            {
                CommandReply.Send(args, WildbloodText.Text("spawn_modifiers_unresolved"));
                yield break;
            }
            if (!CreatureModifierManager.TryApplyForcedModifiers(creature, modifiers, out string error))
            {
                CommandReply.Send(args, WildbloodText.Format("spawn_modifiers_failed", error));
                yield break;
            }
            List<string> kept = CreatureModifierManager.StoredModifierKeys(creature);
            foreach (string dropped in modifiers.Where(m => !kept.Contains(m, StringComparer.OrdinalIgnoreCase)))
                CommandReply.Send(args, WildbloodText.Format("spawn_modifier_dropped", CreatureModifierManager.GetModifierDisplayName(dropped)));
            if (kept.Count > 0)
                CommandReply.Send(args, WildbloodText.Format("spawn_modifiers_applied", string.Join(", ", kept.Select(CreatureModifierManager.GetModifierDisplayName))));
        }
    }
}
