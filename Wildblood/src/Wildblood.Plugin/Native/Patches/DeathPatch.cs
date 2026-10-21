// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; receipted pouch drops.
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Raids;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// The death-triggered mutations, all on the dying creature's owner: Bloated leaves a fuse-and-blast behind,
    /// Splintering breaks into copies (tamed ones when the parent was tamed), and a Devouring killer keeps its victim's
    /// health and damage. Boss aspects too: a Twin's partner falls with it, and a Phantom boss's copies vanish with it.
    /// <para>
    /// The fault this fixes: vanilla <c>Character.OnDeath</c> ends with <c>ZNetScene.Destroy</c>, which calls
    /// <c>ResetZDO</c> and nulls the creature's ZDO. A postfix therefore sees <c>IsValid()</c>/<c>IsOwner()</c> false and
    /// <c>GetZDO()</c> null, so everything read from the ZDO (the split's prefab/generation/root, the devour marker) was
    /// silently gone - which is why Splintering never split. So a PREFIX snapshots everything the death behaviours need
    /// while the ZDO is still alive; the POSTFIX (after the ragdoll exists, so Bloated's warning can ride it) acts on the
    /// snapshot. The prefix is exception-safe and never rethrows: a throw there would abort the creature's own death.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class DeathPatch
    {
        private sealed class Snapshot
        {
            public Character Victim = null!;
            public CreatureTraits Traits = null!;
            public BiomeRules Rules = null!;
            public int PrefabHash;
            public int Generation;
            public string ResolvedRoot = "";
            public Heightmap.Biome Biome;
            public float MaxHealth;
            public Vector3 Pos;
            public Quaternion Rot;
            public ZDOID Devourer;
            public List<PouchStore.Entry> Pouch = new List<PouchStore.Entry>();
            public ZDOID Id;
            public SplinterTame? Tame;
            public RaiderLineage? Raid;
            public bool Wild;
            public BossProgress.Death? Progress;
            public BossMaterialRewards.Context? Rewards;
        }

        // Runs before vanilla resets the ZDO. Must not rethrow, or the creature would fail to die - so it reports and
        // swallows via Guard.Report (never Guard.Run), the pattern for a prefix that must never break the method it guards.
        private static void Prefix(Character __instance, out Snapshot? __state)
        {
            try
            {
                __state = Capture(__instance);
            }
            catch (Exception e)
            {
                __state = null;
                Guard.Report(e, "Character.OnDeath capture");
            }
        }

        private static Snapshot? Capture(Character victim)
        {
            ZNetView nview = victim.GetComponent<ZNetView>();
            bool owner = nview != null && nview.IsValid() && nview.IsOwner();
            EliteController controller = victim.GetComponent<EliteController>();
            if (Log.Diagnostics)
            {
                Log.Diag($"OnDeath {victim.name}: owner={owner} ready={(controller != null && controller.Ready)}");
            }
            if (!owner || controller == null || !controller.Ready)
            {
                return null; // all death work is the owner's, and only on a resolved creature
            }
            var snapshot = Build(victim, controller, nview!.GetZDO());
            try { AspectDeath.Capture(victim, controller, nview!); }
            catch { BossMaterialRewards.End(snapshot.Rewards); throw; }
            return snapshot;
        }

        private static Snapshot Build(Character victim, EliteController controller, ZDO zdo)
        {
            string stored = zdo.GetString(TraitKeys.CascadeRoot);
            // A gen-0 parent has no stored root, so its cascade is rooted at its own id - resolved here while the ZDOID
            // is still readable, because after the ZDO reset the postfix cannot ask the creature for its id any more.
            string resolved = string.IsNullOrEmpty(stored) ? victim.GetZDOID().ToString() : stored;
            return new Snapshot
            {
                Victim = victim, Traits = controller.Traits, Rules = controller.Rules,
                PrefabHash = zdo.GetPrefab(), Generation = zdo.GetInt(TraitKeys.Generation),
                ResolvedRoot = resolved, Biome = TraitStore.GetBiome(zdo),
                MaxHealth = victim.GetMaxHealth(), Pos = victim.transform.position, Rot = victim.transform.rotation,
                Devourer = TraitStore.GetDevouredBy(zdo), Pouch = PouchStore.Load(zdo), Id = zdo.m_uid,
                Wild = ExtraPotions.Wildblood.Adopted.Horde.WildTag.Is(zdo),
                Raid = RaiderLineage.Read(zdo),
                Tame = SplinterTame.Read(victim, zdo), // a tamed splinterer's copies are tamed too
                Progress = BossProgress.Prepare(victim, controller, zdo, true),
                Rewards = BossMaterialRewards.Begin(victim, controller),
            };
        }

        private static void Postfix(Character __instance, Snapshot? __state) =>
            Guard.Run("Character.OnDeath", () => Handle(__instance, __state));

        private static Exception? Finalizer(Exception? __exception, Snapshot? __state)
        {
            try { if(__exception==null) BossProgress.Complete(__state?.Progress); }
            catch(Exception error) { Guard.Report(error,"Character.OnDeath boss progression"); }
            finally { BossMaterialRewards.End(__state?.Rewards); }
            return __exception;
        }

        private static void Handle(Character victim, Snapshot? snap)
        {
            if (snap == null || snap.Victim != victim)
            {
                return; // no valid snapshot for this creature (not owner, not resolved, or capture failed)
            }
            RunTraitDeaths(snap);
            if (snap.Traits.HasAspect(Aspect.Phantom) && !snap.Traits.PhantomCopy)
            {
                PhantomReaper.Release(snap.Id); // the boss is gone, so are its phantoms
            }
            if (snap.Traits.HasAspect(Aspect.Echoing))
            {
                EchoReaper.Release(snap.Id); // and its echo
            }
            Feed(snap);
            DescendantRegistry.Died(snap.Victim); // a copy this machine counted is counted out at its death, as before
        }

        private static void RunTraitDeaths(Snapshot snap)
        {
            if (snap.Traits.Has(Mutation.Bloated))
            {
                Bloat(snap);
            }
            if (snap.Traits.Has(Mutation.Splintering))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"{snap.Victim.name}: splintering stars={snap.Traits.Stars} gen={snap.Generation}");
                }
                bool previous = ExtraPotions.Wildblood.Adopted.Horde.WildSpawn.Begin(snap.Wild);
                try { Splitter.Split(snap.Pos, snap.Rot, snap.PrefabHash, snap.Traits, snap.Rules,
                    snap.Generation, snap.ResolvedRoot, snap.Biome, snap.Tame, snap.Raid); }
                finally { ExtraPotions.Wildblood.Adopted.Horde.WildSpawn.End(previous); }
            }
            if (snap.Traits.Has(Mutation.Thieving))
            {
                // Splintering copies never inherit the pouch (Splitter.Configure never touches TraitKeys.Pouch), so a
                // Thieving+Splintering death both empties this parent's pouch here AND is born-empty as two copies.
                PouchDrop.DropAll(snap.Pouch, snap.Pos, snap.Id, null, lastChance: true); // the ZDO is already reset
            }
        }

        // Broadcasts the Bloated death so every client wears a warning that rides its own copy of this creature's corpse -
        // named by id, the ragdoll this very death made (see DeathRagdoll) - for the whole fuse; the owner, when its fuse
        // ends, broadcasts the blast at that corpse's resting place (see EliteRpc).
        private static void Bloat(Snapshot snap)
        {
            BiomeRules rules = snap.Rules;
            CreatureTraits traits = snap.Traits;
            if (Log.Diagnostics)
            {
                Log.Diag($"{snap.Victim.name}: bloated death, fuse={rules.PowerOf(Mutation.Bloated, Fields.Delay)}s");
            }
            BlastSpec blast = new BlastSpec(
                Enhance.Magnitude(rules, traits, Mutation.Bloated, Fields.Damage),
                Enhance.Magnitude(rules, traits, Mutation.Bloated, Fields.Radius), traits.Stars,
                rules.PrefabOf(Mutation.Bloated, Fields.BlastEffect),
                rules.PrefabOf(Mutation.Bloated, Fields.BlastSound));
            EliteRpc.FireBloat(snap.Pos, DeathRagdoll.MadeAt(snap.Pos), rules.PowerOf(Mutation.Bloated, Fields.Delay),
                rules.PrefabOf(Mutation.Bloated, Fields.WarningEffect), blast);
        }

        // Runs on the VICTIM's owner. The devourer is read from the prey's own ZDO marker (set at the bite, survives a
        // hand-over), so a prey devoured across owners still feeds the right devourer; the grant is routed to its owner.
        private static void Feed(Snapshot snap)
        {
            if (snap.Devourer == ZDOID.None)
            {
                return; // this creature was not devoured (a plain death, or a kill during the devourer's cooldown)
            }
            GameObject? go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(snap.Devourer) : null;
            Character? devourer = go != null ? go.GetComponent<Character>() : null;
            EliteController? kc = devourer != null ? devourer.GetComponent<EliteController>() : null;
            if (devourer == null || kc == null || !kc.Ready || !kc.Traits.Has(Mutation.Devouring))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"{snap.Victim.name}: devourer not resolvable here; cross-owner feed skipped");
                }
                return;
            }
            Grant(snap, devourer, kc);
        }

        // The instant kill always lands the killing blow, so the devourer keeps the whole meal: a share of the prey's max
        // health and of its damage, both permanent. The cooldown is started where this is banked (CreatureRpc.Bank).
        private static void Grant(Snapshot snap, Character devourer, EliteController kc)
        {
            float health = snap.MaxHealth
                * Enhance.Magnitude(kc.Rules, kc.Traits, Mutation.Devouring, Fields.AbsorbHealth) / 100f;
            float damage = EstimateDamage(snap.Victim, snap.MaxHealth)
                * Enhance.Magnitude(kc.Rules, kc.Traits, Mutation.Devouring, Fields.AbsorbDamage) / 100f;
            if (Log.Diagnostics)
            {
                Log.Diag($"{devourer.name} devoured {snap.Victim.name}: +hp={health:0.#} +dmg={damage:0.#}");
            }
            CreatureRpc.Absorb(devourer, health, damage, snap.PrefabHash); // the prefab is what its nameplate shows it ate
        }

        private static float EstimateDamage(Character victim, float maxHealth)
        {
            Humanoid? humanoid = victim as Humanoid;
            ItemDrop.ItemData? weapon = humanoid != null ? humanoid.GetCurrentWeapon() : null;
            float damage = weapon != null ? weapon.GetDamage().GetTotalDamage() : 0f;
            return damage > 0f ? damage : maxHealth * 0.05f;
        }
    }
}
