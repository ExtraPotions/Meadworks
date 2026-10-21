// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// One line of resolved numbers per mutation for <c>wildblood inspect</c>: what a creature actually ended up with after
    /// the additive star-power model and any large-star enhancement, not the configured values. Each field is read
    /// through the very same <see cref="Enhance"/> call the gameplay uses - <c>Stat</c> for a <c>1+bonus</c> multiplier,
    /// <c>Magnitude</c> for a raw amount, <c>PowerOf</c> for a cost or non-enhanced field - so the report can never drift
    /// from the behaviour. The gap between what a rule file says and what a creature became is where bugs live.
    /// </summary>
    public static class MutationReport
    {
        public static string Line(BiomeRules r, CreatureTraits t, Mutation m)
        {
            string tag = t.OnLargeStar(m) ? "  [large-star enhanced]" : "";
            return m switch
            {
                Mutation.Mad => $"Mad: move x{Enhance.Stat(r, t, m, Fields.Move):0.00}, attack speed x{Enhance.Stat(r, t, m, Fields.AttackSpeed):0.00}, health x{r.PowerOf(m, Fields.Health):0.00}{tag}",
                Mutation.Bloated => $"Bloated: health x{Enhance.Stat(r, t, m, Fields.Health):0.00}, blast {Enhance.Magnitude(r, t, m, Fields.Damage) * (1 + t.Stars):0} dmg r{Enhance.Magnitude(r, t, m, Fields.Radius):0.0}, delay {r.PowerOf(m, Fields.Delay):0.0}s{tag}",
                Mutation.Cloaked => $"Cloaked: reveal {Enhance.Magnitude(r, t, m, Fields.RevealDistance):0.0}m, fade {r.PowerOf(m, Fields.FadeTime):0.0}s, margin {r.PowerOf(m, Fields.FadeMargin):0.0}m{tag}",
                Mutation.Splintering => $"Splintering: damage x{r.PowerOf(m, Fields.Damage):0.00} (split table keys off stars, never enhanced)",
                Mutation.Leeching => $"Leeching: regen {r.PowerOf(m, Fields.Regen):0.0}%/s, never enhanced, capped {r.PowerOf(m, Fields.RegenCap):0} hp/s, paused {r.PowerOf(m, Fields.CombatCooldown):0}s after a hit; lifesteal {Enhance.Magnitude(r, t, m, Fields.Lifesteal):0.0}%{tag}",
                Mutation.Warding => $"Warding: reflect {Enhance.Magnitude(r, t, m, Fields.Reflect):0.0}%, at most {r.PowerOf(m, Fields.MaxReflect):0.0}% of the attacker's max hp per second, knockback {Enhance.Magnitude(r, t, m, Fields.Knockback):0.0}{tag}",
                Mutation.Plated => $"Plated: {DamageMath.PlatedPercent(r, t):0}% damage cut at full hp (hard cap {r.PowerOf(m, Fields.MaxReduction):0}%), damage +{Enhance.Magnitude(r, t, m, Fields.Damage):0}% at empty{tag}",
                Mutation.Miasmic => $"Miasmic: poison str {Enhance.Magnitude(r, t, m, Fields.CloudDamage):0.0}, {Enhance.Magnitude(r, t, m, Fields.CloudsPerSecond):0.00} clouds/s, life {r.PowerOf(m, Fields.CloudLife):0.0}s, r{r.PowerOf(m, Fields.CloudRadius):0.0}{tag}",
                Mutation.Devouring => $"Devouring: absorb {Enhance.Magnitude(r, t, m, Fields.AbsorbHealth):0}% hp / {Enhance.Magnitude(r, t, m, Fields.AbsorbDamage):0}% dmg, slow {r.PowerOf(m, Fields.SlowPer100Health):0.0}%/100hp, hunts at {r.PowerOf(m, Fields.PlayerThreshold):0.00}x player hp, cooldown {r.PowerOf(m, Fields.DevourCooldown):0}s, eats {DevourLimits.Allowance(r, t)} (one per star, at least min meals {r.PowerOf(m, Fields.MinMeals):0}) {PreySize(r)}{tag}",
                Mutation.Thieving => $"Thieving: holds {PouchStore.ResolvedMaxItems(r, t)} (one per star, at least max items {Enhance.Stat(r, t, m, Fields.MaxItems):0}; hard cap {PouchStore.HardCap}){tag}",
                _ => LineLater(r, t, m, tag),
            };
        }

        /// <summary>The mutations from Gilded on, split out only to keep <see cref="Line"/> short.</summary>
        private static string LineLater(BiomeRules r, CreatureTraits t, Mutation m, string tag)
        {
            return m switch
            {
                Mutation.Gilded => $"Gilded: loot x{Enhance.Stat(r, t, m, Fields.Loot):0.0} + {System.Math.Min(System.Math.Round(Enhance.Magnitude(r, t, m, Fields.BonusAmount) * (1 + t.Stars)), Loot.DropRoller.AmountCap):0} {r.PrefabOf(m, Fields.BonusItem)} (none when tamed), flees players within {r.PowerOf(m, Fields.FleeDistance):0}m{tag}",
                Mutation.Blinking => $"Blinking: every {r.PowerOf(m, Fields.Every):0}s in combat, {r.PowerOf(m, Fields.Distance):0.0}m behind its target, {r.PowerOf(m, Fields.TellTime):0.0}s tell, health x{r.PowerOf(m, Fields.Health):0.00}",
                Mutation.Relentless => $"Relentless: keeps its target to {r.PowerOf(m, Fields.ChaseDistance):0}m, never faster than its base speed",
                Mutation.Juggernaut => "Juggernaut: never staggered (hits, parries, traps) or knocked back; keeps its own attack recoil; no power fields",
                Mutation.Screecher => $"Screecher: shrieks when one hit takes {r.PowerOf(m, Fields.Threshold):0}% of its max hp, at most every {r.PowerOf(m, Fields.Cooldown):0}s; enemy players within {Enhance.Magnitude(r, t, m, Fields.Radius):0}m deafened and unable to cast for {Enhance.Magnitude(r, t, m, Fields.MuteTime):0.0}s; shriek {r.PrefabOf(m, Fields.ShriekSound)}{tag}",
                Mutation.Frostbound => $"Frostbound: {FrostboundAura(r, t)}; {TrailSpec.Describe(r, t, m)}{tag}",
                Mutation.Mudbound => $"Mudbound: {TrailSpec.Describe(r, t, m)}{tag}",
                Mutation.Corrodent => $"Corrodent: a player's armor wears x{Enhance.Stat(r, t, m, Fields.Durability):0.0} as fast under its hits (shields as usual){tag}",
                Mutation.Flamebound => $"Flamebound: {TrailSpec.Describe(r, t, m)}{tag}",
                Mutation.Piercing => $"Piercing: its hits ignore {Enhance.Magnitude(r, t, m, Fields.IgnoreArmour):0}% of a player's armor{tag}",
                Mutation.Howling => $"Howling: the first time it fights a player it howls and calls up to {r.PowerOf(m, Fields.Count):0} creatures within {Enhance.Magnitude(r, t, m, Fields.Radius):0}m to fight them; none when tamed; howl {r.PrefabOf(m, Fields.HowlSound)}{tag}",
                Mutation.Binding => $"Binding: its hits root a player for {Enhance.Magnitude(r, t, m, Fields.RootTime):0.0}s, at most every {r.PowerOf(m, Fields.Cooldown):0}s{tag}",
                Mutation.Cloning => $"Cloning: hides behind a harmless decoy {System.Math.Round(Enhance.Magnitude(r, t, m, Fields.Times)):0} time(s) when it fights a player within {r.PowerOf(m, Fields.Range):0}m, {r.PowerOf(m, Fields.Cooldown):0}s apart; a decoy lasts at most {r.PowerOf(m, Fields.DecoyLife):0}s (0: until a hit or its death){Decoy(t)}{tag}",
                _ => MutationCatalog.Word(m),
            };
        }

        // Cloning: a decoy wears its creature's mutations, so `wildblood inspect` says which one it is looking at.
        private static string Decoy(CreatureTraits t) => t.Decoy ? "; THIS is a decoy: no damage, no drops, no powers" : "";

        // Frostbound's aura and frost heal; the ground trail's numbers follow them on the same line.
        private static string FrostboundAura(BiomeRules r, CreatureTraits t)
        {
            Mutation m = Mutation.Frostbound;
            float cut = System.Math.Min(Enhance.Magnitude(r, t, m, Fields.StaminaRegen), 100f);
            return $"aura {Enhance.Magnitude(r, t, m, Fields.AuraRadius):0.0}m cuts enemy players' stamina regen {cut:0}% ({r.PrefabOf(m, Fields.AuraEffect)}); frost heals it {Enhance.Magnitude(r, t, m, Fields.FrostHeal):0}% of the frost, never hurts or slows it";
        }

        // Devouring's `max prey health`, where 0 lifts the limit.
        private static string PreySize(BiomeRules r)
        {
            float percent = r.PowerOf(Mutation.Devouring, Fields.MaxPreyHealth);
            return percent > 0f ? $"no bigger than {percent:0}% of its health, never large or a boss" : "of any size, never large or a boss";
        }
    }
}
