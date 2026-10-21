// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Changed by expDARE, 2026: Kall Fimbulbringer summons (FrozenKing, FrozenKing_p3).
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// The `bosses:` section of the default rule file - the boss star table and the boss aspects - exactly as it is
    /// written on first run. Kept apart from <see cref="RuleText"/> only for length; the two are joined into one file
    /// at compile time. This is data, not logic.
    /// </summary>
    internal static class BossRuleText
    {
        public const string Block =
@"# Bosses roll on their own table and take an aspect instead of mutations.
bosses:
  stars: true
  star chances: [90, 6, 3, 1]
  rewards: true
  harder each kill: true
  star power:
    growth:      [0,   0.05, 0.10, 0.15, 0.20, 0.20]
    hp:          [1,   1.5,  2.25, 3.4,  5.1,  7.6]
    attack:      [1,   1.25, 1.55, 1.9,  2.3,  2.75]
    swing speed: [1]
    speed:       [1]
    # No drops line: a boss's stars pay in heads, its other drops are never multiplied by stars.

  aspects:
    enabled: true
    # Real seconds between altar shifts, each rolling the boss's stars and aspect again. 0 fixes each altar.
    shift seconds: 15

    # Weights, not percentages. none is the fight as the game ships it.
    chances:
      none: 42
      Reflective: 10
      Shielded: 10
      Mending: 10
      Summoner: 10
      Elementalist: 10
      Enraged: 10
      Twin: 10
      Phantom: 10
      Adaptive: 10
      Fixated: 10
      Stormbound: 10
      Gravitic: 10
      Colossal: 10
      Tethered: 10
      Bountiful: 10
      Portalbound: 10
      Nightfall: 10
      Brutal: 10
      Echoing: 10

    # Multiplies everything the boss drops, even in Vanilla loot mode.
    loot:
      none: 1
      Twin: 1
      Shielded: 1.1
      Enraged: 1.2
      Elementalist: 1.2
      Mending: 1.3
      Phantom: 1.3
      Reflective: 1.4
      Summoner: 1.5
      Stormbound: 1.2
      Colossal: 1.2
      Adaptive: 1.3
      Fixated: 1.3
      Gravitic: 1.3
      Tethered: 1
      Bountiful: 2
      Portalbound: 1.2
      Nightfall: 1.3
      Brutal: 1.2
      Echoing: 1.3

    # Field meanings: reference, Boss aspect power fields.
    power:
      Reflective:   { reflect: 15 }
      Shielded:     { arrow reduction: 30 }
      Mending:      { regen: 0.2 }
      Summoner:     { every: 33, count: 2, stars: 2 }
      Elementalist: { elemental bonus: 20 }
      Enraged:      { physical bonus: 20 }
      Twin:         { less health: 25, less damage: 25 }
      Phantom:      { split at: [66, 33], per player: 1, health per tier: 25, less damage: 50 }
      Adaptive:     { resist: 50, window: 15 }
      Fixated:      { marked bonus: 50, others less: 30, every: 30 }
      Stormbound:   { every: 20, tell time: 2, radius: 2.5, damage: 8, range: 40 }
      Gravitic:     { every: 20, range: 30, pull time: 1.5, pull speed: 6, slam radius: 6, slam damage: 10 }
      Colossal:     { bigger: 40, more health: 15, slower: 15, shockwave radius: 8 }
      Tethered:     { less health: 25, less damage: 25, attack speed: 50, armour: 50, full gap: 50 }
      Bountiful:    { extra aspects: 2 }
      Portalbound:  { min height: 5, clearance: 2, range: 20 }
      Nightfall:    { range: 60, every: 18, every max: 28, life: 10, form time: 1.5, tornado speed: 40, damage: 25, top width: 9, base width: 1.5, height: 14, toss distance: 10 }
      Brutal:       { launch: 20, lift: 3 }
      Echoing:      { delay: 15 }

    # Per boss, by prefab name: summons is what Summoner calls (no list, no Summoner);
    # aspects, if set, limits which aspects it rolls.
    per boss:
      - match: Eikthyr
        summons: [Boar, Neck]
      - match: gd_king
        summons: [Greydwarf_Elite, Greydwarf_Shaman]
      - match: Bonemass
        summons: [Draugr_Elite, BlobElite]
      - match: Dragon
        summons: [Hatchling]
      - match: GoblinKing
        summons: [GoblinBrute, GoblinShaman]
      - match: SeekerQueen
        summons: [SeekerBrute, Seeker]
      - match: Fader
        summons: [Charred_Melee, Charred_Archer]
      - match: FrozenKing
        summons: [JotunWarrior, JotunWitch]
      - match: FrozenKing_p3
        summons: [JotunWarrior, JotunWitch]
      # Elite Creatures Pack's kraken holds one ship: never Twin, Phantom, Tethered, Brutal or Echoing.
      - match: ECP_Kraken
        aspects: [none, Reflective, Shielded, Mending, Summoner, Elementalist, Enraged, Adaptive, Fixated, Stormbound, Gravitic, Colossal, Bountiful, Nightfall]

";
    }
}
