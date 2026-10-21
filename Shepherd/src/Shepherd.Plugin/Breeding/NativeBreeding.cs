// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Integration;
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Breeding
{
    /// <summary>
    /// Owns the breeding patches (S07b). Installed while the product is active and <c>Breeding Enabled</c> is on; pace
    /// and growth edits also need the Husbandry module (they scale with the keeper's level). Every edit runs on the
    /// pregnant parent's, young's or egg's ZDO owner, as the game's own breeding does.
    /// </summary>
    public static class NativeBreeding
    {
        public const string HarmonyId = Plugin.Guid + ".breeding";

        private static Harmony? harmony;
        private static bool installed;
        private static MeadworksLog? log;

        /// <summary>The Breeding module runs: inheritance, twins, pace, claims, carries and lineage.</summary>
        public static bool Active => Installed && ShepherdSettings.BreedingEnabled.Value;
        public static bool InheritanceActive => Active && !ShepherdSources.InheritanceOwnedElsewhere;
        public static bool GrowthActive => Installed && (ShepherdSettings.BreedingEnabled.Value || ShepherdSettings.InstantGrowth.Value);

        /// <summary>The Herd module runs (S08): keeper Herd Size bonus, Strict Herd Limit and the herd room hover.</summary>
        public static bool HerdOn => Installed && ShepherdSettings.HerdEnabled.Value;

        /// <summary>The Procreate patches are in place: Breeding, Herd or the Unlimited Herd cheat wants them.</summary>
        public static bool Installed => installed && Wanted;

        private static bool Wanted => harmony != null && Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out _)
            && ShepherdSettings.Enabled.Value && (ShepherdSettings.BreedingEnabled.Value || ShepherdSettings.HerdEnabled.Value
                || ShepherdSettings.UnlimitedHerd.Value || ShepherdSettings.InstantGrowth.Value);

        /// <summary>Pace and growth speed (Husbandry perks) apply.</summary>
        public static bool PaceActive => Active && NativeHusbandry.Active;

        /// <summary><paramref name="stars"/> is Wildblood's answer. False keeps the mother's level and adds no bonus star.</summary>
        public static InheritanceSettings Inheritance(bool stars) => new InheritanceSettings(ShepherdSettings.Inheritance.Value,
            ShepherdSettings.MaxAboveBestParent.Value, HusbandryBreedingSettings.MaxOffspringLevel.Value,
            NativeHusbandry.Active ? HusbandryBreedingSettings.BetterOffspring.Value : 0, ShepherdSettings.BirthStarChance.Value,
            stars);

        public static void Initialize(MeadworksLog logger)
        {
            Shutdown();
            log = logger;
            harmony = new Harmony(HarmonyId);
        }

        public static void Tick()
        {
            if (harmony == null) return;
            bool wanted = Wanted;
            if (wanted == installed) return;
            if (!wanted)
            {
                harmony.UnpatchSelf();
                installed = false;
                BreedingHooks.Clear();
                HerdHooks.Clear();
                return;
            }
            try
            {
                foreach (Type root in new[] { typeof(BreedingHooks), typeof(HerdHooks) })
                    foreach (Type patch in PatchTypes(root)) harmony.CreateClassProcessor(patch).Patch();
                installed = true;
            }
            catch
            {
                harmony.UnpatchSelf();
                installed = false;
                throw;
            }
        }

        private static IEnumerable<Type> PatchTypes(Type root)
        {
            if (root.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0) yield return root;
            foreach (Type child in root.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (Type patch in PatchTypes(child)) yield return patch;
        }

        public static void Shutdown()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            installed = false;
            BreedingHooks.Clear();
            HerdHooks.Clear();
            log = null;
        }

        public static void Verbose(string message) => log?.Verbose(message);
        public static void Run(string context, Action action) { try { action(); } catch (Exception error) { Report(error, context); } }
        public static bool Run<T>(string context, Func<T, bool> action, T argument) { try { return action(argument); } catch (Exception error) { Report(error, context); return false; } }
        public static void Run<T>(string context, Action<T> action, T argument) { try { action(argument); } catch (Exception error) { Report(error, context); } }
        public static TResult Run<T, TResult>(string context, Func<T, TResult> action, T argument, TResult fallback) { try { return action(argument); } catch (Exception error) { Report(error, context); return fallback; } }
        public static void Report(Exception error, string context) => log?.Error(context + ": " + error.GetType().Name + ": " + error.Message);
    }

    /// <summary>A Shepherd key store over a ZDO (owner writes only).</summary>
    public sealed class ZdoKeyStore : IKeyStore
    {
        private readonly ZDO zdo;
        public ZdoKeyStore(ZDO zdo) { this.zdo = zdo ?? throw new ArgumentNullException(nameof(zdo)); }
        public string GetString(string key) => zdo.GetString(key, "");
        public int GetInt(string key) => zdo.GetInt(key, 0);
        public long GetLong(string key) => zdo.GetLong(key, 0L);
        public bool GetBool(string key) => zdo.GetBool(key, false);
        public void Set(string key, string value) => zdo.Set(key, value);
        public void Set(string key, int value) => zdo.Set(key, value);
        public void Set(string key, long value) => zdo.Set(key, value);
        public void Set(string key, bool value) => zdo.Set(key, value);
    }
}
