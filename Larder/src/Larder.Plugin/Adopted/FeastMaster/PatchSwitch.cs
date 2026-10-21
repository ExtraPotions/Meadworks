// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/PatchSwitch.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Installs a patch class only while a setting it serves differs from its default, so FeastMaster at its
    /// defaults leaves the game and other mods untouched. A class's rule is its static <c>Prepare()</c>, the method
    /// Harmony itself asks before patching; a class without one is always installed. Every setting change (an edit,
    /// a file reload, a server push) marks the rules for a fresh look, done once on the next frame on the main
    /// thread: classes whose rule turned true are patched and their optional <c>Installed()</c> runs, classes whose
    /// rule turned false are unpatched and their optional <c>Removed()</c> puts back what they changed. The flags in
    /// <see cref="ChangedRules"/> are worked out first, so the rules and the hot hooks read the same answers.
    /// </summary>
    public static class PatchSwitch
    {
        private const string RuleMethod = "Prepare";
        private const string InstalledHook = "Installed";
        private const string RemovedHook = "Removed";
        private const BindingFlags OwnStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly List<Type> classes = new List<Type>();
        private static readonly HashSet<Type> installed = new HashSet<Type>();
        private static readonly HashSet<Type> failed = new HashSet<Type>();
        private static Harmony harmony;
        private static bool dirty;

        /// <summary>Finds the patch classes of the assembly and installs those wanted now; returns the failures.</summary>
        public static int Initialize(Harmony instance, Assembly assembly)
        {
            harmony = instance;
            foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (type.Namespace == typeof(PatchSwitch).Namespace && type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                    classes.Add(type);
            }
            Refresh(log: true);
            return failed.Count;
        }

        public static void AddPatchTypes(IEnumerable<Type> types)
        {
            foreach (Type type in types) if (!classes.Contains(type)) classes.Add(type);
            MarkDirty();
        }
        public static void Shutdown()
        {
            foreach (Type type in installed.ToArray()) Remove(type);
            classes.Clear(); installed.Clear(); failed.Clear(); harmony = null; dirty = false;
        }
        public static void MarkDirty() => dirty = true;

        /// <summary>Called every frame by <see cref="SwitchTicker"/>: one look at the rules after any number of setting changes.</summary>
        public static void Tick()
        {
            if (!dirty || harmony == null)
                return;
            dirty = false;
            Refresh(log: false);
        }

        private static void Refresh(bool log)
        {
            ChangedRules.Refresh();
            foreach (Type type in classes)
            {
                if (failed.Contains(type))
                    continue;
                bool wanted = Wanted(type);
                if (wanted == installed.Contains(type))
                    continue;
                log = true;
                if (wanted)
                    Install(type);
                else
                    Remove(type);
            }
            if (log)
                LogActive();
        }

        private static bool Wanted(Type type)
        {
            if (!NativeFeastMaster.AllowsPatch(type)) return false;
            MethodInfo rule = OwnMethod(type, RuleMethod);
            if (rule == null)
                return true;
            try
            {
                return (bool)rule.Invoke(null, null);
            }
            catch (Exception e)
            {
                NativeFeastMaster.Log.Error($"{type.Name}.{RuleMethod} failed, the patch stays off: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        private static void Install(Type type)
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                installed.Add(type);
            }
            catch (Exception e)
            {
                failed.Add(type);
                NativeFeastMaster.Log.Error($"Patch {type.FullName} failed to apply: {e.Message}");
                return;
            }
            RunHook(type, InstalledHook);
        }

        /// <summary>Takes out every prefix, postfix, finalizer (and the rest) this class added, on every method.</summary>
        private static void Remove(Type type)
        {
            foreach (MethodBase original in harmony.GetPatchedMethods().ToList())
            {
                foreach (Patch patch in PatchesOf(original))
                {
                    if (patch.owner == harmony.Id && patch.PatchMethod.DeclaringType == type)
                        harmony.Unpatch(original, patch.PatchMethod);
                }
            }
            installed.Remove(type);
            RunHook(type, RemovedHook);
        }

        private static List<Patch> PatchesOf(MethodBase original)
        {
            Patches info = Harmony.GetPatchInfo(original);
            if (info == null)
                return new List<Patch>();
            return info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers)
                .Concat(info.Finalizers).Concat(info.ILManipulators).ToList();
        }

        /// <summary>The class's own parameterless static method, or null. Plain reflection: AccessTools logs a warning for every miss.</summary>
        private static MethodInfo OwnMethod(Type type, string name) => type.GetMethod(name, OwnStatic, null, Type.EmptyTypes, null);

        private static void RunHook(Type type, string name)
        {
            MethodInfo hook = OwnMethod(type, name);
            if (hook == null)
                return;
            try
            {
                hook.Invoke(null, null);
            }
            catch (Exception e)
            {
                NativeFeastMaster.Log.Error($"{type.Name}.{name} failed: {(e.InnerException ?? e).Message}");
            }
        }

        /// <summary>Names the patches that changed settings installed; the always-on ones (config binding) are left out.</summary>
        private static void LogActive()
        {
            List<string> names = classes
                .Where(t => installed.Contains(t) && OwnMethod(t, RuleMethod) != null)
                .Select(t => t.Name)
                .ToList();
            NativeFeastMaster.Log.Verbose(names.Count == 0
                ? "Every setting is at its default: no gameplay patches installed."
                : $"Patches installed for changed settings: {string.Join(", ", names)}");
        }
    }

}
