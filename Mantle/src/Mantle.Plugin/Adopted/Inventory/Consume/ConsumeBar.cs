using System.Text;
using System;
using HarmonyLib;
using Mantle.Core;
using Mantle.Slots;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using Object = UnityEngine.Object;

namespace Mantle.Consume
{
    /// <summary>
    /// The Food and Mead bar (the user's request, 2026-09-28: the food and mead hotkeys shown in the bottom left of the
    /// screen). In the empty strip under the game's health panel, level with its left edge: a food square with the Food
    /// Key over it (<see cref="ConsumeBarCell"/>), then a square per Mead slot with its Mead Slot key and its mead
    /// (<see cref="ConsumeBarSlots"/>; Mantle, 2026-10-10, parity choice #29, as PackPanel 0.15 since the Mead Key went).
    /// Food without slots or a key, or a slot without a key, is left out; nothing shows without any, while dead, or with
    /// the module off. Mantle: hidden while a build tool is in hand (<c>Player.InPlaceMode</c>: hammer, hoe, cultivator or any
    /// placement tool), so the game's key hints in the bottom left stay readable, and shown again when it is put away (user
    /// ruling 2026-10-13, a deliberate difference from PackPanel); the keys already do nothing there (<see cref="ConsumeKeys.Ready"/>). A child of the HUD's root, so it hides with the HUD. Checked from a
    /// <c>Hud.Update</c> postfix ten times a second, built again only when a group comes or goes or a key changes. Per
    /// player (<c>5. Look / Food And Mead Bar</c>); local only, nothing is sent.
    /// </summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
    public static class ConsumeBar
    {
        public const string Name = "Mantle_consumebar";
        private const float Gap = 6f;
        private const float Every = 0.1f;

        /// <summary>The bar's bottom-left corner in HUD units: the health panel's left edge, under its bottom (58).</summary>
        private static readonly Vector2 Corner = new Vector2(50f, 6f);

        private static RectTransform bar;
        private static string built;
        private static float next;
        private static Hud owner;

        [HarmonyPostfix]
        public static void Postfix(Hud __instance)
        {
            if (owner != __instance)
            {
                Shutdown();
                owner = __instance;
            }
            if (Time.time < next)
                return;
            next = Time.time + Every;
            Player player = Player.m_localPlayer;
            string plan = Shown(player) ? Plan() : "";
            if (plan.Length > 0 && (bar == null || plan != built))
                Build(__instance, plan);
            if (bar == null)
                return;
            bool show = plan.Length > 0;
            if (bar.gameObject.activeSelf != show)
                bar.gameObject.SetActive(show);
            if (show)
            {
                Position();
                ConsumeBarSlots.Refresh(player);
            }
        }

        public static void Shutdown()
        {
            if (bar != null)
                Object.Destroy(bar.gameObject);
            bar = null;
            owner = null;
            built = null;
            next = 0f;
        }

        // Keep the pinned corner when no extended native food column is present. Only this owned bar moves.
        private static void Position()
        {
            bar.anchoredPosition = Corner;
            if (!(bar.parent is RectTransform parent) || !CruciblePlugin.IsReady
                || !CruciblePlugin.Services.Capabilities.TryResolve<Func<Tuple<int, float>>>(
                    "meadworks.larder.hud.foodslots", new System.Version(1, 0), out var geometry))
                return;
            Tuple<int, float> column;
            try { column = geometry(); }
            catch { return; }
            if (column == null || column.Item1 <= FoodCount.GameFoods || float.IsNaN(column.Item2)
                || float.IsInfinity(column.Item2) || column.Item2 < 0f || column.Item2 > Screen.height)
                return;
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                new Vector2(0f, column.Item2), camera, out Vector2 bottom))
                return;
            float edge = bottom.y - parent.rect.yMin;
            if (edge >= Corner.y + ConsumeBarCell.Size + Gap)
                return;
            float height = parent.rect.height;
            if (height < ConsumeBarCell.Size)
                return;
            bar.anchoredPosition = new Vector2(Corner.x, Mathf.Clamp(edge - ConsumeBarCell.Size - Gap, 0f, height - ConsumeBarCell.Size));
        }

        private static bool Shown(Player player) =>
            player != null && !player.IsDead() && !player.InPlaceMode() && InventoryState.Active && player == InventoryState.Player
            && ConsumeSettings.Bar.Value;

        private static bool FoodDrawn() => InventoryState.CellsOf(SlotKind.Food).Count > 0 && ConsumeKeyNames.Food.Length > 0;

        /// <summary>What the bar is built from: the food square and each drawn Mead slot with its key; empty when nothing is drawn.</summary>
        private static string Plan()
        {
            StringBuilder plan = new StringBuilder();
            if (FoodDrawn())
                plan.Append("food ").Append(ConsumeKeyNames.Food).Append('|');
            ConsumeBarSlots.Plan(plan);
            return plan.ToString();
        }

        private static void Build(Hud hud, string plan)
        {
            if (bar != null)
                Object.Destroy(bar.gameObject);
            bar = null;
            GameObject template = Template(hud);
            if (template == null)
                return;
            bar = MakeBar(hud.m_healthPanel);
            float x = 0f;
            if (FoodDrawn())
            {
                ConsumeBarCell.Group(bar, template, SlotKind.Food, ConsumeKeyNames.Food, x);
                x += ConsumeBarCell.Size + Gap;
            }
            ConsumeBarSlots.Build(bar, template, x, Gap);
            built = plan;
        }

        /// <summary>The game's first food square (<c>hudroot/healthpanel/food0</c>, the parent of <c>m_foodIcons[0]</c>); null before the HUD is up.</summary>
        private static GameObject Template(Hud hud)
        {
            if (hud == null || hud.m_healthPanel == null || hud.m_foodIcons == null || hud.m_foodIcons.Length == 0 || hud.m_foodIcons[0] == null)
                return null;
            Transform square = hud.m_foodIcons[0].transform.parent;
            return square != null ? square.gameObject : null;
        }

        /// <summary>The bar, a sibling of the health panel under the HUD's root, anchored to the screen's bottom-left corner.</summary>
        private static RectTransform MakeBar(RectTransform healthPanel)
        {
            GameObject go = new GameObject(Name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(healthPanel.parent, false);
            rect.SetSiblingIndex(healthPanel.GetSiblingIndex() + 1);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, ConsumeBarCell.Size);
            rect.anchoredPosition = Corner;
            return rect;
        }
    }
}
