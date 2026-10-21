// Wildblood. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT, from SaveYourLuckBruh 1.0.2.
// Pin ece802da9f806f3ee9d6e50552beec479abae4ce. MIT notice: THIRD_PARTY_NOTICES/SaveYourLuckBruh-MIT.txt.
// Changed by expDARE, 2026: per-killer dictionary lease, finalizer restoration and existing LuckDelta integration.
// Source Patches/CharacterDropPatch.cs supplies the last-hit/snapshot/native-drop boundary; attribution is Meadworks-original.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ExtraPotions.Wildblood.Luck;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Runtime;
using HarmonyLib;
using UnityEngine;
using ExtraPotions.Wildblood.AdoptedCm;

namespace ExtraPotions.Wildblood.AdoptedLuck;

[HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
internal static class CharacterDropPatch
{
    [ThreadStatic] private static CounterScope? current;
    internal static bool WeightedDecisionApplied { get; private set; }
    private static bool decisionMismatchLogged;
    private static readonly FieldInfo labelsField = AccessTools.Field(typeof(CodeInstruction), "labels");
    internal static Dictionary<int, int>? ProcessCounters => current?.Revision == StorageManager.Revision ? current.Process : null;
    internal static bool OwnsCurrentCounters => current?.Revision == StorageManager.Revision && current.OwnsCurrent;

    private static int Decide(CharacterDrop drop, CharacterDrop.Drop row, float effectiveChance)
        => WeightedDecisionApplied ? current?.Decide(drop, row, effectiveChance) ?? -1 : -1;

    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
    {
        WeightedDecisionApplied = false;
        var codes = instructions.ToList();
        var locals = __originalMethod.GetMethodBody()?.LocalVariables;
        var quantity = AccessTools.Field(typeof(CharacterDrop.Drop), nameof(CharacterDrop.Drop.m_dontScale));
        var counters = AccessTools.Field(typeof(CharacterDrop), nameof(CharacterDrop.s_pseudoCounter));
        var random = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.value));
        var decide = AccessTools.Method(typeof(CharacterDropPatch), nameof(Decide));
        var quantities = Enumerable.Range(1, Math.Max(0, codes.Count - 1)).Where(i => codes[i].LoadsField(quantity)).ToList();
        var starts = new List<int>();
        for (int i = 0; i + 6 < codes.Count; i++)
            if (codes[i].opcode == OpCodes.Ldc_I4_0 && Local(codes[i + 1], false) >= 0 &&
                Local(codes[i + 2], true) >= 0 &&
                (codes[i + 3].opcode == OpCodes.Brfalse || codes[i + 3].opcode == OpCodes.Brfalse_S) &&
                codes[i + 4].opcode == OpCodes.Ldc_I4_0 && Local(codes[i + 5], false) >= 0 &&
                codes[i + 6].LoadsField(counters) &&
                LocalType(Local(codes[i + 1], false), typeof(bool)) &&
                LocalType(Local(codes[i + 2], true), typeof(bool))) starts.Add(i);
        int amount = quantities.Count == 1 ? quantities[0] - 1 : -1;
        int start = starts.Count == 1 ? starts[0] : -1;
        int row = amount >= 0 ? Local(codes[amount], true) : -1;
        int chance = amount >= 3 ? Local(codes[amount - 2], true) : -1;
        bool valid = locals != null && start >= 0 && amount > start + 6 && row >= 0 && chance >= 0 &&
            LocalType(row, typeof(CharacterDrop.Drop)) && LocalType(chance, typeof(float)) &&
            codes[amount - 3].Calls(random) &&
            (codes[amount - 1].opcode == OpCodes.Bgt_Un || codes[amount - 1].opcode == OpCodes.Bgt_Un_S) &&
            codes[amount - 1].operand is Label && codes[start + 3].operand is Label && labelsField != null && codes[start].blocks.Count == 0 &&
            codes.Count(code => code.Calls(random)) == 1 && codes.Count(code => code.LoadsField(quantity)) == 1;
        if (valid)
        {
            var skip = (Label)codes[amount - 1].operand;
            int skipIndex = codes.FindIndex(code => Labels(code).Contains(skip));
            var nativeBranch = (Label)codes[start + 3].operand;
            int branchIndex = codes.FindIndex(code => Labels(code).Contains(nativeBranch));
            valid = skipIndex > amount && branchIndex > start && branchIndex < amount - 3 &&
                codes.Skip(start).Take(skipIndex - start + 1).All(code => code.blocks.Count == 0);
        }
        if (!valid)
        {
            if (!decisionMismatchLogged)
            {
                decisionMismatchLogged = true;
                Plugin.NativeLogger?.LogWarning("Weighted pity is unavailable: native drop decision pattern did not match; legacy drop behavior is retained.");
            }
            return codes;
        }
        var handled = generator.DeclareLocal(typeof(int));
        Label native = generator.DefineLabel(), accepted = generator.DefineLabel();
        Labels(codes[amount]).Add(accepted);
        var injected = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0), new(codes[amount].opcode, codes[amount].operand),
            new(codes[amount - 2].opcode, codes[amount - 2].operand), new(OpCodes.Call, decide),
            new(OpCodes.Stloc, handled), new(OpCodes.Ldloc, handled), new(OpCodes.Ldc_I4_0), new(OpCodes.Blt, native),
            new(OpCodes.Ldloc, handled), new(OpCodes.Brtrue, accepted), new(OpCodes.Br, codes[amount - 1].operand)
        };
        codes[start].MoveLabelsTo(injected[0]);
        Labels(codes[start]).Add(native);
        codes.InsertRange(start, injected);
        WeightedDecisionApplied = true;
        return codes;

        bool LocalType(int index, Type type) => locals != null && index >= 0 && index < locals.Count && locals[index].LocalType == type;
    }

    private static IList Labels(CodeInstruction code) => (IList)labelsField.GetValue(code);

    private static int Local(CodeInstruction code, bool load)
    {
        if (code.opcode == (load ? OpCodes.Ldloc_0 : OpCodes.Stloc_0)) return 0;
        if (code.opcode == (load ? OpCodes.Ldloc_1 : OpCodes.Stloc_1)) return 1;
        if (code.opcode == (load ? OpCodes.Ldloc_2 : OpCodes.Stloc_2)) return 2;
        if (code.opcode == (load ? OpCodes.Ldloc_3 : OpCodes.Stloc_3)) return 3;
        if (code.opcode != (load ? OpCodes.Ldloc : OpCodes.Stloc) && code.opcode != (load ? OpCodes.Ldloc_S : OpCodes.Stloc_S)) return -1;
        return code.operand switch { LocalBuilder local => local.LocalIndex, LocalVariableInfo local => local.LocalIndex,
            int index => index, short index => index, byte index => index, _ => -1 };
    }

    internal sealed class CounterScope
    {
        internal readonly Dictionary<int, int> Process;
        private readonly Dictionary<int, int> previous, working;
        private readonly CounterScope? parent;
        private readonly Dictionary<int, int> before;
        private readonly List<(CharacterDrop.Drop Row, float Chance)> heldChances = new();
        private readonly HashSet<CharacterDrop.Drop> weightedRows = new();
        private readonly PityChain? chain;
        private IReadOnlyList<PitySource>? pityCatalog;
        private readonly CharacterDrop drop;
        private readonly LuckAttributionRuntime.Credit? credit;
        private readonly List<LuckDelta> deltas = new();
        private bool restored, captured;
        private readonly bool localPersisted, lucky;
        internal readonly long Revision;
        internal bool OwnsCurrent => ReferenceEquals(CharacterDrop.s_pseudoCounter, working);

        private sealed class PityChain
        {
            internal readonly CounterScope Root;
            internal readonly LuckTable Table;
            internal readonly List<(LuckAttributionRuntime.Credit Credit, List<LuckDelta> Deltas)> Claims = new();
            internal PityChain(CounterScope root, LuckTable table, bool ordered)
            { Root = root; Table = ordered ? table : LuckCodec.Parse(LuckCodec.Write(table)); }
        }

        internal CounterScope(CharacterDrop drop, LuckAttributionRuntime.Credit? credit, bool lucky, bool localPersisted)
        {
            this.drop = drop;
            this.credit = credit;
            this.lucky = lucky;
            this.localPersisted = localPersisted;
            Revision = StorageManager.Revision;
            parent = current;
            previous = CharacterDrop.s_pseudoCounter;
            Process = parent?.Revision == Revision ? parent.Process : previous;
            working = credit != null ? LuckAttributionRuntime.NativeCounters(credit.Table)
                : lucky ? new Dictionary<int, int>(Process)
                : Process;
            before = new Dictionary<int, int>(working);
            if (credit != null && credit.WeightedSupported && WeightedDecisionApplied)
            {
                for (var ancestor = parent; ancestor != null && ancestor.Revision == Revision; ancestor = ancestor.parent)
                    if (ancestor.chain != null && ancestor.credit != null && ReferenceEquals(ancestor.credit.View, credit.View) &&
                        ancestor.credit.World == credit.World && ancestor.credit.Generation == credit.Generation && ancestor.credit.Owner == credit.Owner)
                    { chain = ancestor.chain; break; }
                chain ??= new PityChain(this, credit.Table, credit.Decision != null);
            }
        }

        internal void Enter()
        {
            CharacterDrop.s_pseudoCounter = working;
            current = this;
            if (!lucky) return;
            // Restore the exact row objects and values, including a nested caller's temporary values.
            foreach (var row in drop.m_drops)
            {
                if (row == null || row.m_prefab == null || !(row.m_chance > 0f && row.m_chance <= 0.3f)) continue;
                heldChances.Add((row, row.m_chance));
                row.m_chance = 1f;
            }
        }

        internal int Decide(CharacterDrop source, CharacterDrop.Drop row, float effectiveChance)
        {
            if (lucky || captured || restored || Revision != StorageManager.Revision ||
                !ReferenceEquals(source, drop) || !OwnsCurrent || drop.m_character == null ||
                ZoneSystem.instance == null || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPseudoDrops) ||
                Terminal.m_showTests && (Terminal.m_testList.ContainsKey("pseudo") || Terminal.m_testList.ContainsKey("pseudo2"))) return -1;
            LuckTable table;
            if (credit != null)
            {
                if (!credit.WeightedSupported || chain == null) return -1;
                table = chain.Table;
            }
            else if (!localPersisted || !StorageManager.TryNativeNames(out table)) return -1;
            pityCatalog ??= NativeWeightedPity.Catalog();
            if (pityCatalog == null || !NativeWeightedPity.SafeSource(drop, row, pityCatalog) ||
                !WeightedPity.TryWeight(credit?.Decision?.Grant.Request.Level ?? drop.m_character.GetLevel(), row.m_levelMultiplier, out int weight) ||
                1f / row.m_chance * 2f > int.MaxValue - 2d) return -1;
            string prefab = row.m_prefab.name;
            if (!NativeWeightedPity.KnownAliases(table, working, prefab, row.m_chance, row.m_levelMultiplier) ||
                !WeightedPity.TryMerge(table, pityCatalog, prefab, row.m_chance, out int? prior)) return -1;
            bool fresh = !prior.HasValue;
            int remaining = prior ?? UnityEngine.Random.Range(0, (int)(1f / row.m_chance * 2f));
            bool due = remaining <= (fresh ? 0 : weight);
            // Commit the probability decision before native quantity hooks can generate another drop list.
            bool success = effectiveChance <= .3f ? due : UnityEngine.Random.value <= effectiveChance || due;
            int next = success ? UnityEngine.Random.Range(0, (int)(1f / row.m_chance * 2f) + 2)
                : fresh ? remaining : Math.Max(0, remaining - weight);
            var delta = new LuckDelta(prefab, row.m_chance, success || fresh ? 0 : weight,
                success || fresh ? next : -1, true);
            if (!NativeWeightedPity.Apply(table, pityCatalog, delta))
                throw new InvalidOperationException("Validated weighted-pity decision could not be stored");
            foreach (var entry in table.Entries.Where(entry => entry.Prefab == prefab))
                working[NativeLuckCounters.Bucket(entry.Prefab.GetStableHashCode(), entry.Chance)] = entry.Counter;
            weightedRows.Add(row);
            if (credit != null) { deltas.Add(delta); credit.Decision?.Record(delta); }
            else StorageManager.StoreNativeNames(table);
            return success ? 1 : 0;
        }

        internal void Capture(List<KeyValuePair<GameObject, int>> result)
        {
            if (captured || restored) return;
            captured = true;
            if (lucky || !ReferenceEquals(CharacterDrop.s_pseudoCounter, working)) return;
            foreach (var hold in heldChances) hold.Row.m_chance = hold.Chance;
            heldChances.Clear();
            if (credit == null) return;
            var rows = new Dictionary<int, (string Prefab, float Chance, int Count)>();
            var ambiguous = new HashSet<int>();
            foreach (var row in drop.m_drops)
            {
                if (row == null || row.m_prefab == null || weightedRows.Contains(row)) continue;
                string prefab = row.m_prefab.name;
                float chance = EffectiveChance(drop, row);
                if (!(chance > 0f && chance <= 0.3f)) continue;
                int bucket = NativeLuckCounters.Bucket(prefab.GetStableHashCode(), chance);
                if (rows.TryGetValue(bucket, out var prior))
                {
                    if (prior.Prefab != prefab) ambiguous.Add(bucket);
                    rows[bucket] = (prefab, chance, prior.Count + 1);
                }
                else rows.Add(bucket, (prefab, chance, 1));
            }
            foreach (var entry in rows)
            {
                if (ambiguous.Contains(entry.Key) || !working.TryGetValue(entry.Key, out int after) || after < 0) continue;
                bool hadBefore = before.TryGetValue(entry.Key, out int prior);
                if (hadBefore && prior == after) continue;
                if (hadBefore && prior > entry.Value.Count && after == prior - entry.Value.Count)
                    deltas.Add(new LuckDelta(entry.Value.Prefab, entry.Value.Chance, entry.Value.Count, -1));
                else deltas.Add(new LuckDelta(entry.Value.Prefab, entry.Value.Chance, 0, after));
                if (credit.Decision != null)
                {
                    var recorded = deltas[deltas.Count - 1];
                    credit.Decision.Table.Apply(recorded);
                    credit.Decision.Record(recorded);
                }
            }
        }

        internal void Restore()
        {
            if (restored) return;
            restored = true;
            foreach (var hold in heldChances) hold.Row.m_chance = hold.Chance;
            if (Revision == StorageManager.Revision && ReferenceEquals(CharacterDrop.s_pseudoCounter, working))
                CharacterDrop.s_pseudoCounter = previous;
            if (ReferenceEquals(current, this)) current = parent?.Revision == StorageManager.Revision ? parent : null;
        }

        internal void Complete(bool success)
        {
            if (lucky) return;
            if (credit?.Decision != null) return;
            if (credit != null)
            {
                var outgoing = success && captured ? deltas : deltas.Where(delta => delta.Weighted).ToList();
                if (chain != null)
                {
                    if (outgoing.Count != 0) chain.Claims.Add((credit, outgoing));
                    if (!ReferenceEquals(chain.Root, this)) return;
                    var normalized = WeightedPity.ReconcileChain(chain.Claims.SelectMany(claim => claim.Deltas).ToList(), chain.Table);
                    int offset = 0;
                    foreach (var claim in chain.Claims)
                    {
                        LuckAttributionRuntime.Send(claim.Credit, normalized.Skip(offset).Take(claim.Deltas.Count).ToList());
                        offset += claim.Deltas.Count;
                    }
                }
                else if (success && captured && outgoing.Count != 0) LuckAttributionRuntime.Send(credit, outgoing);
            }
            else if (localPersisted && Player.m_localPlayer != null && ZNet.instance != null && ZNet.m_world != null)
                StorageManager.UpdateMemoryCache(Player.m_localPlayer, ZNet.instance.GetWorldUID());
        }
    }

    internal static float EffectiveChance(CharacterDrop drop, CharacterDrop.Drop row)
    {
        if (!row.m_levelMultiplier) return row.m_chance;
        Character character = drop.m_character;
        float factor = character == null ? 1 : Mathf.Max(1, (int)Mathf.Pow(2f, character.GetLevel() - 1));
        return NativePseudoChancePatch.Applied ? NativePseudoChance.ScaleChance(row.m_chance, factor, row, character) : row.m_chance * factor;
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix(CharacterDrop __instance, out CounterScope? __state)
    {
        __state = null;
        LuckDeathAdmission.TryCurrent(__instance, out var admitted);
        bool lucky = admitted == null && Plugin.Settings?.Get(WildbloodSettings.Cheats.LuckyKills) == true;
        if (__instance == null || (admitted == null && (StorageManager.CurrentMode == LuckMode.Off ||
            (!lucky && !StorageManager.IsPersistenceEnabled)))) return;
        Character character = __instance.m_character;
        if (character == null || character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner()) return;
        // EnsureLoaded may restore local persistence. It must run before taking a process-dictionary lease.
        if (Player.m_localPlayer != null && ZNet.instance != null && ZNet.m_world != null)
            StorageManager.EnsureLoaded(Player.m_localPlayer, ZNet.instance.GetWorldUID());
        LuckAttributionRuntime.Credit? credit = admitted;
        // Nested uncredited kills use the true process dictionary, never the enclosing killer's table.
        if (credit == null && !lucky && current == null && StorageManager.CurrentMode != LuckMode.LocalPersisted) return;
        __state = new CounterScope(__instance, credit, lucky, StorageManager.CurrentMode == LuckMode.LocalPersisted);
        try { __state.Enter(); }
        catch { __state.Restore(); throw; }
    }

    [HarmonyPriority(Priority.First)]
    private static void Postfix(CounterScope? __state, List<KeyValuePair<GameObject, int>> __result)
    {
        try { __state?.Capture(__result); }
        finally { __state?.Restore(); }
    }

    private static Exception? Finalizer(Exception? __exception, CounterScope? __state)
    {
        __state?.Restore();
        // Weighted decisions commit before native quantity hooks, including when a later hook throws.
        try { __state?.Complete(__exception == null); }
        catch (Exception error) { Plugin.NativeLogger?.LogWarning("Luck update refused: " + error.GetType().Name); }
        return __exception;
    }
}
