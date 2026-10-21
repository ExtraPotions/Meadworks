// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Lifecycle/authority adapter for pinned GrindstoneSkills Cooking; no replacement transport or payment path.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    public static class NativeCooking
    {
        public const string HarmonyId = "com.meadworks.larder.cooking";
        static Func<bool> active;
        static Harmony harmony;
        static bool installed;
        static readonly List<RpcLease> rpcs = new List<RpcLease>();
        public static MeadworksLog Log { get; private set; }
        public static bool Active => active != null && active();

        public static void Initialize(ConfigFile config, MeadworksLog log, Func<bool> isActive)
        {
            Shutdown();
            Log = log ?? throw new ArgumentNullException(nameof(log));
            active = isActive ?? throw new ArgumentNullException(nameof(isActive));
            CookingSettings.Initialize(config);
            harmony = new Harmony(HarmonyId);
            NativeCookingFeatures.Initialize(log);
            Tick();
        }

        public static void Tick()
        {
            if (harmony == null) return;
            bool wanted = Active;
            if (wanted != installed)
            {
                if (wanted)
                {
                    try
                    {
                        foreach (Type type in PatchRoots.Where(root => MeadworksHeadless.AllowsUi ||
                            (root != typeof(KitchenHover) && root != typeof(FermenterHover))).SelectMany(PatchTypes))
                            harmony.CreateClassProcessor(type).Patch();
                        installed = true;
                        KitchenDiscovery.Discover();
                        // A toggle may enable Cooking after native Awake; register only currently loaded native objects.
                        foreach (var station in UnityEngine.Object.FindObjectsOfType<CookingStation>()) StationRpc.RegisterOn(station);
                        foreach (var barrel in UnityEngine.Object.FindObjectsOfType<Fermenter>()) FermenterAddReceive.RegisterOn(barrel);
                    }
                    catch
                    {
                        harmony.UnpatchSelf();
                        installed = false;
                        throw;
                    }
                }
                else
                {
                    harmony.UnpatchSelf();
                    installed = false;
                    KitchenCraft.ClearScope();
                }
                Log.Verbose("Cooking native patch group " + (installed ? "installed" : "removed") + ".");
            }
            NativeCookingFeatures.Tick();
        }

        static readonly Type[] PatchRoots = {
            typeof(KitchenDiscovery), typeof(StationRpc), typeof(StationSlots), typeof(StationPlacement),
            typeof(StationPerks), typeof(StationTakeOff), typeof(KitchenCraft), typeof(CraftRecord),
            typeof(FermenterAddSend), typeof(FermenterAddReceive), typeof(FermenterSpeed), typeof(FermenterTap),
            typeof(FermenterDrop), typeof(FermenterHover), typeof(RpcSendSwap), typeof(KitchenHover)
        };
        static IEnumerable<Type> PatchTypes(Type type)
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0) yield return type;
            foreach (Type child in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (Type patch in PatchTypes(child)) yield return patch;
        }

        internal static void RememberRpc(ZNetView view, string name)
        {
            int hash = name.GetStableHashCode();
            if (view == null || !view.m_functions.TryGetValue(hash, out var handler)) return;
            rpcs.RemoveAll(p => !p.View.TryGetTarget(out var prior) || prior == null);
            if (!rpcs.Any(p => p.View.TryGetTarget(out var prior) && ReferenceEquals(prior, view) && p.Hash == hash))
                rpcs.Add(new RpcLease(view, hash, handler));
        }
        sealed class RpcLease
        {
            internal readonly WeakReference<ZNetView> View;
            internal readonly int Hash;
            internal readonly WeakReference<object> Handler;
            internal RpcLease(ZNetView view, int hash, object handler)
            { View = new WeakReference<ZNetView>(view); Hash = hash; Handler = new WeakReference<object>(handler); }
        }

        public static void Shutdown()
        {
            active = null;
            NativeCookingFeatures.Shutdown();
            harmony?.UnpatchSelf();
            harmony = null;
            installed = false;
            foreach (var rpc in rpcs)
                if (rpc.View.TryGetTarget(out var view) && view != null && view.m_functions.TryGetValue(rpc.Hash, out var handler)
                    && rpc.Handler.TryGetTarget(out var owned) && ReferenceEquals(handler, owned)) view.m_functions.Remove(rpc.Hash);
            rpcs.Clear();
            KitchenCraft.ClearScope();
            Kitchen.Clear();
            CookingSettings.Shutdown();
            Log = null;
        }

        public static void Report(string message) => Log?.Verbose(message);
        public static void Run(string context, Action action)
        {
            try { action(); }
            catch (Exception error) { Log?.Error(context + ": " + error.GetType().Name + ": " + error.Message); }
        }
    }
}
