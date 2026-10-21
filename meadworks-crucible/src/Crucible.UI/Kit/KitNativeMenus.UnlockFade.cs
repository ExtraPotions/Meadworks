// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static partial class KitNativeMenus
    {
        // While MeadWorks styling is on, unlock cards fade in place on the shared left edge instead of sliding. After the
        // Animator has run each frame the card's box is pinned at its native resting place and the vanilla unlockslide
        // progress becomes the card's opacity: fade in over the slide-in (0 to 0.67 s), hold, fade out over the exit
        // (4.0 to 4.98 s). Timing is the game's; with styling off pinning stops (the Animator places the box again next
        // frame) and the opacity this adds is removed. Approved by the user on October 19, 2026.

        const float FadeInEnd = 2f / 3f, FadeOutStart = 4f, FadeOutEnd = 4.983f;

        sealed class Faded { internal RectTransform Box; internal CanvasGroup Group; internal bool Added; internal float Alpha; }
        static readonly List<Faded> faded = new List<Faded>();

        /// <summary>Call from the UI host's LateUpdate, after the game's Animators have written this frame.</summary>
        public static void LateTickNotices()
        {
            for (int i = faded.Count - 1; i >= 0; i--) if (faded[i].Box == null) faded.RemoveAt(i);
            var hud = MessageHud.instance;
            if (!(On && initialized) || hud == null) { RestoreUnlockFade(); return; }
            var roots = unlockRoots(hud);
            for (int i = 0; i < roots.Count; i++)
            {
                var box = roots[i] != null ? roots[i].transform.Find("UnlockMessage") as RectTransform : null;
                var animator = box != null ? box.GetComponent<Animator>() : null;
                if (animator == null || !animator.isActiveAndEnabled) continue;
                var entry = Fade(box);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                float time = state.IsTag("done") ? FadeOutEnd : Mathf.Min(state.normalizedTime, 1f) * state.length;
                entry.Group.alpha = time < FadeInEnd ? time / FadeInEnd
                    : time < FadeOutStart ? 1f
                    : Mathf.Clamp01(1f - (time - FadeOutStart) / (FadeOutEnd - FadeOutStart));
                box.anchoredPosition = new Vector2(UnlockHoldX, UnlockHoldY);
            }
        }

        static Faded Fade(RectTransform box)
        {
            for (int i = 0; i < faded.Count; i++) if (faded[i].Box == box) return faded[i];
            var group = box.GetComponent<CanvasGroup>();
            var entry = new Faded { Box = box, Group = group != null ? group : box.gameObject.AddComponent<CanvasGroup>(), Added = group == null, Alpha = group != null ? group.alpha : 1f };
            faded.Add(entry);
            return entry;
        }

        static void RestoreUnlockFade()
        {
            for (int i = 0; i < faded.Count; i++)
            {
                var entry = faded[i];
                if (entry.Group == null) continue;
                if (entry.Added) Object.Destroy(entry.Group); else entry.Group.alpha = entry.Alpha;
            }
            faded.Clear();
        }
    }
}
