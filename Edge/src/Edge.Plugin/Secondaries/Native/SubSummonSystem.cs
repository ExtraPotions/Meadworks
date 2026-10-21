// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// Changed by expDARE, 2026: Edge namespace, catalogs and product boundaries; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

// Instance state only: neither the original monster nor its attack prefabs are changed.
internal static class SubSummonSystem
{
    internal const int Limit = 2;
    private static readonly int ParentKey = "SecondaryAttacks_SubSummonParent".GetStableHashCode();
    private static readonly int ChildKey = "SecondaryAttacks_SubSummonChild".GetStableHashCode();
    private static readonly int ChildParentKey = "SecondaryAttacks_SubSummonChildParent".GetStableHashCode();
    private static readonly int PlayerKey = "SecondaryAttacks_SubSummonPlayer".GetStableHashCode();
    private static readonly int[] Slots = { "SecondaryAttacks_SubSummonSlot0".GetStableHashCode(), "SecondaryAttacks_SubSummonSlot1".GetStableHashCode() };
    private static readonly KeyValuePair<int, int>[] References = { ZDO.GetHashZDOID("SecondaryAttacks_SubSummonRef0"), ZDO.GetHashZDOID("SecondaryAttacks_SubSummonRef1") };
    private const string CheckRpc = "Edge_CheckSubSummon";
    private const string ReleaseRpc = "Edge_ReleaseSubSummon";
    private static readonly AccessTools.FieldRef<SpawnAbility, Character> Owner = AccessTools.FieldRefAccess<SpawnAbility, Character>("m_owner");
    private static readonly AccessTools.FieldRef<SpawnAbility, ItemDrop.ItemData> Weapon = AccessTools.FieldRefAccess<SpawnAbility, ItemDrop.ItemData>("m_weapon");
    private static readonly ConditionalWeakTable<ZDOMan, WorldState> Worlds = new();

    private sealed class WorldState
    {
        public WorldState() { }
        internal readonly HashSet<string> Creating = new(StringComparer.Ordinal);
        // Positive server evidence only. A missing client ZDO can be outside its
        // interest area; a missing server ZDO can still be awaiting initial sync.
        internal readonly HashSet<string> Destroyed = new(StringComparer.Ordinal);
    }

