// Larder - Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Lifecycle adapter for the directly adopted GrindstoneSkills source in this directory.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.OriginalArt;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    public static class NativeCookingFeatures
    {
        private static Harmony data;
        private static bool dataOn, uiOn;
        private static MeadworksLog log;
        private static IDisposable experience, page, icon;
        private static Sprite cookingIcon;
        private static readonly Type[] dataRoots = { typeof(StationXp), typeof(CraftXp), typeof(FermenterXp) };
        public static bool Active => data != null && NativeCooking.Active;

        public static void Initialize(MeadworksLog logger)
        {
            Shutdown();
            log = logger;
            data = new Harmony("com.meadworks.larder.cooking.features");
            experience = MeadworksSkills.RegisterExperience("com.meadworks.larder", CookLevel.Skill, XpScaling.Apply);
            page = MeadworksSkills.RegisterPage("com.meadworks.larder", CookLevel.Skill, (player, level) =>
            {
                if (!NativeCooking.Active) return Array.Empty<SkillPageLine>();
                var content = new SkillPage(player, level);
                CookingPage.Write(content);
                return content.Lines();
            });
            Tick();
        }

        public static void Tick()
        {
            if (data == null) return;
            bool active = NativeCooking.Active;
            Switch(data, ref dataOn, active, dataRoots);
            uiOn = active && MeadworksHeadless.AllowsUi;
            if (uiOn && icon == null) icon = MeadworksSkills.RegisterIcon("com.meadworks.larder", CookLevel.Skill, Icon);
            if (!uiOn && icon != null) { icon.Dispose(); icon = null; }
        }

        private static void Switch(Harmony harmony, ref bool installed, bool wanted, Type[] roots)
        {
            if (installed == wanted) return;
            harmony.UnpatchSelf();
            installed = false;
            if (!wanted) return;
            try
            {
                foreach (Type root in roots)
                    foreach (Type type in PatchTypes(root)) harmony.CreateClassProcessor(type).Patch();
                installed = true;
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                Warn("cooking patches", error);
            }
        }

        private static IEnumerable<Type> PatchTypes(Type root)
        {
            if (root.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0) yield return root;
            foreach (Type child in root.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (Type patch in PatchTypes(child)) yield return patch;
        }

        internal static void Warn(string action, Exception error) => log?.Warning(action + ": " + error.GetBaseException().Message);

        private static Sprite Icon()
        {
            if (!NativeCooking.Active || !MeadworksHeadless.AllowsUi) return null;
            if (cookingIcon != null) return cookingIcon;
            try { cookingIcon = OriginalItemArt.Icon("larder_cooking"); }
            catch (Exception error) { Warn("Cooking icon art", error); }
            return cookingIcon;
        }

        public static void Shutdown()
        {
            data?.UnpatchSelf();
            data = null;
            dataOn = uiOn = false;
            experience?.Dispose(); page?.Dispose(); icon?.Dispose();
            experience = page = icon = null;
            XpScope.End(true);
            cookingIcon = null;
            OriginalItemArt.Shutdown();
            log = null;
        }
    }
}
