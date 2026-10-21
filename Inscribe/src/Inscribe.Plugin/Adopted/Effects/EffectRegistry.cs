// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Effect id → registration record. The table is filled from <see cref="EffectCatalog"/> the first time anything
    /// touches the registry, so it is complete before the affix YAML is validated (rules load in plugin Awake).
    /// <para>
    /// To add an effect: add a line to <see cref="EffectCatalog"/> (the effects area owns that file) and implement it
    /// with a Harmony patch or an aggregate channel. <see cref="Register"/> after the rules have loaded throws: an id
    /// the YAML could not see at validation time would make the same file valid or invalid depending on timing.
    /// </para>
    /// <para>
    /// External effects (api.md section 3) are the one exception, through <see cref="RegisterExternal"/>: another mod
    /// registers them whenever it loads, and the API rebuilds the inscription rules afterwards, so the YAML is validated
    /// again with them in.
    /// </para>
    /// </summary>
    public static class EffectRegistry
    {
        private static readonly Dictionary<string, EffectDef> Table = new Dictionary<string, EffectDef>(StringComparer.Ordinal);
        private static readonly Dictionary<string, EffectScope> External = new Dictionary<string, EffectScope>(StringComparer.Ordinal);
        private static bool _frozen;

        static EffectRegistry()
        {
            EffectCatalog.RegisterAll();
        }

        /// <summary>Every registered effect id, for validation and listings.</summary>
        public static IEnumerable<string> AllIds => Table.Keys;

        public static IEnumerable<EffectDef> All => Table.Values;

        public static int Count => Table.Count;

        public static bool IsRegistered(string? id) => id != null && Table.ContainsKey(id);

        public static bool TryGet(string? id, out EffectDef def)
        {
            def = null!;
            return id != null && Table.TryGetValue(id, out def);
        }

        public static EffectDef? Get(string? id) => id != null && Table.TryGetValue(id, out EffectDef def) ? def : null;

        /// <summary>Adds an effect. Only from <see cref="EffectCatalog"/>, before the rules first load.</summary>
        public static void Register(EffectDef def)
        {
            if (_frozen)
            {
                throw new InvalidOperationException($"effect '{def.Id}' registered after the rules loaded; add it to EffectCatalog");
            }
            if (!Core.Ids.IsValid(def.Id) || Table.ContainsKey(def.Id))
            {
                throw new ArgumentException($"effect id '{def.Id}' is invalid or registered twice");
            }
            Table.Add(def.Id, def);
        }

        /// <summary>Called by the rules loader; later registrations throw.</summary>
        internal static void Freeze() => _frozen = true;

        /// <summary>
        /// Adds or replaces an external effect: one another mod applies itself, which ExtraPotions.Inscribe.Adopted rolls, displays and
        /// sums. Allowed at any time, also after <see cref="Freeze"/>: the id must be new or already external (one of
        /// our own ids is refused), the table is only touched on the main thread, and the caller rebuilds the
        /// inscription rules so every family is validated again with it in. Its kind (<see cref="EffectKinds.Of"/>) is
        /// external, a no-op wherever the game is changed. The reason it was refused, or null.
        /// </summary>
        internal static string? RegisterExternal(EffectDef def)
        {
            if (!Core.Ids.IsValid(def.Id))
            {
                return $"'{def.Id}' is not a valid effect id (lowercase letters, digits and _, 2-48 long)";
            }
            if (Table.ContainsKey(def.Id) && !External.ContainsKey(def.Id))
            {
                return $"'{def.Id}' is one of ExtraPotions.Inscribe.Adopted's own effects";
            }
            Table[def.Id] = def;
            External[def.Id] = def.Scope;
            return null;
        }

        /// <summary>Whether the id is an external effect, and its scope.</summary>
        public static bool IsExternal(string? id, out EffectScope scope)
        {
            scope = EffectScope.PlayerGlobal;
            return id != null && External.TryGetValue(id, out scope);
        }
    }
}
