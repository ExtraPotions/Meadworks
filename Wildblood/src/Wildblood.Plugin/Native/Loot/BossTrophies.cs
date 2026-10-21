// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Loot
{
    /// <summary>
    /// A boss's own trophies, the one drop no loot rule touches: a boss with N stars drops one of each trophy in its
    /// own drop table for every player within 100 m of it, plus N+1 - two heads for a plain fight alone, six for a
    /// two-star one with three players - in every loot mode, Vanilla included (the user, 2026-10-04). A head per
    /// player and per star is a count the group can read before the fight, not a quantity to roll or multiply, so it
    /// is the whole rule: no `drops` line, extra roll, global or boss multiplier, other aspect's factor or trophy
    /// switch changes it, and like an aspect's pay it stays when the loot rules are off. Bountiful alone scales it,
    /// since it pays more of everything: the count times its own `loot` (x2 by default, so that two-star group's
    /// Bountiful boss drops twelve heads), rounded. The table decides, not the game's roll: a trophy row in the
    /// boss's table pays even if its
    /// chance (1 on every vanilla boss) came up empty. The engine holds these rows out of the list before any rule
    /// runs and pays them back last, so nothing in between can scale or reroll them; a row the rule file adds for
    /// the same item still adds on top. The exception is a trophy the boss's `creatures:` entry names under `drop
    /// overrides`: the server's own words, left to the engine as before. Every boss that dies in its own right pays
    /// its own - each Twin - while a Phantom copy, which is no boss kill and drops nothing, never does.
    /// </summary>
    internal sealed class BossTrophies
    {
        /// <summary>Nothing held: not a boss, or no trophy in its table left to this rule.</summary>
        public static readonly BossTrophies None = new BossTrophies(new List<GameObject>(), 0);

        /// <summary>Metres from the boss within which a player earns a head of their own. The count is taken on the
        /// boss's owner, who is in the fight, so every player that close is loaded there.</summary>
        internal const float PlayerRange = 100f;

        private readonly List<GameObject> _heads;
        private readonly int _count;

        private BossTrophies(List<GameObject> heads, int count)
        {
            _heads = heads;
            _count = count;
        }

        /// <summary>
        /// Takes a boss's own trophies out of the drop list so the loot rules run without them, and remembers what
        /// to pay back. <paramref name="overridden"/> names the rows the boss's `creatures:` entry overrides (null
        /// when the rules are off); a trophy among them stays in the list for the engine.
        /// </summary>
        public static BossTrophies Hold(CharacterDrop drop, EliteController controller,
            List<KeyValuePair<GameObject, int>> result, HashSet<string>? overridden)
        {
            if (!Pays(controller))
            {
                return None;
            }
            List<GameObject> heads = TableTrophies(drop, overridden);
            if (heads.Count == 0)
            {
                return None;
            }
            result.RemoveAll(pair => heads.Contains(pair.Key));
            return new BossTrophies(heads, Count(controller));
        }

        /// <summary>A head per player near the boss plus N+1, times Bountiful's own loot multiplier when it carries it.</summary>
        private static int Count(EliteController controller)
        {
            CreatureTraits traits = controller.Traits;
            float heads = Player.GetPlayersInRangeXZ(controller.transform.position, PlayerRange) + traits.Stars + 1;
            if (traits.HasAspect(Aspect.Bountiful))
            {
                heads *= RuleState.Active.Boss.Aspects.LootOf(Aspect.Bountiful);
            }
            return Mathf.Clamp(Mathf.RoundToInt(heads), 1, DropRoller.AmountCap);
        }

        /// <summary>True for a trophy held here, which the engine's rerolls pass by.</summary>
        public bool Holds(GameObject prefab) => _heads.Contains(prefab);

        /// <summary>Last of all, after every rule and multiplier: the count of each held trophy, scaled by nothing.</summary>
        public void Pay(List<KeyValuePair<GameObject, int>> result)
        {
            foreach (GameObject head in _heads)
            {
                DropRoller.Add(result, head, _count);
            }
        }

        /// <summary>A boss that died in its own right; never a Phantom copy, whose drops are off anyway.</summary>
        private static bool Pays(EliteController controller) =>
            controller.Creature != null && controller.Creature.IsBoss() && !controller.Traits.PhantomCopy;

        /// <summary>Each distinct trophy in the boss's own table that no `drop overrides` row names.</summary>
        private static List<GameObject> TableTrophies(CharacterDrop drop, HashSet<string>? overridden)
        {
            List<GameObject> heads = new List<GameObject>();
            foreach (CharacterDrop.Drop row in drop.m_drops)
            {
                if (row.m_prefab != null && DropRoller.IsTrophy(row.m_prefab) && !heads.Contains(row.m_prefab)
                    && (overridden == null || !overridden.Contains(row.m_prefab.name)))
                {
                    heads.Add(row.m_prefab);
                }
            }
            return heads;
        }
    }
}
