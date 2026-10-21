// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        // Native notice placement while MeadWorks styling is on; everything moved is recorded and put back exactly when
        // styling turns off, and the native slide and hide animations keep running relative to the moved roots.
        //  - The achievement unlock popup sits just under Cairn's compass bar (or under the Tempest badge when the badge
        //    shows at that spot), horizontally where the native puts it (screen center). Approved by the user on October 19, 2026.
        //  - Popups that arrive together share that row side by side; when the row is full the rest wait unseen, in arrival
        //    order, with their native Start (timers, unlock sound) held until a place frees. Approved by the user on October 19, 2026.
        //  - The top-left message row starts below the hotbar (and any MeadWorks HUD panel above it); the message plate and
        //    every unlock card ("New item", recipe, piece, station, ...) share one left edge, one icon column and one text
        //    column: the message text moves right of its icon like a card, both card texts are left-aligned on the card's
        //    description column, and the card stack starts below the plate row. Approved by the user on October 19, 2026.

        // From the native assets: unlockslide.anim holds UnlockMessage at x 400, y -220 (0.67 s to 4.0 s).
        const float UnlockHoldX = 400f, UnlockHoldY = -220f;
        const float NoticeGap = 6f, PlatePadX = 12f, PlatePadY = 8f;
        const string CompassPanel = "Cairn_Hud", BadgePanel = "Tempest_Badge";

        sealed class Moved
        {
            internal RectTransform Rect;
            internal AchievementUnlockPopup Popup;   // set for an achievement popup root
            internal Vector3 Offset;
            internal TMP_Text Title;                 // set for an unlock card whose title was realigned
            internal TextAlignmentOptions TitleAlignment;
            internal Vector2 TitlePosition, TitleSize;
        }

        static readonly AccessTools.FieldRef<AchievementUnlockPopup, Vector3> popupStart =
            AccessTools.FieldRefAccess<AchievementUnlockPopup, Vector3>("m_startPos");
        static readonly AccessTools.FieldRef<MessageHud, List<GameObject>> unlockRoots =
            AccessTools.FieldRefAccess<MessageHud, List<GameObject>>("m_unlockMessages");
        static readonly List<AchievementUnlockPopup> livePopups = new List<AchievementUnlockPopup>();
        static readonly List<Moved> movedNotices = new List<Moved>();
        static readonly AccessTools.FieldRef<AchievementUnlockPopup, bool> popupHiding =
            AccessTools.FieldRefAccess<AchievementUnlockPopup, bool>("m_shouldHideMe");
        static readonly System.Reflection.MethodInfo popupStartMethod = AccessTools.Method(typeof(AchievementUnlockPopup), "Start");
        static readonly KitPopupQueue<AchievementUnlockPopup> heldPopups = new KitPopupQueue<AchievementUnlockPopup>();
        static readonly Dictionary<AchievementUnlockPopup, int> heldSince = new Dictionary<AchievementUnlockPopup, int>();
        static readonly Dictionary<AchievementUnlockPopup, Rect> popupPlaces = new Dictionary<AchievementUnlockPopup, Rect>();
        static AchievementUnlockPopup startingPopup;

        // The message row while it is owned: native places, and the shared columns in world x.
        static RectTransform rowText, rowIcon;
        static Vector2 rowTextNative, rowIconNative;
        static Vector2Int rowFor;
        static float rowLeft, rowBottom;
        static float iconFromLeft, textFromLeft, cardWidth;   // card units, from the unlock prefab
        // The message text's place beside the icon, and at the plate's left pad for a message without an icon (most quick
        // messages: "Stacked 3 items", "You can't do that"). Without this they kept an empty icon column on their left and
        // read as pushed to the right (playtest, October 20, 2026).
        static Vector2 rowTextIcon, rowTextBare;
        static string rowSeen;
        static bool rowBare;

        /// <summary>True for a native notice rect whose position MeadWorks currently owns (styling on).</summary>
        public static bool OwnsNoticeLayout(RectTransform rect)
        {
            if (rect == null) return false;
            if (rowText != null && (rect == rowText || rect == rowIcon)) return true;
            for (int i = 0; i < movedNotices.Count; i++) if (movedNotices[i].Rect == rect) return true;
            return false;
        }

        /// <summary>
        /// For the top-left message plate while the notice layout owns it: the plate's left edge (world x) and the icon it
        /// should include. False when the native plate geometry applies.
        /// </summary>
        internal static bool NoticePlate(TMP_Text text, out float left, out RectTransform icon)
        {
            left = rowLeft; icon = rowIcon;
            return rowText != null && text != null && text.rectTransform == rowText;
        }

        internal static object NoticeLayoutStatus => new { popups = livePopups.Count, moved = movedNotices.Count, row = rowText != null, queued = heldPopups.Count };

        /// <summary>True while this achievement popup waits for a free place in the row (hidden, its native Start held).</summary>
        public static bool PopupQueued(AchievementUnlockPopup popup) => heldPopups.Contains(popup);

        [HarmonyPatch(typeof(AchievementUnlockPopup), "Start")]
        static class PopupStarted
        {
            // While styled every popup waits in the queue first; the tick runs its native Start once it has a place.
            static bool Prefix(AchievementUnlockPopup __instance)
            {
                if (__instance == null) return true;
                if (!livePopups.Contains(__instance)) livePopups.Add(__instance);
                if (ReferenceEquals(__instance, startingPopup) || !initialized || !On) return true;
                var root = __instance.m_root;
                if (root == null || root == __instance.transform || popupStartMethod == null) return true;
                __instance.enabled = false;
                root.gameObject.SetActive(false);
                heldSince[__instance] = Time.frameCount;
                heldPopups.Enqueue(__instance);
                return false;
            }
        }

        // The held native Start: the vanilla stacking coroutine and the unlock sound, now that the popup is shown.
        static void ShowHeldPopup(AchievementUnlockPopup popup)
        {
            heldSince.Remove(popup);
            if (popup == null) return;
            popup.m_root.gameObject.SetActive(true);
            popup.enabled = true;
            startingPopup = popup;
            try { popupStartMethod.Invoke(popup, null); }
            finally { startingPopup = null; }
        }

        // Claims a row place for a held popup: a free slot, or (forced: nothing else shown) the center slot.
        static bool PlaceHeldPopup(AchievementUnlockPopup popup, bool force)
        {
            // Its overlay canvas scales during its first frames; measure after that, as the native resting point is.
            if (heldSince.TryGetValue(popup, out int since) && Time.frameCount - since < 2) return false;
            // Without the compass row the native stacking applies, as before.
            if (!KitHudLayout.ShownScreenRect(CompassPanel, out var bar)) return true;
            if (!KitHudLayout.ScreenRect(popup.m_root as RectTransform, out var at)) return force;
            var slot = PopupSlot(popup, at, bar.yMin - NoticeGap, out bool free);
            if (!free && !force) return false;
            popupPlaces[popup] = slot;
            return true;
        }

        static bool AnyPopupShown()
        {
            for (int i = 0; i < livePopups.Count; i++) if (livePopups[i] != null && !heldPopups.Contains(livePopups[i])) return true;
            return false;
        }

        static void TickNoticeLayout()
        {
            livePopups.RemoveAll(p => p == null);
            heldPopups.Prune(p => p == null);
            if (popupPlaces.Count > 0 || heldSince.Count > 0) PruneDead();
            for (int i = movedNotices.Count - 1; i >= 0; i--) if (movedNotices[i].Rect == null) movedNotices.RemoveAt(i);
            var hud = MessageHud.instance;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!On) { RestorePopups(); heldPopups.ReleaseAll(ShowHeldPopup); }
            else
            {
                for (int i = 0; i < livePopups.Count; i++) PlacePopup(livePopups[i]);
                heldPopups.Release(PlaceHeldPopup, AnyPopupShown, ShowHeldPopup);
            }
            bool column = On;
            if (!column || rowText != null && (hud == null || rowText != hud.m_messageText.rectTransform || screen != rowFor)) RestoreColumn();
            if (!column || hud == null || rowText == null && !PlaceRow(hud, screen)) return;
            FollowIcon(hud);
            var roots = unlockRoots(hud);
            for (int i = 0; i < roots.Count; i++) if (roots[i] != null) PlaceUnlock(roots[i].transform as RectTransform, i, hud.m_maxUnlockMsgSpace);
        }

        static void PlacePopup(AchievementUnlockPopup popup)
        {
            var root = popup.m_root as RectTransform;
            if (root == null || OwnsNoticeLayout(root) || heldPopups.Contains(popup)) return;
            // Its overlay canvas is laid out only once the popup has captured its resting point (two frames after
            // Start, still faded out); measuring earlier reads the prefab's 1920x1080 space.
            if (popupStart(popup) == Vector3.zero) return;
            // Absolute target from this popup's native resting rect, applied once: under the compass (or the badge
            // there). Vanilla shows every popup at once and stacks each new one under the live ones, which here would land
            // on the center message right below; simultaneous popups instead share the row under the compass, in the
            // first free slot from the native center outward (center, right, left, ...). Vanilla's stack offset is not kept.
            if (!KitHudLayout.ScreenRect(root, out var at)) return;
            bool placed = popupPlaces.TryGetValue(popup, out var target);
            bool compass = KitHudLayout.ShownScreenRect(CompassPanel, out var bar);
            // At session start the popup can arrive before the compass shows; wait for it while the popup still rests.
            if (!placed && !compass && !popupHiding(popup)) return;
            if (!placed) target = compass ? PopupSlot(popup, at, bar.yMin - NoticeGap, out _) : at;
            popupPlaces.Remove(popup);
            var shift = target.position - at.position;
            // Recorded even when nothing moves, so each popup is placed exactly once.
            var offset = WorldOffset(root, shift);
            Shift(new Moved { Rect = root, Popup = popup, Offset = offset }, 1f);
        }

        static readonly List<Rect> popupBlocks = new List<Rect>();

        // Screen rect for a popup the size of `at` in the row whose top is `top`: the first slot, from the native center
        // outward, that stays on screen and is clear of other popups (placed ones where they are now, so a hiding popup
        // frees its slot once it has slid off it; claimed ones where they will go) and shown HUD panels (the badge lowers
        // its slot). `free` is false when the row is full; the center slot is returned then.
        static Rect PopupSlot(AchievementUnlockPopup popup, Rect at, float top, out bool free)
        {
            popupBlocks.Clear();
            KitHudLayout.ShownScreenRects(popupBlocks);
            bool badgeShown = KitHudLayout.ShownScreenRect(BadgePanel, out var badge);
            for (int i = 0; i < movedNotices.Count; i++)
            {
                var other = movedNotices[i].Popup;
                if (other != null && other != popup && KitHudLayout.ScreenRect(movedNotices[i].Rect, out var r)) popupBlocks.Add(r);
            }
            foreach (var pair in popupPlaces) if (pair.Key != null && pair.Key != popup) popupBlocks.Add(pair.Value);
            var first = new Rect(at.x, top - at.height, at.width, at.height);
            if (badgeShown && badge.Overlaps(first)) first.y = badge.yMin - NoticeGap - at.height;
            for (int k = 0; at.width > 0f; k++)
            {
                int step = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                var slot = new Rect(at.x + step * (at.width + NoticeGap), top - at.height, at.width, at.height);
                if (slot.xMin < 0f || slot.xMax > Screen.width) { if (k % 2 == 0) break; continue; }
                if (badgeShown && badge.Overlaps(slot)) slot.y = badge.yMin - NoticeGap - at.height;
                free = true;
                for (int i = 0; i < popupBlocks.Count && free; i++) if (popupBlocks[i].Overlaps(slot)) free = false;
                if (free) return slot;
            }
            free = false;
            return first;   // the row is full: the center slot, taken only when nothing else is shown
        }

        static readonly List<AchievementUnlockPopup> deadPopups = new List<AchievementUnlockPopup>();

        static void PruneDead()
        {
            deadPopups.Clear();
            foreach (var key in popupPlaces.Keys) if (key == null) deadPopups.Add(key);
            foreach (var key in heldSince.Keys) if (key == null) deadPopups.Add(key);
            foreach (var key in deadPopups) { popupPlaces.Remove(key); heldSince.Remove(key); }
        }

        static Vector3 WorldOffset(RectTransform rect, Vector2 pixels)
        {
            if (pixels == Vector2.zero) return Vector3.zero;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 from = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, from, camera, out var a)
                || !RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, from + pixels, camera, out var b)) return Vector3.zero;
            return b - a;
        }

        // Columns come from the native unlock prefab: icon center and description left, measured from the card's left.
        static bool Columns(MessageHud hud)
        {
            var prefab = hud.m_unlockMsgPrefab != null ? hud.m_unlockMsgPrefab.transform : null;
            var box = prefab != null ? prefab.Find("UnlockMessage") as RectTransform : null;
            var icon = prefab != null ? prefab.Find("UnlockMessage/icon_bkg") as RectTransform : null;
            var body = prefab != null ? prefab.Find("UnlockMessage/UnlockDescription") as RectTransform : null;
            if (box == null || icon == null || body == null) return false;
            float half = box.sizeDelta.x * .5f;
            cardWidth = box.sizeDelta.x;
            iconFromLeft = half + icon.anchoredPosition.x;
            textFromLeft = half + body.anchoredPosition.x - body.sizeDelta.x * body.pivot.x;
            return true;
        }

        static bool PlaceRow(MessageHud hud, Vector2Int screen)
        {
            if (hud.m_messageText == null || hud.m_messageIcon == null || !Columns(hud)) return false;
            var text = hud.m_messageText.rectTransform; var icon = hud.m_messageIcon.rectTransform;
            float unit = text.lossyScale.x;
            var corners = new Vector3[4];
            text.GetWorldCorners(corners);
            float textLeft = corners[0].x, middle = (corners[0].y + corners[1].y) * .5f;
            rowText = text; rowIcon = icon; rowFor = screen;
            rowTextNative = text.anchoredPosition; rowIconNative = icon.anchoredPosition;
            rowLeft = textLeft - PlatePadX * unit;
            float half = Mathf.Max(icon.rect.height * icon.lossyScale.y, (corners[1].y - corners[0].y)) * .5f;
            float pad = PlatePadY * text.lossyScale.y;
            // Start the row (plate top) a gap below the hotbar and any MeadWorks HUD panel above it in this column.
            float down = RowDrop(text, rowLeft, rowLeft + cardWidth * unit, middle + half + pad, middle - half - pad);
            text.position += new Vector3(rowLeft + textFromLeft * unit - textLeft, down, 0f);
            middle += down;
            icon.position = new Vector3(rowLeft + iconFromLeft * unit, middle, icon.position.z);
            rowBottom = middle - half - pad;
            rowTextIcon = text.anchoredPosition;
            rowTextBare = rowTextIcon - new Vector2((textFromLeft - PlatePadX) * text.localScale.x, 0f);
            rowSeen = null; rowBare = false;
            return true;
        }

        // Each message is laid out once when it appears: beside its icon, or at the plate's left pad when it has none (the
        // game hides a missing icon with alpha 0 and keeps the last sprite).
        static void FollowIcon(MessageHud hud)
        {
            var icon = hud.m_messageIcon;
            bool shown = icon != null && icon.sprite != null && icon.gameObject.activeInHierarchy && icon.canvasRenderer.GetAlpha() > .01f;
            string content = hud.m_messageText.text;
            if (content != rowSeen || rowBare && shown)
            {
                rowSeen = content;
                rowBare = !shown;
            }
            var want = rowBare ? rowTextBare : rowTextIcon;
            if (rowText.anchoredPosition != want) rowText.anchoredPosition = want;
        }

        static readonly List<Rect> rowBlocks = new List<Rect>();

        // World y the row must move (0 or negative) so its top clears what sits above it in [left, right].
        static float RowDrop(RectTransform text, float left, float right, float top, float bottom)
        {
            var canvas = text.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, new Vector3(left, top, text.position.z));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, new Vector3(right, bottom, text.position.z));
            var row = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            rowBlocks.Clear();
            KitHudLayout.ShownScreenRects(rowBlocks);
            if (Hud.instance != null)
                foreach (var bar in Hud.instance.GetComponentsInChildren<HotkeyBar>())
                    foreach (Transform slot in bar.transform)
                        if (slot.gameObject.activeInHierarchy && slot is RectTransform shown && KitHudLayout.ScreenRect(shown, out var r)) rowBlocks.Add(r);
            float limit = row.yMax;
            foreach (var block in rowBlocks)
                if (block.xMin < row.xMax && block.xMax > row.xMin && block.center.y > row.center.y && block.yMin - NoticeGap < limit)
                    limit = block.yMin - NoticeGap;
            if (limit >= row.yMax) return 0f;
            return WorldOffset(text, new Vector2(0f, limit - row.yMax)).y;
        }

        static void PlaceUnlock(RectTransform root, int slot, float space)
        {
            if (root == null || OwnsNoticeLayout(root)) return;
            var box = root.Find("UnlockMessage") as RectTransform;
            var title = root.Find("UnlockMessage/UnlockTitle") as RectTransform;
            var body = root.Find("UnlockMessage/UnlockDescription") as RectTransform;
            if (box == null || title == null || body == null) return;
            float unit = root.lossyScale.y;
            // Native resting rect of the slot-0 card, in the root's space (anchors at the root's top-left).
            float left = root.rect.xMin + Mathf.Lerp(0f, root.rect.width, box.anchorMin.x) + UnlockHoldX - box.rect.width * box.pivot.x;
            float top = root.rect.yMin + Mathf.Lerp(0f, root.rect.height, box.anchorMin.y) + UnlockHoldY + box.rect.height * (1f - box.pivot.y);
            Vector3 corner = root.TransformPoint(new Vector3(left, top, 0f));
            // This root already carries the native slot offset; the first slot's top decides the stack's shift.
            float firstTop = corner.y + space * slot * unit;
            float dy = Mathf.Min(0f, rowBottom - NoticeGap * unit - firstTop);
            var moved = new Moved { Rect = root, Offset = new Vector3(rowLeft - corner.x, dy, 0f) };
            // Title onto the description's text column, left-aligned, ending a pad short of the card's right edge.
            var text = title.GetComponent<TMP_Text>();
            if (text != null && Mathf.Approximately(title.anchorMin.x, body.anchorMin.x) && Mathf.Approximately(title.anchorMax.x, body.anchorMax.x))
            {
                moved.Title = text; moved.TitleAlignment = text.alignment; moved.TitlePosition = title.anchoredPosition; moved.TitleSize = title.sizeDelta;
                float columnLeft = body.anchoredPosition.x - body.rect.width * body.pivot.x;
                float columnRight = box.rect.width * (1f - title.anchorMin.x) - PlatePadX;
                float width = columnRight - columnLeft;
                title.sizeDelta = new Vector2(width - box.rect.width * (title.anchorMax.x - title.anchorMin.x), title.sizeDelta.y);
                title.anchoredPosition = new Vector2(columnLeft + width * title.pivot.x, title.anchoredPosition.y);
                text.alignment = TextAlignmentOptions.Left;
            }
            Shift(moved, 1f);
        }

        static void Shift(Moved moved, float sign)
        {
            var delta = moved.Offset * sign;
            if (delta != Vector3.zero) moved.Rect.position += delta;
            // The popup captures its resting point a couple of frames after Start and slides from it when hiding.
            if (moved.Popup != null && popupStart(moved.Popup) != Vector3.zero) popupStart(moved.Popup) += delta;
            if (sign > 0) movedNotices.Add(moved);
            else if (moved.Title != null)
            {
                var title = moved.Title.rectTransform;
                moved.Title.alignment = moved.TitleAlignment; title.anchoredPosition = moved.TitlePosition; title.sizeDelta = moved.TitleSize;
            }
        }

        static void RestorePopups()
        {
            popupPlaces.Clear();
            for (int i = movedNotices.Count - 1; i >= 0; i--)
                if (!ReferenceEquals(movedNotices[i].Popup, null))
                { if (movedNotices[i].Rect != null) Shift(movedNotices[i], -1f); movedNotices.RemoveAt(i); }
        }

        static void RestoreColumn()
        {
            for (int i = movedNotices.Count - 1; i >= 0; i--)
                if (ReferenceEquals(movedNotices[i].Popup, null))
                { if (movedNotices[i].Rect != null) Shift(movedNotices[i], -1f); movedNotices.RemoveAt(i); }
            if (rowText != null) rowText.anchoredPosition = rowTextNative;
            if (rowIcon != null) rowIcon.anchoredPosition = rowIconNative;
            rowText = rowIcon = null;
            rowSeen = null; rowBare = false;
        }

        static void ShutdownNoticeLayout()
        {
            heldPopups.Prune(p => p == null);
            heldPopups.ReleaseAll(ShowHeldPopup);   // never dropped: anything still waiting shows natively
            heldSince.Clear();
            RestorePopups(); RestoreColumn(); RestoreUnlockFade(); livePopups.Clear();
        }
    }
}
