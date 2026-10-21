// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Replaces the nameplate's stars with the mod's coloured row. For every starred creature's nameplate it hides the
    /// vanilla two- and three-star badges (which only ever cover those two counts) and adds the coloured
    /// <see cref="StarRow"/>, so the star display is one consistent, individually-drawn row at any count - on a boss's
    /// health bar too, which has no star badges of its own and borrows the creature bar's star sprite. A Phantom copy's
    /// boss bar gets no star row: it is gathered instead and laid out small in one row with the boss's own bar by
    /// <see cref="PhantomBars"/>; a Tethered pair's two bars are stacked by <see cref="TetherBars"/>. It also adds the
    /// icon rows on the star row's line: what a Thieving creature carries (<see cref="PouchIcons"/>) and what a Devouring
    /// creature has eaten (<see cref="MealIcons"/>). Each plate is dressed once (<see cref="PlateDress"/>) and then only
    /// kept: its badges stay hidden every frame, and it is dressed again only when a display setting changes. The game's
    /// plates are read from its own fields, not by reflection.
    /// </summary>
    [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
    public static class EnemyHudPatch
    {
        /// <summary>This frame's Phantom copy bars, reused every frame.</summary>
        private static readonly List<PhantomBars.Bar> Copies = new List<PhantomBars.Bar>();

        /// <summary>This frame's Phantom boss bars, which join their copies' row while any copy stands.</summary>
        private static readonly List<PhantomBars.Bar> Bosses = new List<PhantomBars.Bar>();

        /// <summary>This frame's Tethered pair bars, reused every frame.</summary>
        private static readonly List<PhantomBars.Bar> Pairs = new List<PhantomBars.Bar>();

        /// <summary>How far each plate the game shows has been dressed, by the game's own record of the plate.</summary>
        private static readonly Dictionary<EnemyHud.HudData, PlateDress> Dressed = new Dictionary<EnemyHud.HudData, PlateDress>();

        private static readonly List<EnemyHud.HudData> Gone = new List<EnemyHud.HudData>();

        private static long Generation = -1;
        private static EnemyHud? Owner;
        internal static bool Available => Plugin.NativeRulesReady && Plugin.AllowsUi && WildbloodModules.Active(WildbloodModule.Display)
            && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0
            && ZNet.instance != null && ZNet.m_world != null && ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId;

        internal static void Tick()
        {
            if (!Available || Generation != CruciblePlugin.Session.Generation || Owner != EnemyHud.instance) Reset();
            Generation = CruciblePlugin.Session.Generation;
            Owner = Available ? EnemyHud.instance : null;
        }

        internal static void Reset()
        {
            foreach (EnemyHud.HudData data in Dressed.Keys) Guard.Run("Wildblood plate release", () => Release(data));
            Dressed.Clear(); Gone.Clear(); Copies.Clear(); Bosses.Clear(); Pairs.Clear(); Owner = null;
        }

        private static void Release(EnemyHud.HudData data)
        {
            GameObject gui = data.m_gui;
            if (gui == null) return;
            PhantomBarShape.Restore(gui);
            if (Dressed.TryGetValue(data, out PlateDress dress))
            {
                if (dress.BadgesOwned)
                {
                    if (data.m_level2 != null && !data.m_level2.gameObject.activeSelf) data.m_level2.gameObject.SetActive(dress.Level2);
                    if (data.m_level3 != null && !data.m_level3.gameObject.activeSelf) data.m_level3.gameObject.SetActive(dress.Level3);
                }
                dress.BadgesOwned = false; dress.Starred = false; dress.DressedFor = -1;
            }
            CreatureModifierManager.HideModifierBadges(data);
            ClearRows(gui);
        }

        private static void ClearRows(GameObject gui)
        {
            StarRow? stars = gui.GetComponent<StarRow>();
            if (stars != null) { stars.ClearOwned(); stars.enabled = false; Object.Destroy(stars); }
            PouchIcons? pouch = gui.GetComponent<PouchIcons>();
            if (pouch != null) { pouch.ClearOwned(); pouch.enabled = false; Object.Destroy(pouch); }
            MealIcons? meals = gui.GetComponent<MealIcons>();
            if (meals != null) { meals.ClearOwned(); meals.enabled = false; Object.Destroy(meals); }
            TraitLine? line = gui.GetComponent<TraitLine>();
            if (line != null) line.Remove();
        }

        // Only the health bar update reads a creature's plain name while its trait line shows; hover text keeps the words.
        private static void Prefix() => TraitLine.InHudUpdate = true;

        private static void Finalizer() => TraitLine.InHudUpdate = false;

        private static void Postfix(EnemyHud __instance) =>
            Guard.Run("EnemyHud.UpdateHuds stars", static hud => Decorate(hud), __instance);

        private static void Decorate(EnemyHud hud)
        {
            Tick();
            if (!Available) return;
            Dictionary<Character, EnemyHud.HudData> huds = hud.m_huds;
            if (huds == null)
            {
                return;
            }
            Copies.Clear();
            Bosses.Clear();
            Pairs.Clear();
            int wanted = PlateDress.Wanted();
            int frame = Time.frameCount;
            foreach (EnemyHud.HudData data in huds.Values)
            {
                DecorateOne(data, wanted, frame);
            }
            PhantomBars.Layout(Copies, Bosses);
            TetherBars.Layout(Pairs);
            Forget(frame);
        }

        private static void DecorateOne(EnemyHud.HudData data, int wanted, int frame)
        {
            Character character = data.m_character;
            GameObject gui = data.m_gui;
            if (character == null || gui == null)
            {
                return;
            }
            PlateDress dress = DressOf(data, character);
            dress.Seen = frame;
            dress.BadgesOwned = false; // the native producer just refreshed both badges
            if (dress.Controller == null) dress.Controller = character.GetComponent<EliteController>();
            if (dress.Controller == null || !dress.Controller.Ready)
            {
                Release(data);
                return;
            }
            if (character.IsBoss() && Gather(character, gui))
            {
                ClearRows(gui);
                return;
            }
            if (!dress.DressedUnder(wanted) || dress.TraitStars != dress.Controller.Traits.Stars || dress.TraitMask != dress.Controller.Traits.Mask)
            {
                Dress(data, dress, wanted);
            }
            if (dress.Starred)
            {
                dress.Level2 = data.m_level2 != null && data.m_level2.gameObject.activeSelf;
                dress.Level3 = data.m_level3 != null && data.m_level3.gameObject.activeSelf;
                dress.BadgesOwned = true;
                HideVanillaBadges(data); // the game sets them again every frame from the level
            }
            else dress.BadgesOwned = false;
            TraitLine.Apply(data, dress.Controller);
            CreatureModifierManager.DrawModifierBadges(character, data);
        }

        private static PlateDress DressOf(EnemyHud.HudData data, Character character)
        {
            if (!Dressed.TryGetValue(data, out PlateDress dress))
            {
                dress = new PlateDress(character.GetComponent<EliteController>());
                Dressed[data] = dress;
            }
            return dress;
        }

        /// <summary>Plates the game no longer shows (it drops one a frame at most) are forgotten with it.</summary>
        private static void Forget(int frame)
        {
            Gone.Clear();
            foreach (KeyValuePair<EnemyHud.HudData, PlateDress> pair in Dressed)
            {
                if (pair.Value.Seen != frame)
                {
                    Gone.Add(pair.Key);
                }
            }
            foreach (EnemyHud.HudData data in Gone)
            {
                Guard.Run("Wildblood plate forget", () => Release(data));
                Dressed.Remove(data);
            }
        }

        /// <summary>Collects the boss bars laid out after the loop: a Phantom copy's (true: it takes no star row), a
        /// Phantom boss's and a Tethered pair's (false: decorated like any boss bar, then laid out).</summary>
        private static bool Gather(Character character, GameObject gui)
        {
            if (PhantomBars.IsCopy(character, gui, out PhantomBars.Bar copy))
            {
                Copies.Add(copy);
                return true;
            }
            if (PhantomBars.IsPhantomBoss(character, gui, out PhantomBars.Bar boss))
            {
                Bosses.Add(boss);
            }
            else if (TetherBars.IsTethered(character, out ZDOID pairId))
            {
                Pairs.Add(new PhantomBars.Bar(pairId, gui));
            }
            else PhantomBarShape.Restore(gui);
            return false;
        }

        // Every part the settings ask for, added once; the plate counts as dressed only when none is still missing. A
        // creature with none of this mod's stars keeps the game's badges: a level another mod or the game gave it (this
        // mod's stars off) shows as the game, or that mod, draws it. Nothing is dressed before the creature resolves.
        private static void Dress(EnemyHud.HudData data, PlateDress dress, int wanted)
        {
            EliteController? controller = dress.Controller;
            if (controller != null && !controller.Ready)
            {
                dress.Starred = false;
                dress.DressedFor = -1;
                return;
            }
            bool done = true;
            if (controller != null)
            {
                bool wantsStars = (wanted & PlateDress.Stars) != 0 && controller.Traits.Stars > 0;
                bool rowReady = !wantsStars || EnsureRow(data.m_gui, controller.Creature);
                dress.Starred = wantsStars && rowReady;
                done &= rowReady;
                done &= EnsureIcons<PouchIcons>(data.m_gui, controller, (wanted & PlateDress.Stolen) != 0, Mutation.Thieving);
                done &= EnsureIcons<MealIcons>(data.m_gui, controller, (wanted & PlateDress.Devoured) != 0, Mutation.Devouring);
            }
            dress.DressedFor = done ? wanted : -1;
            dress.TraitStars = controller != null ? controller.Traits.Stars : -1;
            dress.TraitMask = controller != null ? controller.Traits.Mask : -1;
        }

        private static void HideVanillaBadges(EnemyHud.HudData data)
        {
            SetInactive(data.m_level2);
            SetInactive(data.m_level3);
        }

        private static void SetInactive(Transform badge)
        {
            if (badge != null && badge.gameObject.activeSelf)
            {
                badge.gameObject.SetActive(false);
            }
        }

        /// <summary>True once the plate has its star row; false while the sprite or the bar cannot be found yet.</summary>
        private static bool EnsureRow(GameObject gui, Character character)
        {
            if (gui.GetComponent<StarRow>() != null)
            {
                return true;
            }
            Sprite? sprite = FindStarSprite(gui);
            RectTransform? bar = gui.transform.Find("Health") as RectTransform;
            if (sprite == null || bar == null)
            {
                return false;
            }
            gui.AddComponent<StarRow>().Init(character, sprite, bar);
            return true;
        }

        // A mutation's icon row on the nameplate - what a thief carries, what a devourer has eaten - added once, when the
        // player shows it and the creature carries the mutation. True unless it is wanted and the bar is not there yet.
        private static bool EnsureIcons<T>(GameObject gui, EliteController controller, bool shown, Mutation mutation)
            where T : MonoBehaviour, IPlateIcons
        {
            if (!shown || !controller.Traits.Has(mutation) || gui.GetComponent<T>() != null)
            {
                return true;
            }
            if (gui.transform.Find("Health") is RectTransform bar)
            {
                gui.AddComponent<T>().Init(controller.Creature, bar);
                return true;
            }
            return false;
        }

        private static Sprite? FindStarSprite(GameObject gui)
        {
            Sprite? sprite = SpriteUnder(gui.transform.Find("level_3"));
            if (sprite == null)
            {
                sprite = SpriteUnder(gui.transform.Find("level_2"));
            }
            return sprite != null ? sprite : PlainHudStar();
        }

        /// <summary>
        /// The boss health bar carries no star badges of its own, so a starred boss borrows the ordinary creature bar's
        /// star - read from the HUD's template, never instantiated - and draws its row under the boss bar like any other.
        /// </summary>
        private static Sprite? PlainHudStar()
        {
            GameObject? template = EnemyHud.instance != null ? EnemyHud.instance.m_baseHud : null;
            if (template == null)
            {
                return null;
            }
            Sprite? sprite = SpriteUnder(template.transform.Find("level_3"));
            return sprite != null ? sprite : SpriteUnder(template.transform.Find("level_2"));
        }

        private static Sprite? SpriteUnder(Transform badge)
        {
            if (badge == null)
            {
                return null;
            }
            Image image = badge.GetComponentInChildren<Image>(true);
            return image != null ? image.sprite : null;
        }
    }
}
