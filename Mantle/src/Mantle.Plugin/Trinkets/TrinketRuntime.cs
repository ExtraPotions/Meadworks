// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Mantle.Trinkets
{
    internal static class TrinketRuntime
    {
        internal sealed class NativeSource
        {
            internal ItemDrop.ItemData Item;
            internal SE_Stats Effect;
            internal TrinketRow Row;
        }

        private sealed class RunningEffect
        {
            internal SE_Stats Effect;
            internal Player Player;
            internal NativeSource Source;
            internal TrinketRow Applied;
            internal float DurationMultiplier;
        }

        internal sealed class AddContext
        {
            internal Player Player;
            internal NativeSource Source;
            internal TrinketRow Row;
        }

        private static readonly string[] FloatFields =
        {
            "m_healthUpFront", "m_staminaUpFront", "m_eitrUpFront", "m_skillLevelModifier",
            "m_skillLevelModifier2", "m_addArmor", "m_healthRegenMultiplier", "m_staminaRegenMultiplier",
            "m_eitrRegenMultiplier", "m_timedBlockBonus", "m_runStaminaDrainModifier", "m_runStaminaUseModifier",
            "m_attackStaminaUseModifier", "m_blockStaminaUseModifier", "m_swimStaminaUseModifier",
            "m_speedModifier", "m_swimSpeedModifier"
        };
        private static readonly Dictionary<string, NativeSource> sources = new Dictionary<string, NativeSource>(StringComparer.Ordinal);
        private static readonly Dictionary<SE_Stats, RunningEffect> running = new Dictionary<SE_Stats, RunningEffect>();
        private static readonly Stack<AddContext> adds = new Stack<AddContext>();
        private static bool initialized;
        private static bool ticking;
        private static ObjectDB database;
        private static float nextCheck;
        private static bool foreignOwner;
        private static Player lastPlayer;
        private static float lastMaximum;
        private static bool lastThresholdOwned;

        internal static bool BaselineReady => sources.Count == TrinketTable.NativeIds.Count;
        internal static bool CanApply => CanTune && string.IsNullOrEmpty(FallbackReason);
        internal static string FallbackReason { get; private set; } = "native-data-pending";

        internal static void Initialize()
        {
            initialized = true;
            nextCheck = 0f;
            TrinketSettings.Changed -= OnSettingsChanged;
            TrinketSettings.Changed += OnSettingsChanged;
        }

        internal static void Dispose()
        {
            TrinketSettings.Changed -= OnSettingsChanged;
            foreach (RunningEffect effect in running.Values) Restore(effect);
            running.Clear();
            sources.Clear();
            adds.Clear();
            initialized = false;
            if (lastThresholdOwned && lastPlayer != null) lastPlayer.GetMaxAdrenaline();
            database = null;
            lastPlayer = null;
            lastMaximum = 0f;
            lastThresholdOwned = false;
        }

        internal static void Tick()
        {
            if (!initialized || ticking || Time.unscaledTime < nextCheck) return;
            ticking = true;
            try { Refresh(); }
            finally { ticking = false; }
        }

        private static void OnSettingsChanged()
        {
            nextCheck = 0f;
            Tick();
        }

        private static void Refresh()
        {
            nextCheck = Time.unscaledTime + 0.25f;
            if (database != ObjectDB.instance)
            {
                foreach (RunningEffect effect in running.Values) Restore(effect);
                running.Clear();
                sources.Clear();
                database = ObjectDB.instance;
            }
            if (!BaselineReady && database != null) CaptureNative(database);
            if (BaselineReady) FallbackReason = "";
            foreignOwner = HasForeignOwner();
            Player player = Player.m_localPlayer;
            bool active = CanTune;
            var expired = new List<SE_Stats>();
            foreach (var pair in running)
            {
                RunningEffect state = pair.Value;
                if (state.Player == null || state.Player.GetSEMan().GetStatusEffect(pair.Key.NameHash()) != pair.Key)
                { expired.Add(pair.Key); continue; }
                if (!active || !SourceUnchanged(state.Source))
                { Restore(state); expired.Add(pair.Key); continue; }
                if (TrinketSettings.EffectiveTable.TryGet(state.Source.Row.PrefabId, out TrinketRow row))
                    ApplyRunning(state, row);
            }
            foreach (SE_Stats effect in expired) running.Remove(effect);

            // A settings edit also reaches a buff that was triggered before tuning was enabled.
            if (active && player != null && TryFindEquipped(player, out NativeSource equipped) && SourceUnchanged(equipped))
            {
                NativeSource source = equipped;
                if (TrinketSettings.EffectiveTable.TryGet(source.Row.PrefabId, out TrinketRow effective))
                {
                    var effect = player.GetSEMan().GetStatusEffect(source.Effect.NameHash()) as SE_Stats;
                    if (effect != null && !running.ContainsKey(effect) && EffectsEqual(effect, source.Row))
                    {
                        var state = new RunningEffect { Effect = effect, Player = player, Source = source,
                            Applied = source.Row, DurationMultiplier = effect.m_ttl / source.Row.Duration };
                        running.Add(effect, state);
                        ApplyRunning(state, effective);
                    }
                }
            }
            if (player != null)
            {
                player.GetMaxAdrenaline();
            }
            else { lastPlayer = null; lastMaximum = 0f; lastThresholdOwned = false; }
        }

        private static bool CanTune => initialized && BaselineReady && !foreignOwner &&
            TrinketSettings.Enabled && TrinketSettings.EffectiveTable != null && TrinketKillCredit.TuningSupported;

        internal static bool TryGetEquippedNative(Player player, out string prefab)
        {
            prefab = null;
            if (!BaselineReady || !TryFindEquipped(player, out NativeSource source)) return false;
            prefab = source.Row.PrefabId;
            return true;
        }

        internal static bool TryGetEffectiveMaximum(Player player, out float maximum)
        {
            maximum = 0f;
            if (!TryGetSource(player, out _, out _)) return false;
            maximum = player.GetMaxAdrenaline();
            return Finite(maximum) && maximum > 0f;
        }

        internal static bool TryGetKillFraction(Player player, out float fraction)
        {
            fraction = 0f;
            if (!TryGetSource(player, out _, out TrinketRow row)) return false;
            fraction = row.KillFraction;
            return fraction > 0f;
        }

        private static bool TryGetSource(Player player, out NativeSource source, out TrinketRow row)
        {
            row = null;
            source = null;
            return CanTune && TryFindEquipped(player, out source) && SourceUnchanged(source) &&
                TrinketSettings.EffectiveTable.TryGet(source.Row.PrefabId, out row);
        }

        private static bool TryFindEquipped(Player player, out NativeSource source)
        {
            source = null;
            if (player == null || player.GetInventory() == null) return false;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item == null || !item.m_equipped || item.m_shared == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trinket) continue;
                // Multiple trinkets or a third-party identity are outside option A.
                if (source != null || item.m_dropPrefab == null || !sources.TryGetValue(item.m_dropPrefab.name, out NativeSource found)) return false;
                if (!ReferenceEquals(item.m_shared.m_fullAdrenalineSE, found.Effect) || item.m_shared.m_maxAdrenaline != found.Row.Threshold) return false;
                source = found;
            }
            return source != null;
        }

        internal static void Maximum(Player player, ref float value)
        {
            if (!ReferenceEquals(player, Player.m_localPlayer)) return;
            bool owned = false;
            if (TryGetSource(player, out NativeSource source, out TrinketRow row))
            {
                float delta = row.Threshold - source.Row.Threshold;
                value += delta;
                owned = delta != 0f;
            }
            // GetMaxAdrenaline can be reached by the next native gain before LateUpdate.
            // Assigning the meter directly avoids AddAdrenaline's full-meter effect branch.
            bool samePlayer = ReferenceEquals(lastPlayer, player);
            if ((owned || (samePlayer && lastThresholdOwned)) &&
                ((samePlayer && lastMaximum != value) || (!samePlayer && owned)))
                ClampMeter(player, value);
            lastPlayer = player;
            lastMaximum = value;
            lastThresholdOwned = owned;
        }

        private static void ClampMeter(Player player, float maximum)
        {
            if (!Finite(maximum) || maximum <= 0f) return;
            if (player.m_adrenaline >= maximum)
                player.m_adrenaline = Mathf.Max(0f, maximum - Mathf.Max(0.0001f, maximum * 0.000001f));
        }

        private static void CaptureNative(ObjectDB db)
        {
            var captured = new Dictionary<string, NativeSource>(StringComparer.Ordinal);
            var rows = new List<TrinketRow>();
            var hashes = new HashSet<int>();
            foreach (string id in TrinketTable.NativeIds)
            {
                GameObject prefab = db.GetItemPrefab(id);
                ItemDrop drop = prefab == null ? null : prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) { FallbackReason = "native-data-pending"; return; }
                ItemDrop.ItemData item = drop.m_itemData;
                var effect = item.m_shared.m_fullAdrenalineSE as SE_Stats;
                if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trinket || effect == null ||
                    effect.GetType() != typeof(SE_Stats) || effect.m_mods == null || !hashes.Add(effect.NameHash()))
                { FallbackReason = "native-identity-conflict"; return; }
                IReadOnlyDictionary<string, float> fields;
                try { fields = ReadEffects(effect); }
                catch (ArgumentException) { FallbackReason = "native-table-invalid"; return; }
                var row = new TrinketRow(id, item.m_shared.m_maxAdrenaline, effect.m_ttl, 0f, fields);
                rows.Add(row);
                captured.Add(id, new NativeSource { Item = item, Effect = effect, Row = row });
            }
            if (!TrinketTable.TryCreateNative(rows, out TrinketTable table, out string error))
            {
                FallbackReason = "native-table-invalid";
                Plugin.Log?.LogWarning("Mantle trinket native data rejected: " + error);
                return;
            }
            foreach (var pair in captured) sources.Add(pair.Key, pair.Value);
            FallbackReason = "";
            TrinketSettings.SetNativeTable(table);
            Plugin.Log?.LogInfo("Mantle captured and validated all 15 native trinkets from ObjectDB; item templates are unchanged.");
        }

        private static IReadOnlyDictionary<string, float> ReadEffects(SE_Stats effect)
        {
            var values = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (string field in FloatFields)
            {
                float value = (float)typeof(SE_Stats).GetField(field).GetValue(effect);
                float nativeDefault = field.EndsWith("RegenMultiplier", StringComparison.Ordinal) ? 1f : 0f;
                if (value != nativeDefault) values.Add(field, value);
            }
            if (effect.m_percentigeDamageModifiers.m_slash != 0f)
                values.Add("m_percentigeDamageModifiers.m_slash", effect.m_percentigeDamageModifiers.m_slash);
            if (effect.m_percentigeDamageModifiers.m_blunt != 0f)
                values.Add("m_percentigeDamageModifiers.m_blunt", effect.m_percentigeDamageModifiers.m_blunt);
            if (effect.m_percentigeDamageModifiers.m_pierce != 0f)
                values.Add("m_percentigeDamageModifiers.m_pierce", effect.m_percentigeDamageModifiers.m_pierce);
            foreach (HitData.DamageModPair mod in effect.m_mods)
                values.Add("resistance." + mod.m_type, (float)mod.m_modifier);
            return values;
        }

        private static bool SourceUnchanged(NativeSource source)
        {
            bool valid = source.Item.m_shared.m_maxAdrenaline == source.Row.Threshold &&
                ReferenceEquals(source.Item.m_shared.m_fullAdrenalineSE, source.Effect) &&
                source.Effect.m_ttl == source.Row.Duration && EffectsEqual(source.Effect, source.Row);
            if (!valid) FallbackReason = "native-template-conflict";
            return valid;
        }

        private static bool EffectsEqual(SE_Stats effect, TrinketRow row)
        {
            IReadOnlyDictionary<string, float> actual;
            try { actual = ReadEffects(effect); }
            catch (ArgumentException) { return false; }
            foreach (var pair in actual)
                if (!row.Effects.ContainsKey(pair.Key)) return false;
            foreach (var pair in row.Effects)
            {
                float value;
                if (!actual.TryGetValue(pair.Key, out value))
                    value = pair.Key.EndsWith("RegenMultiplier", StringComparison.Ordinal) ? 1f : 0f;
                if (value != pair.Value) return false;
            }
            return true;
        }

        private static void WriteEffects(SE_Stats effect, TrinketRow row)
        {
            // StatusEffect.Clone is MemberwiseClone, so lists must be replaced before writing.
            var mods = new List<HitData.DamageModPair>(effect.m_mods);
            foreach (var pair in row.Effects)
            {
                if (pair.Key.StartsWith("resistance.", StringComparison.Ordinal))
                {
                    var type = (HitData.DamageType)Enum.Parse(typeof(HitData.DamageType), pair.Key.Substring(11));
                    for (int index = 0; index < mods.Count; ++index)
                        if (mods[index].m_type == type)
                            mods[index] = new HitData.DamageModPair { m_type = type, m_modifier = (HitData.DamageModifier)(int)pair.Value };
                }
                else if (pair.Key == "m_percentigeDamageModifiers.m_slash") effect.m_percentigeDamageModifiers.m_slash = pair.Value;
                else if (pair.Key == "m_percentigeDamageModifiers.m_blunt") effect.m_percentigeDamageModifiers.m_blunt = pair.Value;
                else if (pair.Key == "m_percentigeDamageModifiers.m_pierce") effect.m_percentigeDamageModifiers.m_pierce = pair.Value;
                else typeof(SE_Stats).GetField(pair.Key).SetValue(effect, pair.Value);
            }
            effect.m_mods = mods;
        }

        internal static AddContext BeginAdd(SEMan manager, StatusEffect source)
        {
            AddContext context = null;
            if (manager.m_character is Player player && ReferenceEquals(player, Player.m_localPlayer) &&
                TryGetSource(player, out NativeSource native, out TrinketRow row) && ReferenceEquals(source, native.Effect))
                context = new AddContext { Player = player, Source = native, Row = row };
            adds.Push(context);
            return context;
        }

        internal static void BeforeSetup(SE_Stats effect, Character character)
        {
            AddContext context = adds.Count == 0 ? null : adds.Peek();
            if (context != null && ReferenceEquals(character, context.Player) &&
                effect.NameHash() == context.Source.Effect.NameHash() && !ReferenceEquals(effect, context.Source.Effect))
                WriteEffects(effect, context.Row);
        }

        internal static void AfterAdd(AddContext context, StatusEffect result)
        {
            if (context == null || !(result is SE_Stats effect)) return;
            // Inscribe reads the untouched native source and applies its bonus in its postfix.
            // Our postfix runs after it; the observed native-duration ratio is applied once.
            float multiplier = effect.m_ttl / context.Source.Row.Duration;
            if (!Finite(multiplier) || multiplier <= 0f) return;
            effect.m_ttl = context.Row.Duration * multiplier;
            running[effect] = new RunningEffect { Effect = effect, Player = context.Player, Source = context.Source,
                Applied = context.Row, DurationMultiplier = multiplier };
        }

        internal static void EndAdd() { if (adds.Count > 0) adds.Pop(); }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ApplyRunning(RunningEffect state, TrinketRow row)
        {
            if (ReferenceEquals(row, state.Applied)) return;
            if (!EffectsEqual(state.Effect, state.Applied)) { FallbackReason = "running-effect-conflict"; return; }
            WriteEffects(state.Effect, row);
            state.Effect.m_ttl = row.Duration * state.DurationMultiplier;
            state.Applied = row;
        }

        private static void Restore(RunningEffect state)
        {
            if (state.Effect == null) return;
            // Preserve another mod's intervening write rather than overwriting its ownership.
            if (EffectsEqual(state.Effect, state.Applied)) WriteEffects(state.Effect, state.Source.Row);
            if (state.Effect.m_ttl == state.Applied.Duration * state.DurationMultiplier)
                state.Effect.m_ttl = state.Source.Row.Duration * state.DurationMultiplier;
        }

        private static bool HasForeignOwner()
        {
            MethodBase[] targets = { AccessTools.Method(typeof(Player), nameof(Player.GetMaxAdrenaline)),
                AccessTools.Method(typeof(SE_Stats), nameof(SE_Stats.Setup)),
                AccessTools.Method(typeof(SEMan), nameof(SEMan.AddStatusEffect),
                    new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) }) };
            foreach (MethodBase method in targets)
            {
                Patches info = Harmony.GetPatchInfo(method);
                if (info == null) continue;
                string owner = UnknownOwner(info.Prefixes, method, false) ?? UnknownOwner(info.Postfixes, method, true) ??
                    UnknownOwner(info.Transpilers, method, false) ?? UnknownOwner(info.Finalizers, method, false);
                if (owner != null) { FallbackReason = "foreign-tuning-owner:" + owner; return true; }
            }
            return false;
        }

        private static string UnknownOwner(IEnumerable<Patch> patches, MethodBase target, bool postfix)
        {
            foreach (Patch patch in patches)
            {
                if (patch.owner == Plugin.PluginGuid || patch.owner == "com.meadworks.inscribe") continue;
                // Saga's reviewed swimming guard changes only the Wet effect, never a trinket.
                if (postfix && patch.owner == "com.meadworks.saga" && target.DeclaringType == typeof(SEMan) &&
                    target.Name == nameof(SEMan.AddStatusEffect) && patch.PatchMethod?.Name == "Postfix" &&
                    patch.PatchMethod.DeclaringType?.FullName == "ExtraPotions.Saga.Runtime.NativeSwimming+Wet") continue;
                return patch.owner;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxAdrenaline))]
    internal static class TrinketMaximumPatch
    {
        private static void Postfix(Player __instance, ref float __result) => TrinketRuntime.Maximum(__instance, ref __result);
    }

    [HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.Setup))]
    internal static class TrinketSetupPatch
    {
        private static void Prefix(SE_Stats __instance, Character character) => TrinketRuntime.BeforeSetup(__instance, character);
    }

    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect),
        new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) })]
    internal static class TrinketEffectPatch
    {
        private static void Prefix(SEMan __instance, StatusEffect statusEffect, out TrinketRuntime.AddContext __state) =>
            __state = TrinketRuntime.BeginAdd(__instance, statusEffect);

        [HarmonyAfter("com.meadworks.inscribe")]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(TrinketRuntime.AddContext __state, StatusEffect __result) => TrinketRuntime.AfterAdd(__state, __result);

        private static Exception Finalizer(Exception __exception) { TrinketRuntime.EndAdd(); return __exception; }
    }
}
