// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Owns the adopted keeper/taming/calm/petting patches and the yield and companion patches (S09: butcher yield, prime
    /// cuts, produce, extra honey, pack leader, routed husbandry credit); CS1 owns the skill.
    /// </summary>
    public static class NativeHusbandry
    {
        public const string HarmonyId = Plugin.Guid + ".husbandry";
        const string SourceId = Plugin.Guid + ".husbandry";
        static Harmony? harmony;
        static NativeConfiguration? configuration;
        static MeadworksLog? log;
        static bool installed, sourceEnabled;
        static float nextSourceCheck;
        static readonly StepAsideNotices notices = new StepAsideNotices();
        static readonly List<RpcLease> rpcs = new List<RpcLease>();
        public static bool Active => harmony != null && Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value && HusbandrySettings.Enabled.Value && !sourceEnabled;
        public static IReadOnlyList<ConfigEntryBase> Entries => configuration?.Entries ?? Array.Empty<ConfigEntryBase>();
        public static bool SteppedAside => sourceEnabled;

        public static void Initialize(ConfigFile file, MeadworksLog logger)
        {
            Shutdown();
            log = logger;
            configuration = new NativeConfiguration(file);
            ShepherdSettings.Initialize(configuration);
            ShepherdSettings.NoteIgnoredStars(log);
            HusbandrySettings.Initialize(configuration);
            HoneyProduction.Initialize();
            if (ExtraPotions.Meadworks.Crucible.Plugin.Services.StepAside.Declared(SourceId).Count == 0)
                MeadworksStepAside.Declare(SourceId, "Shepherd Husbandry", new[] { new SourceMod("com.GrindstoneSkills", "GrindstoneSkills Husbandry") });
            harmony = new Harmony(HarmonyId);
            nextSourceCheck = 0;
        }

        public static void Tick()
        {
            if (harmony == null) return;
            HoneyProduction.Tick();
            if (Time.realtimeSinceStartup >= nextSourceCheck)
            {
                nextSourceCheck = Time.realtimeSinceStartup + 0.5f;
                sourceEnabled = SourceHusbandryEnabled();
                var decision = ExtraPotions.Meadworks.Crucible.Plugin.Services.StepAside.Decide(SourceId,
                    sourceEnabled ? new[] { "com.GrindstoneSkills" } : Array.Empty<string>());
                if (notices.ShouldShow(decision)) log?.Warning(decision.Notice);
            }
            bool wanted = Active;
            if (wanted == installed) return;
            if (!wanted)
            {
                harmony.UnpatchSelf();
                installed = false;
                ReleaseRpcs();
                LoreHover.Clear();
                HerdCallout.Clear();
                return;
            }
            try
            {
                foreach (Type root in new[] { typeof(TamingSpeed), typeof(FedLonger), typeof(Calm), typeof(CalmProvocation), typeof(Petting), typeof(PetRpcAwake),
                    typeof(HusbandryCredit), typeof(Butchering), typeof(ButcherYield), typeof(ExtraHoney), typeof(Produce),
                    typeof(YieldCatalog), typeof(PackLeader), typeof(LoreHover), typeof(HerdCallout) })
                    foreach (Type patch in PatchTypes(root)) harmony.CreateClassProcessor(patch).Patch();
                installed = true;
                HusbandryCredit.Register();
                YieldCatalog.Discover();
                foreach (Tameable tameable in UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.InstanceID))
                    if (tameable.m_nview != null && tameable.m_nview.IsValid()) { Petting.RegisterOn(tameable.m_nview); HerdCallout.Register(tameable); }
            }
            catch
            {
                harmony.UnpatchSelf();
                installed = false;
                ReleaseRpcs();
                HerdCallout.Clear();
                throw;
            }
        }

        static bool SourceHusbandryEnabled()
        {
            if (!Chainloader.PluginInfos.TryGetValue("com.GrindstoneSkills", out var source)) return false;
            // Unknown config layouts yield conservatively, while the actual pinned master switch supports per-module coexistence.
            return source.Instance == null || !source.Instance.Config.TryGetEntry<bool>("23 - Husbandry", "Husbandry Enabled", out var entry) || entry.Value;
        }

        static IEnumerable<Type> PatchTypes(Type root)
        {
            if (root.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0) yield return root;
            foreach (Type child in root.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (Type patch in PatchTypes(child)) yield return patch;
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
        static class PetRpcAwake
        {
            [HarmonyPostfix]
            static void Postfix(Tameable __instance)
            {
                if (Active && __instance.m_nview != null && __instance.m_nview.IsValid()) Petting.RegisterOn(__instance.m_nview);
            }
        }

        internal static void RegisterPet(ZNetView view, Action<long> receive)
        {
            if (!Active || view == null || !view.IsValid()) return;
            int hash = Keys.RpcPet.GetStableHashCode();
            rpcs.RemoveAll(p => !p.View.TryGetTarget(out var prior) || prior == null);
            // Preserve any foreign registration; an owned handler remains until this patch group stops.
            if (view.m_functions.ContainsKey(hash)) return;
            view.Register(Keys.RpcPet, receive);
            rpcs.Add(new RpcLease(view, hash, view.m_functions[hash]));
        }

        internal static bool ValidPetter(Tameable tameable, long sender)
        {
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || player.IsDead()) continue;
                var view = player.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || view.GetZDO().GetOwner() != sender) continue;
                float reach = 5f + (tameable.m_character != null ? tameable.m_character.GetRadius() : 0f);
                if ((player.transform.position - tameable.transform.position).sqrMagnitude <= reach * reach) return true;
            }
            return false;
        }

        public static bool IsContent(ZNetView view) => Active && HusbandryBreedingSettings.ContentDuration.Value > 0f
            && view != null && view.IsValid()
            && view.GetZDO().GetLong(Keys.ContentUntil) > Herd.Now.Ticks / TimeSpan.TicksPerMillisecond;

        sealed class RpcLease
        {
            internal readonly WeakReference<ZNetView> View;
            internal readonly int Hash;
            internal readonly object Handler;
            internal RpcLease(ZNetView view, int hash, object handler) { View = new WeakReference<ZNetView>(view); Hash = hash; Handler = handler; }
        }
        static void ReleaseRpcs()
        {
            foreach (var rpc in rpcs)
                if (rpc.View.TryGetTarget(out var view) && view != null && view.m_functions.TryGetValue(rpc.Hash, out var handler)
                    && ReferenceEquals(handler, rpc.Handler)) view.m_functions.Remove(rpc.Hash);
            rpcs.Clear();
        }

        public static void Shutdown()
        {
            HoneyProduction.Shutdown();
            harmony?.UnpatchSelf();
            harmony = null;
            installed = sourceEnabled = false;
            ReleaseRpcs();
            LoreHover.Clear();
            HerdCallout.Clear();
            ProduceTables.Clear();
            configuration?.Dispose();
            configuration = null;
            log = null;
        }
        public static void Run(string context, Action action) { try { action(); } catch (Exception error) { Report(error, context); } }
        public static void Run<T>(string context, Action<T> action, T argument) { try { action(argument); } catch (Exception error) { Report(error, context); } }
        public static TResult Run<TResult>(string context, Func<TResult> action, TResult fallback) { try { return action(); } catch (Exception error) { Report(error, context); return fallback; } }
        public static TResult Run<T, TResult>(string context, Func<T, TResult> action, T argument, TResult fallback) { try { return action(argument); } catch (Exception error) { Report(error, context); return fallback; } }
        public static void Verbose(string message) => log?.Verbose(message);
        public static void Report(Exception error, string context) => log?.Error(context + ": " + error.GetType().Name + ": " + error.Message);
    }
}
