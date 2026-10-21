// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Breeding/BreedingCheck.cs, BreedingPace.cs,
// BirthRolls.cs, TwinPregnancy.cs and GrowingUp.cs, and EliteCreaturesReborn/EliteCreaturesReborn/Patches/BreedingPatch.cs
// and Breeding/Parents.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: one Procreate prefix/postfix/finalizer for pace, birth plan and twins; decisions in the
// portable Shepherd.Breeding core (BreedingPace, BirthPlanner, ClaimSlot, Carry, SireCapture, TwinRules); Shepherd
// pen-scoped count lifetime, temporary capacity and moving-ship pause; lineage keys and egg record with optional Wildblood trait transport; twins due after Twin Delay; external inheritance yields its level/trait/egg writers while lineage continues; Mutation Chance passed to the trait provider.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    /// <summary>
    /// Breeding on the owner (design 7, 10):
    /// <list type="bullet">
    /// <item>Conception (<c>Procreation.MakePregnant</c> postfix): the nearest ready tame of the partner prefab within the
    /// partner range becomes the pending sire.</item>
    /// <item>Procreate prefix: pace edits (breeding speed, contentment, herd room) and, when a birth is due, the birth plan
    /// (inheritance, lineage, egg record) armed in the claim slot for the offspring prefab. The newborn (Character or egg
    /// ItemDrop) claims it while the game instantiates it; the postfix writes level, lineage or egg data on it, clears
    /// the pending sire and conceives a twin that is due Twin Delay seconds later. The finalizer restores every field
    /// and clears the slot.</item>
    /// <item>Growup and EggGrow prefixes: growth speed for keepers and the record carried to the adult or hatchling.</item>
    /// </list>
    /// </summary>
    public static class BreedingHooks
    {
        private static readonly string InstanceKey = BreedingKeys.Instance;
        private static readonly ClaimSlot<BirthPlan> Births = new ClaimSlot<BirthPlan>();
        private static readonly ClaimSlot<OffspringCarry> Carries = new ClaimSlot<OffspringCarry>();
        private static readonly IBreedingRandom Dice = new UnityDice();
        private static GameObject? claimed;
        private static OffspringCarry? claimedCarry;
        private static string carryPrimary = "";
        private static bool carryInheritance;
        private static readonly HashSet<string> carryAlternates = new HashSet<string>(StringComparer.Ordinal);

        public static void Clear()
        {
            Births.Clear();
            Carries.Clear();
            claimed = null;
            claimedCarry = null;
            carryAlternates.Clear();
        }

        private sealed class UnityDice : IBreedingRandom
        {
            public double Value() => UnityEngine.Random.value;
            public int Range(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
        }

        private sealed class Call
        {
            public PacedCall? Pace;
            public IDisposable? PenScope;
            public int OriginalMax;
            public bool Birth;
            public bool WasTwin;
            public bool Twins;
            public BirthPlan? Plan;
            public bool OwnInheritance;
            public bool ExternalStar;
        }

        private static bool Owned(ZNetView view) => view != null && view.IsValid() && view.IsOwner();

        private static bool Breeds(Procreation parent) => NativeBreeding.Active && Tended(parent);

        /// <summary>An owned tamed parent while the Procreate patches are installed (Breeding, Herd or the cheat).</summary>
        private static bool Tended(Procreation parent) =>
            NativeBreeding.Installed && Owned(parent.m_nview) && parent.m_tameable != null && parent.m_tameable.IsTamed();

        [HarmonyPatch(typeof(Procreation), nameof(Procreation.Procreate))]
        private static class Procreate
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Procreation __instance, out Call? __state)
            {
                Procreation parent = __instance;
                __state = null;
                if (!Tended(parent)) return true;
                if (NativeBreeding.Run("moving pen", static p => LivestockPens.HoldsMoving(p), parent)) return false;
                // Strict Herd Limit (S08): a due birth waits for room; nothing is armed and the call is skipped.
                if (NativeBreeding.Run("herd limit", static p => HerdHooks.Holds(p), parent)) return false;
                __state = NativeBreeding.Run("breeding call", static p => Begin(p), parent, (Call?)null);
                return true;
            }

            [HarmonyPostfix]
            private static void Postfix(Procreation __instance, Call? __state)
            {
                if (__state != null)
                    NativeBreeding.Run("birth", static birth => After(birth.parent, birth.call), (parent: __instance, call: __state));
            }

            [HarmonyFinalizer]
            private static void Finalizer(Procreation __instance, Call? __state)
            {
                try
                {
                    if (__state?.Pace != null && __state.Pace.Paced)
                    {
                        PaceFields original = __state.Pace.Original;
                        __instance.m_pregnancyDuration = (float)original.PregnancyDuration;
                        __instance.m_pregnancyChance = (float)original.PregnancyChance;
                        __instance.m_maxCreatures = original.MaxCreatures;
                    }
                    if (__state != null) { __instance.m_maxCreatures=__state.OriginalMax; __state.PenScope?.Dispose(); }
                }
                finally
                {
                    try { if (__state != null) EcrOffspringStar.End(__instance, __state.ExternalStar); }
                    finally { Births.Clear(); claimed = null; }
                }
            }
        }

        private static Call Begin(Procreation parent)
        {
            var duration=parent.m_pregnancyDuration;var chance=parent.m_pregnancyChance;var maximum=parent.m_maxCreatures;
            var scope=LivestockPens.EnterCount(parent);
            try { var call=BeginCore(parent);call.PenScope=scope;call.OriginalMax=maximum;return call; }
            catch { parent.m_pregnancyDuration=duration;parent.m_pregnancyChance=chance;parent.m_maxCreatures=maximum;scope.Dispose();throw; }
        }
        private static Call BeginCore(Procreation parent)
        {
            var call = new Call();
            float level = NativeHusbandry.Active ? Keeper.BestLevel(parent.transform.position) : 0f;
            double factor = NativeBreeding.PaceActive ? BreedingPace.SpeedFactor(HusbandryBreedingSettings.BreedingSpeed.Value, level,
                NativeHusbandry.IsContent(parent.m_nview), HusbandryBreedingSettings.ContentBonus.Value) : 1;
            // Herd module bonus or the Unlimited Herd cheat (S08), restored with the pace fields by the finalizer.
            int room = HerdHooks.ExtraMax(parent, level);
            int maximum = HerdHooks.Maximum(parent, level);
            call.Pace = BreedingPace.Apply(new PaceFields(parent.m_pregnancyDuration, parent.m_pregnancyChance, parent.m_maxCreatures), factor, room);
            if (call.Pace.Paced)
            {
                parent.m_pregnancyDuration = (float)call.Pace.Fields.PregnancyDuration;
                parent.m_pregnancyChance = (float)call.Pace.Fields.PregnancyChance;
                parent.m_maxCreatures = call.Pace.Fields.MaxCreatures;
            }
            parent.m_maxCreatures=maximum;
            if (!NativeBreeding.Active || !parent.IsPregnant() || !parent.IsDue() || parent.m_offspring == null)
                return call;
            ZDO zdo = parent.m_nview.GetZDO();
            var store = new ZdoKeyStore(zdo);
            LegacyBreeding.Sire(parent);
            call.Birth = true;
            call.OwnInheritance = NativeBreeding.InheritanceActive;
            call.WasTwin = BreedingStore.IsTwin(store);
            call.Twins = NativeHusbandry.Active && TwinRules.RollTwin(call.WasTwin, HusbandryBreedingSettings.Twins.Value, level, Dice);
            Character mother = parent.m_character;
            var input = new BirthInput(InstanceToken.New().Value, MotherRecord(parent), NameOf(mother), mother != null ? mother.GetLevel() : 1,
                BreedingStore.ReadPendingSire(store), parent.m_minOffspringLevel, level, EnvMan.instance != null ? EnvMan.instance.GetDay() : 0,
                BreederName(parent.transform.position), ShepherdSettings.RecordLineage.Value, "");
            BirthPlan plan = BirthPlanner.Plan(input, call.OwnInheritance ? NativeBreeding.Inheritance()
                : new InheritanceSettings(InheritanceMode.Vanilla, 0, 10, 0, 0), Dice);
            var traits = WildbloodTraitsCapability.Current;
            call.Plan = call.OwnInheritance ? traits.Inherit(plan, traits.Read(mother, BreedingStore.GetTraits(store)),
                input.Sire?.Traits ?? "", input.Token.GetStableHashCode(), ShepherdSettings.MutationChance.Value) : plan;
            if (!call.OwnInheritance && NativeHusbandry.Active && HusbandryShare.Rolls(HusbandryBreedingSettings.BetterOffspring.Value, level, Dice))
                call.ExternalStar = EcrOffspringStar.Begin(parent);
            try { Births.Arm(call.Plan, parent.m_offspring.name); }
            catch { EcrOffspringStar.End(parent, call.ExternalStar); throw; }
            return call;
        }

        private static void After(Procreation parent, Call call)
        {
            if (!call.Birth || call.Plan == null || !Owned(parent.m_nview) || parent.IsPregnant()) return;
            ZDO zdo = parent.m_nview.GetZDO();
            var store = new ZdoKeyStore(zdo);
            zdo.Set(BreedingKeys.Births, zdo.GetInt(BreedingKeys.Births, 0) + 1);
            GameObject? born = claimed;
            claimed = null;
            if (born != null) Stamp(born, call.Plan, call.WasTwin, call.OwnInheritance && NativeBreeding.InheritanceActive);
            if (call.WasTwin)
            {
                BreedingStore.SetIsTwin(store, false);
                BreedingStore.WritePendingSire(store, null);
            }
            else if (!call.Twins)
            {
                BreedingStore.WritePendingSire(store, null);
            }
            if (call.Twins) ConceiveTwin(parent, store);
            if (born != null) HerdCallout.Send(parent, call.OwnInheritance ? call.Plan.Inheritance.StrongOffspring : EcrOffspringStar.Added(parent, call.ExternalStar), call.Twins);
            NativeBreeding.Verbose("birth: " + global::Utils.GetPrefabName(parent.gameObject) + " level " + call.Plan.Level
                + (call.Twins ? ", twin due" : ""));
        }

        /// <summary>Writes the plan on the claimed newborn: level (after the game's own SetLevel/SetQuality) and lineage or egg record.</summary>
        private static void Stamp(GameObject born, BirthPlan plan, bool twin, bool inheritance)
        {
            Character character = born.GetComponent<Character>();
            if (character != null)
            {
                ZNetView view = character.m_nview;
                if (!Owned(view)) return;
                if (inheritance) character.SetLevel(plan.Level);
                var store = new ZdoKeyStore(view.GetZDO());
                if (plan.Lineage != null)
                {
                    store.Set(InstanceKey, plan.Lineage.Token);
                    BreedingStore.WriteLineage(store, plan.Lineage);
                }
                if (inheritance)
                {
                    BreedingStore.SetTraits(store, plan.Egg.Traits);
                    WildbloodTraitsCapability.Current.Apply(character, plan.Egg.Traits);
                }
                if (twin) BreedingStore.SetIsTwin(store, true);
                return;
            }
            ItemDrop egg = born.GetComponent<ItemDrop>();
            if (egg == null || !Owned(egg.m_nview)) return;
            if (inheritance) egg.SetQuality(plan.EggQuality);
            var traits = WildbloodTraitsCapability.Current;
            if (plan.Lineage != null || inheritance && (traits.Available || plan.Egg.Traits.Length != 0))
            {
                egg.m_itemData.m_customData[BreedingKeys.Egg] = inheritance ? traits.EncodeEgg(plan.Egg) : EggCodec.Encode(plan.Egg);
                egg.Save();
            }
        }

        /// <summary>
        /// GrindstoneSkills TwinPregnancy: the parent is made pregnant again at once, with a start time that makes it due
        /// Twin Delay seconds later; the pending sire is kept, and the twin's own birth never rolls twins.
        /// </summary>
        private static void ConceiveTwin(Procreation parent, IKeyStore store)
        {
            SireRecord? sire = BreedingStore.ReadPendingSire(store);
            parent.MakePregnant();
            double offset = BreedingPace.TwinStartOffset(parent.m_pregnancyDuration, ShepherdSettings.TwinDelay.Value);
            parent.m_nview.GetZDO().Set(ZDOVars.s_pregnant, Herd.TicksIn(offset));
            BreedingStore.WritePendingSire(store, sire);
            BreedingStore.SetIsTwin(store, true);
        }

        [HarmonyPatch(typeof(Procreation), nameof(Procreation.MakePregnant))]
        private static class Conception
        {
            [HarmonyPostfix]
            private static void Postfix(Procreation __instance)
            {
                if (Breeds(__instance)) NativeBreeding.Run("sire capture", static p => CaptureSire(p), __instance);
            }
        }

        /// <summary>EliteCreaturesReborn Parents.FindPartner, decided by the portable <see cref="SireCapture"/>.</summary>
        internal static void CaptureSire(Procreation parent)
        {
            if (!Owned(parent.m_nview)) return;
            var parentZdo = parent.m_nview.GetZDO();
            GameObject? partnerPrefab = parent.m_seperatePartner != null ? parent.m_seperatePartner
                : parent.m_myPrefab != null ? parent.m_myPrefab : ZNetScene.instance?.GetPrefab(parentZdo.GetPrefab());
            if (partnerPrefab == null) return;
            var store = new ZdoKeyStore(parent.m_nview.GetZDO());
            if (BreedingStore.IsTwin(store)) return;
            Vector3 at = parent.transform.position;
            var candidates = new List<PartnerCandidate>();
            var traits = NativeBreeding.InheritanceActive ? WildbloodTraitsCapability.Current : new TraitsConsumer(null);
            foreach (Character candidate in Character.GetAllCharacters())
            {
                if (candidate == null || candidate.m_nview == null || !candidate.m_nview.IsValid()) continue;
                float distance = Vector3.Distance(at, candidate.transform.position);
                if (distance > parent.m_partnerCheckRange) continue;
                Procreation procreation = candidate.GetComponent<Procreation>();
                ZDO zdo = candidate.m_nview.GetZDO();
                candidates.Add(new PartnerCandidate(Token(candidate.m_nview), NameOf(candidate), global::Utils.GetPrefabName(candidate.gameObject),
                    candidate.GetLevel(), zdo.GetInt(BreedingKeys.Generation, 0), traits.Read(candidate, zdo.GetString(BreedingKeys.Traits, "")), distance,
                    candidate.gameObject == parent.gameObject, candidate.IsTamed(), procreation == null || procreation.ReadyForProcreation()));
            }
            if (Owned(parent.m_nview) && ReferenceEquals(parentZdo, parent.m_nview.GetZDO()))
                BreedingStore.WritePendingSire(store, SireCapture.Choose(partnerPrefab.name, parent.m_partnerCheckRange, candidates));
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Awake))]
        private static class CharacterAwake
        {
            [HarmonyPostfix]
            private static void Postfix(Character __instance)
            {
                if (Births.Armed || Carries.Armed) NativeBreeding.Run("newborn claim", Notice, __instance.gameObject);
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        private static class ItemAwake
        {
            [HarmonyPostfix]
            private static void Postfix(ItemDrop __instance)
            {
                if (Births.Armed) NativeBreeding.Run("egg claim", Notice, __instance.gameObject);
            }
        }

        private static void Notice(GameObject woke)
        {
            string name = global::Utils.GetPrefabName(woke);
            if (Births.Claim(name) != null)
            {
                claimed = woke;
                return;
            }
            if (Carries.Armed && carryAlternates.Contains(name)) name = carryPrimary;
            OffspringCarry? carry = Carries.Claim(name);
            if (carry != null)
            {
                claimed = woke;
                claimedCarry = carry;
            }
        }

        [HarmonyPatch(typeof(Growup), nameof(Growup.GrowUpdate))]
        private static class Young
        {
            [HarmonyPrefix]
            private static void Prefix(Growup __instance, out float __state)
            {
                __state = -1f;
                if (!NativeBreeding.GrowthActive || !Owned(__instance.m_nview)) return;
                __state = NativeBreeding.Run("growing up", static young => BeginGrow(young), __instance, __instance.m_growTime);
            }

            [HarmonyPostfix]
            private static void Postfix() => NativeBreeding.Run("grown", FinishCarry);

            [HarmonyFinalizer]
            private static void Finalizer(Growup __instance, float __state)
            {
                if (__state >= 0f) __instance.m_growTime = __state;
                EndCarry();
            }
        }

        private static float BeginGrow(Growup young)
        {
            float original = -1f;
            Character character = young.GetComponent<Character>();
            if (ShepherdSettings.InstantGrowth.Value)
            {
                if (young.m_growTime >= 0f) { original = young.m_growTime; young.m_growTime = -1f; }
            }
            else if (NativeBreeding.PaceActive && character != null && character.IsTamed())
            {
                double factor = BreedingPace.GrowthFactor(HusbandryBreedingSettings.GrowthSpeed.Value, Keeper.BestLevel(young.transform.position));
                if (factor > 1)
                {
                    original = young.m_growTime;
                    young.m_growTime = (float)BreedingPace.ScaledTime(young.m_growTime, factor);
                }
            }
            if (young.m_baseAI == null || young.m_grownPrefab == null
                || !Carry.GrowsNow(young.m_baseAI.GetTimeSinceSpawned().TotalSeconds, young.m_growTime))
                return original;
            var store = new ZdoKeyStore(young.m_nview.GetZDO());
            LineageRecord? record = BreedingStore.ReadLineage(store);
            var traits = NativeBreeding.InheritanceActive ? WildbloodTraitsCapability.Current : new TraitsConsumer(null);
            string carriedTraits = traits.Read(character, BreedingStore.GetTraits(store));
            if (record == null && !traits.Available && carriedTraits.Length == 0) return original;
            ArmCarry(new OffspringCarry(record == null ? null : Carry.GrowUp(record), carriedTraits), young.m_grownPrefab.name);
            if (young.m_altGrownPrefabs != null)
                foreach (var alternative in young.m_altGrownPrefabs)
                    if (alternative != null && alternative.m_prefab != null) carryAlternates.Add(alternative.m_prefab.name);
            return original;
        }

        [HarmonyPatch(typeof(EggGrow), nameof(EggGrow.GrowUpdate))]
        private static class Egg
        {
            [HarmonyPrefix]
            private static void Prefix(EggGrow __instance, out float __state)
            {
                __state = -1f;
                if (!NativeBreeding.GrowthActive || !Owned(__instance.m_nview)) return;
                __state = NativeBreeding.Run("hatching", static egg => BeginHatch(egg), __instance, __instance.m_growTime);
            }

            [HarmonyPostfix]
            private static void Postfix() => NativeBreeding.Run("hatched", FinishCarry);

            [HarmonyFinalizer]
            private static void Finalizer(EggGrow __instance, float __state)
            {
                if (__state >= 0f) __instance.m_growTime = __state;
                EndCarry();
            }
        }

        private static float BeginHatch(EggGrow egg)
        {
            float original = -1f;
            LegacyBreeding.Egg(egg);
            if (ShepherdSettings.InstantGrowth.Value)
            {
                if (egg.m_growTime >= 0f) { original = egg.m_growTime; egg.m_growTime = -1f; }
            }
            else if (NativeBreeding.PaceActive)
            {
                double factor = BreedingPace.GrowthFactor(HusbandryBreedingSettings.GrowthSpeed.Value, Keeper.BestLevel(egg.transform.position));
                if (factor > 1)
                {
                    original = egg.m_growTime;
                    egg.m_growTime = (float)BreedingPace.ScaledTime(egg.m_growTime, factor);
                }
            }
            ItemDrop item = egg.m_item;
            if (item == null || egg.m_grownPrefab == null) return original;
            var traits = NativeBreeding.InheritanceActive ? WildbloodTraitsCapability.Current : new TraitsConsumer(null);
            if (item.m_itemData.m_customData.TryGetValue(BreedingKeys.Egg, out string text))
            {
                if (EggCodec.TryDecode(text, out EggRecord? record) && record != null)
                    ArmCarry(traits.Hatch(record, InstanceToken.New().Value), egg.m_grownPrefab.name);
            }
            else if (traits.Available)
            {
                // The provider reads legacy source egg traits without touching level or custom data.
                ArmCarry(new OffspringCarry(null, traits.Read(item.m_itemData)), egg.m_grownPrefab.name);
            }
            return original;
        }

        private static void ArmCarry(OffspringCarry record, string prefab)
        {
            Carries.Arm(record, prefab);
            carryInheritance = NativeBreeding.InheritanceActive;
            carryPrimary = prefab;
            carryAlternates.Clear();
        }

        /// <summary>Writes carried lineage/traits on the adult or hatchling the game just created (its ZDO is ours).</summary>
        private static void FinishCarry()
        {
            GameObject? grown = claimed;
            OffspringCarry? carry = claimedCarry;
            if (grown == null || carry == null) return;
            Character character = grown.GetComponent<Character>();
            if (character == null || !Owned(character.m_nview)) return;
            var store = new ZdoKeyStore(character.m_nview.GetZDO());
            if (carry.Lineage != null)
            {
                store.Set(InstanceKey, carry.Lineage.Token);
                BreedingStore.WriteLineage(store, carry.Lineage);
            }
            if (carryInheritance && NativeBreeding.InheritanceActive)
            {
                BreedingStore.SetTraits(store, carry.Traits);
                WildbloodTraitsCapability.Current.Apply(character, carry.Traits);
            }
        }

        private static void EndCarry()
        {
            Carries.Clear();
            carryInheritance = false;
            carryAlternates.Clear();
            claimed = null;
            claimedCarry = null;
        }

        private static LineageRecord? MotherRecord(Procreation parent)
        {
            var store = new ZdoKeyStore(parent.m_nview.GetZDO());
            LineageRecord? record = BreedingStore.ReadLineage(store);
            if (record != null) return record;
            string token = Token(parent.m_nview);
            return token.Length > 0 ? Lineage.Wild(token) : null;
        }

        /// <summary>The C12 token of an animal; the owner assigns one when missing.</summary>
        private static string Token(ZNetView view)
        {
            try
            {
                return MeadworksInstanceIdentity.Ensure(view, InstanceKey).Token?.Value ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string NameOf(Character character)
        {
            if (character == null) return "";
            Tameable tameable = character.GetComponent<Tameable>();
            string name = tameable != null ? tameable.GetText() : "";
            return string.IsNullOrEmpty(name) ? character.m_name : name;
        }

        private static string BreederName(Vector3 position)
        {
            if (!NativeHusbandry.Active) return "";
            Player? best = null;
            float bestLevel = -1f;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || player.IsDead()) continue;
                float range = HusbandrySettings.KeeperRange.Value;
                if ((player.transform.position - position).sqrMagnitude > range * range) continue;
                float level = HusbandrySkill.Of(player);
                if (level > bestLevel) { best = player; bestLevel = level; }
            }
            return best != null ? best.GetPlayerName() : "";
        }
    }
}
