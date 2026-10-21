// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Keeps <see cref="EcfAggregate"/> on the local player: creates the template once, adds a copy when it is missing
    /// (first spawn, respawn after death, anything that cleared the player's status effects), removes it when effects
    /// are switched off, and pushes the active value set into it. Local player only; never on a dedicated server.
    /// <para>
    /// The game adds a shallow clone of the template; we never keep a reference to that clone across frames but look it
    /// up by name hash when needed (one hash-set check), because death and logout destroy it under us.
    /// </para>
    /// </summary>
    internal static class AggregateHost
    {
        private static EcfAggregate? _template;

        /// <summary>The value set the overrides read: <see cref="AggregateBuilder.Values"/>.</summary>
        public static AggregateValues Current => AggregateBuilder.Values;

        public static bool IsPresent(Player player) => player.GetSEMan().HaveStatusEffect(EcfAggregate.Hash);

        /// <summary>After a rebuild: make sure the effect is on the player and holds the current values.</summary>
        public static void Apply(Player player)
        {
            if (player.IsDead())
            {
                // Death removed every status effect; the respawned Player gets a fresh one.
                return;
            }
            EcfAggregate? effect = Find(player) ?? player.GetSEMan().AddStatusEffect(Template) as EcfAggregate;
            effect?.ApplyFields(Current);
        }

        /// <summary>Effects switched off: take the status effect away (quietly).</summary>
        public static void Remove(Player player) => player.GetSEMan().RemoveStatusEffect(EcfAggregate.Hash, quiet: true);

        private static EcfAggregate? Find(Player player) =>
            player.GetSEMan().GetStatusEffect(EcfAggregate.Hash) as EcfAggregate;

        // Built once per process and kept alive: every clone the game makes shares the template's native object
        // (MemberwiseClone), so it must never be unloaded or destroyed. Never registered in ObjectDB.
        private static EcfAggregate Template
        {
            get
            {
                if (_template == null)
                {
                    _template = ScriptableObject.CreateInstance<EcfAggregate>();
                    _template.name = EcfAggregate.EffectName;
                    _template.hideFlags = HideFlags.HideAndDontSave;
                    _template.m_name = "ExtraPotions.Inscribe.Adopted";
                    _template.m_hidden = true;
                    _template.m_icon = null;
                    _template.m_ttl = 0f;
                    _template.m_startMessage = "";
                    _template.m_stopMessage = "";
                }
                return _template;
            }
        }
    }
}
