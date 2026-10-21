// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: FeastMaster/FeastMaster/FeastMasterCore/FoodSlotsHud.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/names/tokens; shared lifecycle/catalog integration;
// native food-root copies, frames, timers and tooltip insertion algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster;
using FMSettings = ExtraPotions.Larder.Runtime.Adopted.FeastMaster.Settings;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Larder.Runtime.Adopted.FoodHud
{
    /// <summary>
    /// The HUD's food slots. Hud.UpdateFood draws one food per entry of m_foodBars, m_foodIcons and m_foodTime and
    /// hides the entries beyond the foods held, so the three lists are extended to <see cref="FoodSlots.MaxSlots"/>
    /// once per HUD while Food Slots is changed: when the HUD is created, or at once when the setting changes with a
    /// HUD already up. A new slot copies the last one and moves it by the distance between the last two. The copied
    /// object is each element's slot root, the highest ancestor that does not also hold the previous slot's element,
    /// so a slot built as one object with its bar, icon and timer inside is copied once and its parts found in the
    /// copy by their path. Only Food Slots of them are shown, see <see cref="FoodSlotFrames"/>. An extended HUD stays
    /// extended until it is rebuilt (the next login).
    /// </summary>
    [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
    public static class FoodSlotsHud
    {
        private static Hud extended;

        public static bool Prepare() => Customized.Any(FMSettings.FoodSlots);

        public static void Installed()
        {
            if (Hud.instance != null)
                Extend(Hud.instance);
        }

        /// <summary>Whether the current HUD carries the extra slots, whose frames then still need hiding.</summary>
        public static bool CurrentHudExtended => Hud.instance != null && extended == Hud.instance;

        [HarmonyPostfix]
        public static void Postfix(Hud __instance) => Extend(__instance);

        private static void Extend(Hud hud)
        {
            int count = hud.m_foodBars?.Length ?? 0;
            if (count < 2 || count >= FoodSlots.MaxSlots || hud.m_foodIcons?.Length != count || hud.m_foodTime?.Length != count)
                return;
            List<Image> bars = new List<Image>(hud.m_foodBars);
            List<Image> icons = new List<Image>(hud.m_foodIcons);
            List<TMP_Text> times = new List<TMP_Text>(hud.m_foodTime);
            while (bars.Count < FoodSlots.MaxSlots)
                AddSlot(bars, icons, times);
            hud.m_foodBars = bars.ToArray();
            hud.m_foodIcons = icons.ToArray();
            hud.m_foodTime = times.ToArray();
            FoodSlotFrames.Capture(hud);
            extended = hud;
            PatchSwitch.MarkDirty();
        }

        private static void AddSlot(List<Image> bars, List<Image> icons, List<TMP_Text> times)
        {
            Dictionary<Transform, Transform> copies = new Dictionary<Transform, Transform>();
            bars.Add(CopyNext(bars, copies));
            icons.Add(CopyNext(icons, copies));
            times.Add(CopyNext(times, copies));
        }

        /// <summary>The last element's counterpart in a copy of its slot root (copied once per root per slot).</summary>
        private static T CopyNext<T>(List<T> list, Dictionary<Transform, Transform> copies) where T : Component
        {
            Transform last = list[list.Count - 1].transform;
            Transform previous = list[list.Count - 2].transform;
            Transform root = SlotRoot(last, previous);
            if (!copies.TryGetValue(root, out Transform copy))
                copies[root] = copy = CopyShifted(root, SlotRoot(previous, last));
            return Counterpart(copy, root, last).GetComponent<T>();
        }

        /// <summary>The highest ancestor of <paramref name="element"/> that does not also hold <paramref name="other"/>.</summary>
        public static Transform SlotRoot(Transform element, Transform other)
        {
            Transform root = element;
            while (root.parent != null && !other.IsChildOf(root.parent))
                root = root.parent;
            return root;
        }

        private static Transform CopyShifted(Transform root, Transform previousRoot)
        {
            Transform copy = Object.Instantiate(root.gameObject, root.parent).transform;
            copy.name = root.name + "_larder";
            copy.SetSiblingIndex(root.GetSiblingIndex() + 1);
            copy.localPosition = root.localPosition + (root.localPosition - previousRoot.localPosition);
            return copy;
        }

        /// <summary>The transform in <paramref name="copy"/> at the path <paramref name="target"/> has below <paramref name="root"/>.</summary>
        private static Transform Counterpart(Transform copy, Transform root, Transform target)
        {
            Stack<int> path = new Stack<int>();
            for (Transform t = target; t != root; t = t.parent)
                path.Push(t.GetSiblingIndex());
            Transform result = copy;
            while (path.Count > 0)
                result = result.GetChild(path.Pop());
            return result;
        }
    }
}