    internal static GameObject? Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, SpawnAbility ability)
    {
        if (!EdgeModules.Active(EdgeModule.Secondaries)) return Object.Instantiate(prefab, position, rotation);
        Character owner = Owner(ability);
        if (prefab.GetComponent<Character>() == null || owner == null)
            return Object.Instantiate(prefab, position, rotation);

        ZDO? parent = owner.GetComponent<ZNetView>()?.GetZDO();
        string parentToken = parent?.GetString(ParentKey, "") ?? "";
        if (parentToken.Length == 0 || !owner.IsTamed())
        {
            GameObject direct = Object.Instantiate(prefab, position, rotation);
            ItemDrop.ItemData item = Weapon(ability);
            if (owner is Player player && item?.m_shared?.m_skillType == Skills.SkillType.BloodMagic &&
                direct.GetComponent<Character>() is Character character && character.IsTamed())
            {
                ZDO? zdo = character.GetComponent<ZNetView>()?.GetZDO();
                if (zdo != null && zdo.IsOwner())
                {
                    // GUIDs survive the game's world-load ZDOID reassignment.
                    zdo.Set(ParentKey, Guid.NewGuid().ToString("N"));
                    zdo.Set(PlayerKey, player.GetPlayerID());
                }
            }
            return direct;
        }

        if (parent == null || !parent.IsOwner() || owner.IsDead() || ZDOMan.instance == null)
            return null;
        WorldState world = Worlds.GetOrCreateValue(ZDOMan.instance);
        if (!world.Creating.Add(parentToken))
            return null;
        try
        {
            int slot = FindSlot(parent, world);
            if (slot < 0)
                return null;

            // No yield between capacity check and slot assignment. This also
            // handles several children in one cast and overlapping coroutines.
            GameObject child = Object.Instantiate(prefab, position, rotation);
            Character? character = child.GetComponent<Character>();
            ZDO? childZdo = child.GetComponent<ZNetView>()?.GetZDO();
            if (character == null || childZdo == null || !childZdo.IsOwner() || !parent.IsOwner())
            {
                if (childZdo != null && childZdo.IsOwner())
                    ZNetScene.instance.Destroy(child);
                else if (childZdo == null)
                    Object.Destroy(child);
                return null;
            }

            string childToken = Guid.NewGuid().ToString("N");
            childZdo.Set(ChildKey, childToken);
            childZdo.Set(ChildParentKey, parentToken);
            childZdo.Set(PlayerKey, parent.GetLong(PlayerKey, 0L));
            parent.Set(Slots[slot], childToken);
            parent.Set(References[slot], childZdo.m_uid);
            character.SetTamed(true);
            character.m_faction = Character.Faction.PlayerSpawned;
            // Do not give children ParentKey: grandchildren retain vanilla behavior.
            return child;
        }
        finally
        {
            world.Creating.Remove(parentToken);
        }
    }

    private static int FindSlot(ZDO parent, WorldState world)
    {
        for (int slot = 0; slot < Limit; slot++)
        {
            string token = parent.GetString(Slots[slot], "");
            if (token.Length == 0)
                return slot;
            ZDO? child = ZDOMan.instance.GetZDO(parent.GetZDOID(References[slot]));
            if (child != null && child.GetString(ChildKey, "") != token)
                child = null;
            if ((child != null && child.GetFloat(ZDOVars.s_health, float.PositiveInfinity) <= 0f) ||
                (ZNet.instance != null && ZNet.instance.IsServer() && world.Destroyed.Contains(token)))
            {
                parent.Set(Slots[slot], "");
                parent.Set(References[slot], ZDOID.None);
                return slot;
            }
            if (ZNet.instance != null && !ZNet.instance.IsServer() && ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(CheckRpc, parent.m_uid, token);
        }
        return -1;
    }

    internal static void RestoreFaction(Character character, ZNetView? view, ref Character.Faction faction)
    {
        // A cached integer-key lookup also covers ZDO updates received after
        // Awake. No scene search, allocation, or RPC on this AI query path.
        if (!EdgeModules.Active(EdgeModule.Secondaries)) return;
        ZDO? zdo = view?.GetZDO();
        if (zdo != null && zdo.GetString(ChildKey, "").Length != 0)
        {
            character.m_faction = Character.Faction.PlayerSpawned;
            faction = Character.Faction.PlayerSpawned;
        }
    }

    private static ZRoutedRpc? _registeredRouter;
    private static RoutedMethodBase? _checkRegistration, _releaseRegistration;
    private static readonly Action<long, ZDOID, string> CheckHandler = CheckDestroyed;
    private static readonly Action<long, ZDOID, string> ReleaseHandler = ReleaseSlot;

    internal static void RegisterRpc(ZRoutedRpc router)
    {
        if (ReferenceEquals(_registeredRouter, router)) return;
        DisposeRpc();
        if (!EdgeModules.Active(EdgeModule.Secondaries) || router == null) return;
        int checkKey = CheckRpc.GetStableHashCode(), releaseKey = ReleaseRpc.GetStableHashCode();
        // Refuse both registrations if either name is occupied. Never overwrite a foreign handler.
        if (router.m_functions.ContainsKey(checkKey) || router.m_functions.ContainsKey(releaseKey))
        {
            SecondaryAttackWarningLog.WarnOnce("sub-summon-rpc-collision", "Edge sub-summon transport is unavailable because a routed name is already registered.");
            return;
        }
        router.Register<ZDOID, string>(CheckRpc, CheckHandler);
        _registeredRouter = router;
        _checkRegistration = router.m_functions[checkKey];
        try
        {
            router.Register<ZDOID, string>(ReleaseRpc, ReleaseHandler);
            _releaseRegistration = router.m_functions[releaseKey];
        }
        catch { DisposeRpc(); throw; }
    }

    internal static void DisposeRpc()
    {
        var router = _registeredRouter;
        if (router != null)
        {
            RemoveOwned(router, CheckRpc, _checkRegistration, CheckHandler);
            RemoveOwned(router, ReleaseRpc, _releaseRegistration, ReleaseHandler);
        }
        _registeredRouter = null; _checkRegistration = _releaseRegistration = null;
    }

    private static void RemoveOwned(ZRoutedRpc router, string name, RoutedMethodBase? expected, Action<long, ZDOID, string> handler)
    {
        int key = name.GetStableHashCode();
        if (expected != null && router.m_functions.TryGetValue(key, out var current) &&
            ReferenceEquals(current, expected) && current is RoutedMethod<ZDOID, string> typed &&
            ReferenceEquals(typed.m_action, handler)) router.m_functions.Remove(key);
    }

    private static void CheckDestroyed(long sender, ZDOID parentId, string token)
    {
        if (!EdgeModules.Active(EdgeModule.Secondaries) || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || token == null || token.Length != 32)
            return;
        ZDO? parent = ZDOMan.instance.GetZDO(parentId);
        if (parent == null || parent.GetOwner() != sender || parent.GetString(ParentKey, "").Length == 0 || !HasSlot(parent, token))
            return;
        if (Worlds.GetOrCreateValue(ZDOMan.instance).Destroyed.Contains(token))
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, ReleaseRpc, parentId, token);
    }

    private static void ReleaseSlot(long sender, ZDOID parentId, string token)
    {
        if (!EdgeModules.Active(EdgeModule.Secondaries) || ZNet.instance == null || ZNet.instance.IsServer() || ZNet.instance.GetServerPeer()?.m_uid != sender || ZDOMan.instance == null || token == null || token.Length != 32)
            return;
        ZDO? parent = ZDOMan.instance.GetZDO(parentId);
        if (parent == null || !parent.IsOwner())
            return;
        ClearSlot(parent, token); // compare-and-clear ignores stale replies for replaced slots
    }

    private static bool HasSlot(ZDO parent, string token)
    {
        for (int i = 0; i < Limit; i++)
            if (parent.GetString(Slots[i], "") == token)
                return true;
        return false;
    }

    private static void ClearSlot(ZDO parent, string token)
    {
        for (int i = 0; i < Limit; i++)
            if (parent.GetString(Slots[i], "") == token)
            {
                parent.Set(Slots[i], "");
                parent.Set(References[i], ZDOID.None);
            }
    }

    internal static string BeforeDestroyed(ZDOMan manager, ZDOID id)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return "";
        return manager.GetZDO(id)?.GetString(ChildKey, "") ?? "";
    }

    internal static void AfterDestroyed(ZDOMan manager, ZDOID id, string token)
    {
        if (token.Length != 0 && manager.GetZDO(id) == null)
            Worlds.GetOrCreateValue(manager).Destroyed.Add(token);
    }

    internal static void AfterWorldLoad(ZDOMan manager)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
            return;
        Worlds.Remove(manager);
        // All saved ZDOs are available here, before clients can spawn anything.
        // Reconcile orphans at load only; runtime absence is not proof of death.
        // Rebuild the two lookup references from GUIDs after ZDOID reassignment.
        // Full string-data enumeration happens only at world load, never at cast time.
        Dictionary<string, ZDOID> children = new(StringComparer.Ordinal);
        foreach (ZDOID id in ZDOExtraData.GetAllZDOIDsWithHash(ZDOExtraData.Type.String, ChildKey))
        {
            string token = manager.GetZDO(id)?.GetString(ChildKey, "") ?? "";
            if (token.Length != 0)
                children[token] = id;
        }
        foreach (ZDOID id in ZDOExtraData.GetAllZDOIDsWithHash(ZDOExtraData.Type.String, ParentKey))
        {
            ZDO? parent = manager.GetZDO(id);
            if (parent == null)
                continue;
            for (int slot = 0; slot < Limit; slot++)
            {
                string token = parent.GetString(Slots[slot], "");
                if (token.Length != 0 && children.TryGetValue(token, out ZDOID childId))
                    parent.Set(References[slot], childId);
                else
                {
                    parent.Set(Slots[slot], "");
                    parent.Set(References[slot], ZDOID.None);
                }
            }
        }
    }
}

