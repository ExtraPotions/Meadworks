using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;

namespace Mantle.Core
{
    /// <summary>
    /// Epic Loot's magic effects on a player, through the public API it publishes for other mods (<c>EpicLoot.API</c>),
    /// found by reflection on first use: Mantle neither references nor needs Epic Loot. The active effects
    /// (<c>GetAllActiveMagicEffects</c>: every equipped magic item's effects and every set bonus, as JSON), each type
    /// once with Epic Loot's own total (<c>GetTotalPlayerActiveMagicEffectValue</c>), written with Epic Loot's own words
    /// (<c>GetMagicItemEffectDefinition</c>'s DisplayText, localized, the total in its {0}). Epic Loot lists the
    /// inventory's equipped items (<c>Inventory.GetEquippedItems</c>), so Mantle's extra utilities count with no
    /// provider of ours. Absent, missing a method or failing: no lines, one warning in the log.
    /// </summary>
    public static class EpicLootLink
    {
        private const string Guid = "randyknapp.mods.epicloot";
        private static readonly Regex TypeField = new Regex("\"EffectType\"\\s*:\\s*\"([^\"]+)\"");
        private static readonly Regex DisplayField = new Regex("\"DisplayText\"\\s*:\\s*\"([^\"]*)\"");
        private static readonly Dictionary<string, string> displayTexts = new Dictionary<string, string>();
        private static bool looked;
        private static MethodInfo activeEffects;
        private static MethodInfo total;
        private static MethodInfo definition;

        public static bool Present
        {
            get
            {
                Bind();
                return activeEffects != null;
            }
        }

        /// <summary>Every active effect type once, in the order Epic Loot lists them; false without Epic Loot.</summary>
        public static bool ActiveTypes(Player player, List<string> into)
        {
            into.Clear();
            if (player == null || !Present)
                return false;
            IEnumerable effects = Call(activeEffects, player, null) as IEnumerable;
            if (effects == null)
                return false;
            foreach (object effect in effects)
            {
                Match match = TypeField.Match(effect as string ?? "");
                if (match.Success && !into.Contains(match.Groups[1].Value))
                    into.Add(match.Groups[1].Value);
            }
            return true;
        }

        /// <summary>Epic Loot's total of one effect type on the player.</summary>
        public static float Total(Player player, string type) =>
            Call(total, player, type, 1f, null) is float value ? value : 0f;

        /// <summary>The effect as Epic Loot words it, with the total in it; the type's name when it has no words.</summary>
        public static string Line(string type, float value)
        {
            string format = Language.Localize(DisplayText(type));
            if (string.IsNullOrEmpty(format) || format.StartsWith("["))
                return Spaced(type) + " " + value.ToString("0.#");
            try
            {
                return string.Format(format, value);
            }
            catch (FormatException)
            {
                return format + " " + value.ToString("0.#");
            }
        }

        private static string DisplayText(string type)
        {
            if (displayTexts.TryGetValue(type, out string text))
                return text;
            Match match = DisplayField.Match(Call(definition, type) as string ?? "");
            text = match.Success ? match.Groups[1].Value : "";
            displayTexts[type] = text;
            return text;
        }

        /// <summary>"AddCrafterSkills" as "Add Crafter Skills".</summary>
        private static string Spaced(string type) => Regex.Replace(type, "(?<=[a-z])(?=[A-Z])", " ");

        private static object Call(MethodInfo method, params object[] args)
        {
            if (method == null)
                return null;
            try
            {
                return method.Invoke(null, args);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Epic Loot's {method.Name} failed, its effects are left out of the stats panel: {e.InnerException?.Message ?? e.Message}");
                activeEffects = null;
                return null;
            }
        }

        private static void Bind()
        {
            if (looked)
                return;
            looked = true;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out BepInEx.PluginInfo info) || info.Instance == null)
                return;
            Type api = info.Instance.GetType().Assembly.GetType("EpicLoot.API");
            activeEffects = api?.GetMethod("GetAllActiveMagicEffects", new[] { typeof(Player), typeof(string) });
            total = api?.GetMethod("GetTotalPlayerActiveMagicEffectValue", new[] { typeof(Player), typeof(string), typeof(float), typeof(ItemDrop.ItemData) });
            definition = api?.GetMethod("GetMagicItemEffectDefinition", new[] { typeof(string) });
            if (activeEffects == null || total == null || definition == null)
            {
                Plugin.Log.LogWarning("Epic Loot is installed but its API lacks a method Mantle uses; its effects are left out of the stats panel");
                activeEffects = null;
            }
        }
    }
}
