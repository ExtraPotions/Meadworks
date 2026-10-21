// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// Builds the recap window from a copy of the game's own compendium window, so it wears the game's wood frame, list,
    /// buttons and fonts, and any mod that reskins the game's wood panels (PackPanel's Timber theme) reskins it the same
    /// way. The copy is made under an inactive holder, so the game's dialog script never wakes in it, and that script is
    /// removed before the copy is shown. It lives on a canvas of its own beside the game's screens, sorted just above the
    /// inventory and scaled by the game's own GUI scale. The left list and the right area are kept and placed anew; the
    /// video, timeline and controls are added by <see cref="PaneBuilder"/>.
    /// </summary>
    internal static class WindowBuilder
    {
        public const float Width = 1280f;
        public const float Height = 820f;
        private const int AboveInventory = 50;

        public static WindowView? Build()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_textsDialog == null || gui.m_splitDialog == null)
            {
                return null;
            }
            RectTransform root = CanvasRoot(gui);
            WindowParts? parts = Copy(gui.m_textsDialog, root.gameObject);
            if (parts == null)
            {
                Object.Destroy(root.gameObject);
                return null;
            }
            Arrange(parts);
            WindowView view = root.gameObject.AddComponent<WindowView>();
            view.Init(parts, gui.m_splitDialog.m_splitSlider);
            root.gameObject.SetActive(false);
            return view;
        }

        private static RectTransform CanvasRoot(InventoryGui gui)
        {
            RectTransform root = UiParts.Node("ECR_DeathRecap", gui.transform.parent);
            Canvas source = gui.GetComponent<Canvas>();
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingLayerID = source.sortingLayerID;
            canvas.sortingOrder = source.sortingOrder + AboveInventory;
            canvas.pixelPerfect = source.pixelPerfect;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            CanvasScaler? sourceScaler = gui.GetComponent<CanvasScaler>();
            if (sourceScaler != null)
            {
                scaler.uiScaleMode = sourceScaler.uiScaleMode;
                scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            }
            root.gameObject.AddComponent<GuiScaler>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            return root;
        }

        private static WindowParts? Copy(TextsDialog source, GameObject root)
        {
            GameObject holder = new GameObject("ecr_recap_holder");
            holder.SetActive(false);
            GameObject copy = Object.Instantiate(source.gameObject, holder.transform, false);
            copy.name = "ecr_recap_window";
            TextsDialog dialog = copy.GetComponent<TextsDialog>();
            WindowParts? parts = WindowParts.From(root, dialog);
            Object.DestroyImmediate(dialog);
            copy.transform.SetParent(root.transform, false);
            UiParts.Fill((RectTransform)copy.transform, 0f);
            copy.SetActive(true);
            Object.Destroy(holder);
            return parts;
        }

        // The frame grows to the recap's size; the deaths list keeps the left, the close button sits under it, the right
        // area becomes the hit list at the bottom right, its text layout removed so rows can be placed one by one.
        private static void Arrange(WindowParts parts)
        {
            parts.Frame.sizeDelta = new Vector2(Width, Height);
            parts.Topic.text = "Death Recap";
            UiParts.Place(parts.List, 20f, 51f, 300f, 680f);
            UiParts.Place((RectTransform)parts.Close.transform, 70f, 745f, 200f, 46f);
            UiParts.Place(parts.Area, 340f, 648f, 920f, 142f);
            UiParts.Rewire(parts.Close, RecapWindow.Close);
            UiParts.Rewire(parts.Backdrop, RecapWindow.Close);
            UiParts.Destroy(parts.AreaContent.GetComponent<VerticalLayoutGroup>(), parts.AreaContent.GetComponent<ContentSizeFitter>());
            parts.Text.gameObject.SetActive(false);
            Transform name = parts.AreaContent.Find("Name");
            name?.gameObject.SetActive(false);
        }
    }
}
