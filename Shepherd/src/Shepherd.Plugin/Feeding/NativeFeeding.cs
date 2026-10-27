// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    /// <summary>
    /// Owns the trough and feeding patches (S04, S05). Two groups:
    /// <list type="bullet">
    /// <item>Pieces, while Shepherd is loaded: trough prefabs, the grindstone_feeder alias and the Build Level gate, so
    /// troughs already built always load (a world never loses them because a setting is off).</item>
    /// <item>Feeding, while the product is active (Crucible authority, <c>Enabled</c>): container tracking, the feed-only
    /// filter, the owner-routed take handlers and trough eating. Eating itself also follows <c>Feeding Enabled</c>.</item>
    /// </list>
    /// Server-authoritative values come through Crucible (C01/C02); a blocked option reads its safe value.
    /// </summary>
    public static class NativeFeeding
    {
        public const string PiecesHarmonyId = Plugin.Guid + ".troughs";
        public const string FeedingHarmonyId = Plugin.Guid + ".feeding";

        private static Harmony? pieces, feeding;
        private static bool installed;
        // A failed install waits for the module to be switched off and on (or a restart) instead of re-patching every frame.
        private static bool installFailed;
        private static MeadworksLog? log;
        private static string disallowFeedText = "\u0000", disallowAnimalsText = "\u0000";
        private static IReadOnlyCollection<string> disallowFeed = Array.Empty<string>(), disallowAnimals = Array.Empty<string>();
        private static float nextRecipeCheck;

        /// <summary>The product is running here (registered, authority allows it, master switch on).</summary>
        public static bool ProductActive => feeding != null && Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value;

        /// <summary>Animals look for trough (and container) food.</summary>
        public static bool FeedingActive => installed && ProductActive && ShepherdSettings.FeedingEnabled.Value && !ShepherdSources.FeedingOwnedElsewhere;

        public static IReadOnlyCollection<string> DisallowedFeed
        {
            get
            {
                string text = ShepherdSettings.DisallowFeed.Value ?? "";
                if (text != disallowFeedText) { disallowFeed = NameList.Parse(text); disallowFeedText = text; }
                return disallowFeed;
            }
        }

        public static bool FoodDisallowed(string itemPrefab) => FeedingRules.IsListed(DisallowedFeed, itemPrefab);

        public static bool AnimalDisallowed(string creaturePrefab)
        {
            string text = ShepherdSettings.DisallowAnimals.Value ?? "";
            if (text != disallowAnimalsText) { disallowAnimals = NameList.Parse(text); disallowAnimalsText = text; }
            return FeedingRules.IsListed(disallowAnimals, creaturePrefab);
        }

        public static void Initialize(MeadworksLog logger)
        {
            Shutdown();
            log = logger;
            pieces = new Harmony(PiecesHarmonyId);
            feeding = new Harmony(FeedingHarmonyId);
            Patch(pieces, typeof(TroughPieces));
            TroughPieces.Install();
        }

        public static void Tick()
        {
            if (feeding == null) return;
            if (Time.realtimeSinceStartup >= nextRecipeCheck)
            {
                nextRecipeCheck = Time.realtimeSinceStartup + 2f;
                TroughPieces.ApplyRecipes();
            }
            bool wanted = ProductActive;
            if (!wanted) installFailed = false;
            if (wanted != installed && !(wanted && installFailed))
            {
                if (!wanted)
                {
                    feeding.UnpatchSelf();
                    installed = false;
                    TakeTransport.Release();
                    TroughFoodVisuals.Clear();
                    return;
                }
                try
                {
                    Patch(feeding, typeof(FeedSources));
                    Patch(feeding, typeof(TroughEating));
                    Patch(feeding, typeof(FeedProtection));
                    Patch(feeding, typeof(TroughFoodVisuals));
                    installed = true;
                    FeedSources.TrackLoaded();
                    TroughFoodVisuals.TrackLoaded();
                    TroughEating.TrackLoaded();
                }
                catch
                {
                    feeding.UnpatchSelf();
                    installed = false;
                    installFailed = true;
                    TakeTransport.Release();
                    throw;
                }
            }
            if (installed) TakeTransport.Expire();
        }

        private static void Patch(Harmony harmony, Type root)
        {
            foreach (Type patch in PatchTypes(root)) harmony.CreateClassProcessor(patch).Patch();
        }

        private static IEnumerable<Type> PatchTypes(Type root)
        {
            if (root.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0) yield return root;
            foreach (Type child in root.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (Type patch in PatchTypes(child)) yield return patch;
        }

        public static void Shutdown()
        {
            feeding?.UnpatchSelf();
            pieces?.UnpatchSelf();
            feeding = pieces = null;
            installed = installFailed = false;
            TakeTransport.Release();
            TroughFoodVisuals.Clear();
            log = null;
        }

        public static void Warn(string message) => log?.Warning(message);
        public static void Verbose(string message) => log?.Verbose(message);
        public static void Run(string context, Action action) { try { action(); } catch (Exception error) { Report(error, context); } }
        public static void Run<T>(string context, Action<T> action, T argument) { try { action(argument); } catch (Exception error) { Report(error, context); } }
        public static TResult Run<T, TResult>(string context, Func<T, TResult> action, T argument, TResult fallback) { try { return action(argument); } catch (Exception error) { Report(error, context); return fallback; } }
        public static void Report(Exception error, string context) => log?.Error(context + ": " + error.GetType().Name + ": " + error.Message);
    }
}