[HarmonyPatch(typeof(SpawnAbility), "Spawn", MethodType.Enumerator)]
internal static class SpawnAbilitySubSummonPatch
{
    // CombatMeter must observe the vanilla creation site before we replace it.
    [HarmonyAfter("Likhtenvald.CombatMeter")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
    {
        List<CodeInstruction> codes = instructions.ToList();
        MethodInfo instantiate = typeof(Object).GetMethods().Single(m => m.Name == "Instantiate" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 3 && m.GetParameters()[1].ParameterType == typeof(Vector3) && m.GetParameters()[2].ParameterType == typeof(Quaternion))
            .MakeGenericMethod(typeof(GameObject));
        int at = codes.FindIndex(c => c.Calls(instantiate));
        int error = codes.FindIndex(c => c.opcode == OpCodes.Ldstr && c.operand is string s && s.Contains("has null prefab, skipping spawn"));
        int skip = error < 0 ? -1 : codes.FindIndex(error, c => c.opcode == OpCodes.Br || c.opcode == OpCodes.Br_S);
        FieldInfo? ability = AccessTools.Field(original.DeclaringType, "<>4__this");
        if (at < 0 || at + 1 >= codes.Count || codes.Count(c => c.Calls(instantiate)) != 1 || skip < 0 || skip >= at || ability?.FieldType != typeof(SpawnAbility) ||
            !(codes[skip].operand is Label continueLabel))
            throw new InvalidOperationException("SecondaryAttacks: SpawnAbility sub-summon guard could not verify the vanilla instantiate/continue path.");

        int continuation = codes.FindIndex(c => ((System.Collections.IList)AccessTools.Field(typeof(CodeInstruction), "labels").GetValue(c)).Contains(continueLabel));
        if (continuation <= at || codes.Count(c => ((System.Collections.IList)AccessTools.Field(typeof(CodeInstruction), "labels").GetValue(c)).Contains(continueLabel)) != 1 ||
            !codes.Skip(continuation).Take(4).Any(c => c.opcode == OpCodes.Add) || codes.Any(c => c.blocks.Count != 0))
            throw new InvalidOperationException("SecondaryAttacks: SpawnAbility sub-summon guard could not verify the loop continuation outside exception regions.");

        // Keep vanilla's original per-iteration continue target and final cleanup.
        // A denied spawn never creates an object or executes its post-spawn effects.
        var load = new CodeInstruction(OpCodes.Ldarg_0);
        codes[at].MoveLabelsTo(load);
        load.blocks.AddRange(codes[at].blocks);
        codes[at].blocks.Clear();
        codes.InsertRange(at, new[] { load, new CodeInstruction(OpCodes.Ldfld, ability) });
        at += 2;
        codes[at].operand = AccessTools.DeclaredMethod(typeof(SubSummonSystem), nameof(SubSummonSystem.Instantiate));
        // Preserve the successful result for any post-creation observers and
        // vanilla's store. Denial must skip both with an empty evaluation stack.
        Label created = generator.DefineLabel();
        ((System.Collections.IList)AccessTools.Field(typeof(CodeInstruction), "labels").GetValue(codes[at + 1])).Add(created);
        codes.InsertRange(at + 1, new[]
        {
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brtrue, created),
            new CodeInstruction(OpCodes.Pop),
            new CodeInstruction(OpCodes.Br, continueLabel)
        });
        return codes;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.GetFaction))]
internal static class CharacterSubSummonFactionPatch
{
    private static void Postfix(Character __instance, ZNetView ___m_nview, ref Character.Faction __result) => SubSummonSystem.RestoreFaction(__instance, ___m_nview, ref __result);
}

[HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, typeof(bool))]
internal static class SubSummonRpcPatch
{
    private static void Postfix(ZRoutedRpc __instance) => SubSummonSystem.RegisterRpc(__instance);
}

[HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
internal static class SubSummonDestroyedPatch
{
    private static void Prefix(ZDOMan __instance, ZDOID uid, out string __state) => __state = SubSummonSystem.BeforeDestroyed(__instance, uid);
    private static void Postfix(ZDOMan __instance, ZDOID uid, string __state) => SubSummonSystem.AfterDestroyed(__instance, uid, __state);
}

[HarmonyPatch]
internal static class SubSummonWorldLoadPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(ZDOMan)).Where(m => m.Name == "Load" || m.Name == "LoadChunks");
    private static void Postfix(ZDOMan __instance) => SubSummonSystem.AfterWorldLoad(__instance);
}
