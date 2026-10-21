// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// Makes the Rune Table's window as a copy of the game's own crafting panel (rune-table.md section 6), so it has the
    /// game's look and whatever look a UI mod gave it (PackPanel's timber included): its background, title, one tab,
    /// the recipe list, the description panel with its icon, name, text, requirement slots and craft button, and the
    /// small "Style" button. Every other part (other mods' additions, the station level, upgrade and repair parts) is
    /// removed, and so is every component that would tie the copy to the game's crafting panel: UI groups, gamepad
    /// hooks, hold triggers, localizers that would rewrite its words, layout groups another mod put on the list; a UI
    /// mod's skin is shed so that mod skins the copy itself (<see cref="ShedSkins"/>). Copied under an inactive holder,
    /// so none of its components wake before the stripping.
    /// </summary>
    internal static class PanelBuilder
    {
        private static readonly string[] Keep =
        {
            "Darken", "Bkg", "topic", "TabsButtons/Craft", "TabsButtons/TabBorder", "RecipeList/Recipes",
            "RecipeList/RecipeScroll", "Decription/Icon", "Decription/Name", "Decription/Description",
            "Decription/requirements", "Decription/craft_button_panel/CraftButton", "Decription/SelectVariant",
        };

        // PackPanel's timber background, a child of each wood panel image it skins.
        private const string PackPanelSkin = "PackPanel_timberwood";

        private static readonly HashSet<string> Foreign = new HashSet<string>
        {
            "UIGroupHandler", "UIGamePad", "UIInputHint", "EventTrigger", "Localize", "UIInputHandler",
            "GridLayoutGroup", "VerticalLayoutGroup", "HorizontalLayoutGroup", "ContentSizeFitter",
        };

        public static GameObject Build(RectTransform crafting)
        {
            var holder = new GameObject("ECF_RuneTableHolder");
            holder.SetActive(false);
            GameObject copy = Object.Instantiate(crafting.gameObject, holder.transform, false);
            copy.name = "ECF_RuneTablePanel";
            EnabledColours(crafting.gameObject, copy);
            Prune(copy.transform, "");
            ClearList(copy.transform.Find("RecipeList/Recipes"));
            StripForeign(copy);
            ShedSkins(copy);
            copy.SetActive(false);
            copy.transform.SetParent(crafting.parent, false);
            Object.Destroy(holder);
            return copy;
        }

        // The copied buttons' labels in their enabled colours, before the copy wakes: the game's ButtonTextColor keeps the
        // colour its label has when it wakes as the enabled one, and the crafting panel's own buttons may be greyed right
        // now (no recipe craftable), which left the table's Inscribe, Sacrifice and Set gem grey even when usable. The
        // two hierarchies are still identical here, so the parts pair by order.
        private static void EnabledColours(GameObject source, GameObject copy)
        {
            ButtonTextColor[] from = source.GetComponentsInChildren<ButtonTextColor>(true);
            ButtonTextColor[] to = copy.GetComponentsInChildren<ButtonTextColor>(true);
            for (int i = 0; i < from.Length && i < to.Length; i++)
            {
                TMPro.TMP_Text? text = to[i].GetComponentInChildren<TMPro.TMP_Text>(true);
                if (text != null)
                {
                    text.color = from[i].m_defaultColor;
                }
            }
        }

        // Keeps a part whose path is listed (with all below it) and every part on the way to one; removes the rest.
        private static void Prune(Transform parent, string path)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                string at = path.Length == 0 ? child.name : path + "/" + child.name;
                if (Kept(at, out bool whole))
                {
                    if (!whole)
                    {
                        Prune(child, at);
                    }
                    continue;
                }
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool Kept(string path, out bool whole)
        {
            whole = false;
            foreach (string keep in Keep)
            {
                if (keep == path || path.StartsWith(keep + "/"))
                {
                    whole = true;
                    return true;
                }
            }
            foreach (string keep in Keep)
            {
                if (keep.StartsWith(path + "/"))
                {
                    return true;
                }
            }
            return false;
        }

        // The game's live rows and its row template: the window makes its own rows from the game's template.
        private static void ClearList(Transform? recipes)
        {
            Transform? list = recipes != null && recipes.GetComponent<UnityEngine.UI.ScrollRect>() is { } scroll ? scroll.content : null;
            if (recipes == null)
            {
                return;
            }
            for (int i = recipes.childCount - 1; i >= 0; i--)
            {
                Transform child = recipes.GetChild(i);
                if (child != list)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
            for (int i = list != null ? list.childCount - 1 : -1; i >= 0; i--)
            {
                Object.DestroyImmediate(list!.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// A UI mod's skin does not survive the copy: PackPanel's timber background keeps its art in fields a copy loses,
        /// and it has already made the panel's own image clear. So the dead skin goes and the image gets the game's look
        /// back (opaque, centre filled); PackPanel skins it afresh when it is first shown, as it does every game wood
        /// panel. Found by PackPanel's published object name only; without PackPanel there is nothing to shed.
        /// </summary>
        private static void ShedSkins(GameObject copy)
        {
            foreach (UnityEngine.UI.Image image in copy.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                Transform? skin = image.transform.Find(PackPanelSkin);
                if (skin == null)
                {
                    continue;
                }
                Object.DestroyImmediate(skin.gameObject);
                image.color = Color.white;
                image.fillCenter = true;
                image.material = null;
            }
        }

        private static void StripForeign(GameObject copy)
        {
            foreach (Component part in copy.GetComponentsInChildren<Component>(true))
            {
                if (part != null && Foreign.Contains(part.GetType().Name))
                {
                    Object.DestroyImmediate(part);
                }
            }
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true))
            {
                if (part != null && part.name.StartsWith("gamepad_hint"))
                {
                    Object.DestroyImmediate(part.gameObject);
                }
            }
        }
    }
}
