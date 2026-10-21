// Adopted from OpenKeep 2.0.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: OpenKeep/OpenKeep/src/Capacity/HoverText.cs at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026-10-10 (parity choices #19 and #59): the access check is shared with the chest preview panel,
// and while that panel shows the container the hover text leaves its fill and content lines out.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Capacity
{
    /// <summary>
    /// "Hover Contents": Container.GetHoverText() gets a fill line and up to Hover Lines content lines appended,
    /// only for a container the local player could open: the game's own guard stone check
    /// (PrivateArea.CheckAccess when the container checks guard stones) and privacy check (Container.CheckAccess)
    /// are run, and the net view must be valid. The chest preview panel is the contents display; these lines are its
    /// text fallback and appear only while Chest Preview is off.
    /// </summary>
    public static class HoverText
    {
        public static string Append(Container container, string text)
        {
            if (!CapacitySettings.HoverContents.Value || PreviewSettings.Enabled.Value || !CanSee(container) || ChestPreview.Shows(container))
                return text;
            Inventory inventory = container.GetInventory();
            StringBuilder extra = new StringBuilder();
            string fill = FillLine(inventory);
            if (fill != null)
                extra.Append('\n').Append(fill);
            foreach (string line in ContentLines(inventory, CapacitySettings.HoverLines.Value))
                extra.Append('\n').Append(line);
            return extra.Length == 0 ? text : text + Language.Localize(extra.ToString());
        }

        /// <summary>The local player could open the container (guard stone and privacy checks); loot chests pass too.</summary>
        internal static bool CanSee(Container container)
        {
            if (container == null || container.GetInventory() == null || container.m_nview == null || !container.m_nview.IsValid())
                return false;
            if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, false))
                return false;
            if (Game.instance == null || Game.instance.GetPlayerProfile() == null)
                return false;
            return container.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID());
        }

        private static string FillLine(Inventory inventory)
        {
            int used = inventory.NrOfItems();
            int total = Mathf.Max(1, inventory.GetWidth() * inventory.GetHeight());
            switch (CapacitySettings.HoverFill.Value)
            {
                case HoverFill.Fraction: return $"{used} / {total} $burrow_slots";
                case HoverFill.Percent: return $"{Mathf.RoundToInt(100f * used / total)}% $burrow_full";
                default: return null;
            }
        }

        /// <summary>Stacks of the same item summed, most first; "name x count" lines, then "and n more".</summary>
        public static List<string> ContentLines(Inventory inventory, int maxLines)
        {
            List<KeyValuePair<string, int>> counts = CountByName(inventory).OrderByDescending(pair => pair.Value).ToList();
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts.Take(Mathf.Max(0, maxLines)))
                lines.Add($"{pair.Key} x {pair.Value}");
            int more = counts.Count - lines.Count;
            if (more > 0)
                lines.Add($"$burrow_and {more} $burrow_more");
            return lines;
        }

        private static Dictionary<string, int> CountByName(Inventory inventory)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == null || item.m_shared == null)
                    continue;
                string name = string.IsNullOrEmpty(item.m_shared.m_name) ? "?" : item.m_shared.m_name;
                counts.TryGetValue(name, out int count);
                counts[name] = count + item.m_stack;
            }
            return counts;
        }

        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        private static class HoverPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Container __instance, ref string __result) => __result = Append(__instance, __result);
        }
    }
}
