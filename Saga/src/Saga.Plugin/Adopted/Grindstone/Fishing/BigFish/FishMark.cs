// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/BigFish/FishMark.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Keeps a fish looking right on every client with a screen (not on a dedicated server). The game reads a fish's item
    /// data from its ZDO only when the fish loads (ItemDrop.Awake), so a change made later on the owner (a big one growing
    /// on the hook) would never show elsewhere. Twice a second, on a client that does not own the fish, the mark reloads
    /// it while it is hooked, and once two seconds after it loaded (in case its data arrived after it did); ItemDrop.Load
    /// reads only when the ZDO's data changed, and rescales the fish. It also puts the glow on a legendary fish
    /// (<see cref="LegendaryGlow"/>), on every client including the owner's, and takes it off if the fish stops being one.
    /// While Fishing is turned off it does nothing and takes any glow off. The marks are driven by one update
    /// (<see cref="Ticker"/>), not one per fish.
    /// </summary>
    public sealed class FishMark : MonoBehaviour
    {
        private const float Every = 0.5f;
        private const float LateLoad = 2f;

        private static readonly List<FishMark> marks = new List<FishMark>();

        private Fish fish;
        private LegendaryGlow glow;
        private float next;
        private float born;
        private bool lateLoaded;
        private int index = -1;
        private Action refresh;

        [HarmonyPatch(typeof(Fish), nameof(Fish.Start))]
        private static class Attach
        {
            [HarmonyPostfix]
            private static void Postfix(Fish __instance)
            {
                if (ZNet.instance != null && !ZNet.instance.IsDedicated())
                    HookGuard.Run("fish mark", static fish => Add(fish), __instance);
            }
        }

        private static void Add(Fish fish)
        {
            FishMark mark = fish.gameObject.AddComponent<FishMark>();
            mark.fish = fish;
            mark.born = Time.time;
            mark.refresh = mark.Refresh;
            mark.index = marks.Count;
            marks.Add(mark);
            Ticker.Ensure();
        }

        /// <summary>Refreshes each loaded fish's mark twice a second, from <see cref="Ticker"/>.</summary>
        internal static void Shutdown() { foreach (var mark in marks.ToArray()) { mark.RemoveGlow(); UnityEngine.Object.Destroy(mark); } }

        internal static void TickAll()
        {
            float now = Time.time;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                FishMark mark = marks[i];
                if (now < mark.next)
                    continue;
                mark.next = now + Every;
                HookGuard.Run("fish mark", mark.refresh);
            }
        }

        /// <summary>Swap-removes the mark from the list.</summary>
        private void OnDestroy()
        {
            if (index < 0 || index >= marks.Count || !ReferenceEquals(marks[index], this))
                return;
            FishMark moved = marks[marks.Count - 1];
            marks[index] = moved;
            moved.index = index;
            marks.RemoveAt(marks.Count - 1);
            index = -1;
        }

        private void Refresh()
        {
            if (fish == null || fish.m_nview == null || !fish.m_nview.IsValid())
                return;
            if (!FishSkill.Active)
            {
                RemoveGlow();
                return;
            }
            ItemDrop item = FishInfo.Item(fish);
            if (item != null && !fish.m_nview.IsOwner() && (Hooked() || !lateLoaded && Time.time - born >= LateLoad))
            {
                lateLoaded = true;
                item.Load();
            }
            if (!FishInfo.IsLegendary(FishInfo.Level(fish)))
                RemoveGlow();
            else if (glow == null)
                glow = LegendaryGlow.Attach(transform);
        }

        private void RemoveGlow()
        {
            if (glow == null)
                return;
            glow.Remove();
            glow = null;
        }

        private bool Hooked() => fish.m_nview.GetZDO().GetInt(ZDOVars.s_hooked) == 1;
    }
}
