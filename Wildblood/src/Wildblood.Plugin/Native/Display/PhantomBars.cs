// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// Lays a Phantom fight's bars out as one row of small, identical health bars, the boss's among them. The game draws
    /// every boss bar in the same place at the top of the screen, so without this the bars sit on top of each other and
    /// only one shows. Each frame, after the game has updated its bars, each copy's bar - and the boss's own while any of
    /// its copies stands - is shrunk to a quarter of the boss bar's width and a sliver of its height
    /// (<see cref="PhantomBarShape"/>) and put in a slot of the row where the boss bar was: four side by side are as long
    /// as the boss bar, a fifth starts a second row, and a short row is centred. Names shrink with them and the boss's
    /// stars are hidden, since copies show none. Bars are ordered by the random number each one drew at its split, so the
    /// boss's place in the row says nothing about which one it is, and a bar keeps its place until one before it falls.
    /// When the last copy falls the boss's bar is put back as the game drew it. A copy whose boss this client does not
    /// hold sits in the row under the boss bar's place, as before. Drawn locally on each client from the replicated
    /// marks; nothing is sent.
    /// </summary>
    internal static class PhantomBars
    {
        public readonly struct Bar
        {
            public readonly ZDOID Id;
            public readonly GameObject Gui;

            /// <summary>The Phantom boss the bar belongs to: a copy's boss, a boss's own id; none for other bars.</summary>
            public readonly ZDOID Of;

            public readonly float Order;

            public Bar(ZDOID id, GameObject gui) : this(id, gui, ZDOID.None, 0f)
            {
            }

            public Bar(ZDOID id, GameObject gui, ZDOID of, float order)
            {
                Id = id;
                Gui = gui;
                Of = of;
                Order = order;
            }
        }

        private const int PerRow = 4;
        private const float Gap = 10f;

        /// <summary>From the boss bar's place to the first row when the boss is not in the row: clear of its stars.</summary>
        private const float FirstDrop = 46f;
        private const float RowStep = 26f;

        /// <summary>The boss bar's width when the game's template cannot be read.</summary>
        private const float FallbackWidth = 600f;

        /// <summary>True, with its bar, when this character is a Phantom copy - read from its ZDO, ready or not.</summary>
        public static bool IsCopy(Character character, GameObject gui, out Bar bar)
        {
            bar = default;
            ZDO? zdo = BossZdo(character); // a copy is its boss's own prefab, so only a boss bar can be one
            ZDOID boss = zdo != null ? AspectStore.GetPhantomOf(zdo) : ZDOID.None;
            if (zdo == null || boss == ZDOID.None)
            {
                return false;
            }
            bar = new Bar(zdo.m_uid, gui, boss, AspectStore.GetPhantomOrder(zdo));
            return true;
        }

        /// <summary>True, with its bar, for a Phantom boss itself: its bar joins its copies' row while any of them stands.</summary>
        public static bool IsPhantomBoss(Character character, GameObject gui, out Bar bar)
        {
            bar = default;
            ZDO? zdo = BossZdo(character);
            EliteController? controller = zdo != null ? ReadyElites.Of(character) : null;
            if (zdo == null || controller == null || controller.Traits.PhantomCopy
                || !controller.Traits.HasAspect(Aspect.Phantom))
            {
                return false;
            }
            bar = new Bar(zdo.m_uid, gui, zdo.m_uid, AspectStore.GetPhantomOrder(zdo));
            return true;
        }

        public static void Layout(List<Bar> copies, List<Bar> bosses)
        {
            GameObject? bossHud = EnemyHud.instance != null ? EnemyHud.instance.m_baseHudBoss : null;
            RectTransform? template = bossHud != null ? bossHud.transform as RectTransform : null;
            if (template == null)
            {
                return;
            }
            float top = Join(copies, bosses, template) ? 0f : FirstDrop;
            copies.Sort(ByOrder);
            float slot = BossBarWidth(template) / PerRow;
            for (int i = 0; i < copies.Count; i++)
            {
                if (copies[i].Gui != null && copies[i].Gui.transform is RectTransform root)
                {
                    PhantomBarShape.Begin(root);
                    Vector2 place = template.anchoredPosition + SlotOffset(i, copies.Count, slot, top);
                    if (root.anchoredPosition != place)
                    {
                        root.anchoredPosition = place; // a write marks the canvas for a rebuild: only when it moved
                    }
                    try { PhantomBarShape.Shrink(root, slot - Gap); }
                    finally { if (root != null) PhantomBarShape.Capture(root, true); }
                }
            }
        }

        /// <summary>Bars in ZDO id order, so each keeps its place on every client while the ones before it stand.</summary>
        public static int ById(Bar a, Bar b) =>
            a.Id.UserID != b.Id.UserID ? a.Id.UserID.CompareTo(b.Id.UserID) : a.Id.ID.CompareTo(b.Id.ID);

        private static int ByOrder(Bar a, Bar b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : ById(a, b);

        /// <summary>
        /// Each Phantom boss with a copy standing joins the row, stars hidden; any other is put back as the game drew it.
        /// True when a boss joined, so the row starts where the boss bar was.
        /// </summary>
        private static bool Join(List<Bar> copies, List<Bar> bosses, RectTransform template)
        {
            bool joined = false;
            foreach (Bar boss in bosses)
            {
                bool split = HasCopy(copies, boss.Id);
                PhantomBarShape.ShowStars(boss.Gui, !split);
                if (!split)
                {
                    PhantomBarShape.Restore(boss.Gui);
                    continue;
                }
                copies.Add(boss);
                joined = true;
            }
            return joined;
        }

        private static bool HasCopy(List<Bar> bars, ZDOID boss)
        {
            foreach (Bar bar in bars)
            {
                if (bar.Of == boss && bar.Id != boss)
                {
                    return true;
                }
            }
            return false;
        }

        private static ZDO? BossZdo(Character character)
        {
            if (!character.IsBoss())
            {
                return null;
            }
            ZNetView nview = character.m_nview;
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }

        private static float BossBarWidth(RectTransform template)
        {
            RectTransform? health = PhantomBarShape.HealthOf(template);
            return health != null && health.sizeDelta.x > 1f ? health.sizeDelta.x : FallbackWidth;
        }

        /// <summary>Where slot <paramref name="index"/> of <paramref name="count"/> sits relative to the boss bar.</summary>
        private static Vector2 SlotOffset(int index, int count, float slot, float top)
        {
            int row = index / PerRow;
            int inRow = Mathf.Min(PerRow, count - row * PerRow);
            float x = (index % PerRow - (inRow - 1) / 2f) * slot;
            return new Vector2(x, -(top + row * RowStep));
        }
    }
}
