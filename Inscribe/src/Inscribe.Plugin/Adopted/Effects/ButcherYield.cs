// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Butcher's Cut (<c>butcher_yield</c>): an animal the player kills drops X more of each of its meat and hide drops.
    /// <para>
    /// The game rolls a creature's loot in <c>CharacterDrop.GenerateDropList</c> on the dying creature's owner (directly,
    /// or through the ragdoll that keeps the list for later), which is often not the killer's machine. The killer is the
    /// last hitter (<c>Character.m_lastHit</c>, the rule Loot uses); the owner reads that player's published total from
    /// the player's own ZDO (<see cref="PlayerStats"/>, clamped to the running rules) and adds X to every meat or hide
    /// row of the rolled list. An animal is wildlife (the AnimalsVeg faction: deer, boar, hare, neck...) or any
    /// tameable creature (wolf, lox, hen, asksvin), tamed or not, never a boss (judgement call: butchering livestock is
    /// the point). Meat and hide: see <see cref="AnimalProduce"/>.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class ButcherYieldPatch
    {
        private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
        {
            if (__result == null || __result.Count == 0 || __instance == null)
            {
                return;
            }
            Character? creature = __instance.m_character;   // the game's own, set at Start
            if (!IsDyingOnOwner(creature) || creature!.m_lastHit == null)
            {
                return;
            }
            int extra = Mathf.RoundToInt(PlayerStats.OfAttacker(creature.m_lastHit, PlayerStats.Butcher));
            if (extra > 0 && IsAnimal(creature))
            {
                AddExtra(__result, extra);
            }
        }

        // On the creature's owner, during its death: the game only rolls loot there, at zero health.
        private static bool IsDyingOnOwner(Character? creature) =>
            creature != null && !creature.IsPlayer() && !creature.IsBoss() && creature.m_nview != null
            && creature.m_nview.IsValid() && creature.m_nview.IsOwner() && creature.GetHealth() <= 0f;

        private static bool IsAnimal(Character creature) =>
            creature.GetFaction() == Character.Faction.AnimalsVeg || creature.GetComponent<Tameable>() != null;

        private static void AddExtra(List<KeyValuePair<GameObject, int>> drops, int extra)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                if (AnimalProduce.IsMeatOrHide(drops[i].Key))
                {
                    drops[i] = new KeyValuePair<GameObject, int>(drops[i].Key, drops[i].Value + extra);
                }
            }
        }
    }

    /// <summary>
    /// Which drops are meat or hide. Meat: any item a cooking station in the game cooks (read once per ZNetScene, at the
    /// end of its Awake after other mods' registrations, so every mod's meat on a registered cooking station counts;
    /// a scene not read then is read at the first drop check). Hide: a material whose prefab name says
    /// hide, pelt or leather (DeerHide, TrollHide, LoxPelt, WolfPelt, LeatherScraps; judgement call: the game marks
    /// hides no other way). Trophies, fangs, feathers and the rest are never multiplied.
    /// </summary>
    [HarmonyPatch]
    internal static class AnimalProduce
    {
        private static readonly string[] HideWords = { "hide", "pelt", "leather" };
        private static readonly HashSet<string> Meats = new HashSet<string>(StringComparer.Ordinal);
        private static ZNetScene? _scene;

        public static bool IsMeatOrHide(GameObject? prefab)
        {
            if (prefab == null)
            {
                return false;
            }
            RefreshMeats();
            return Meats.Contains(prefab.name) || IsHide(prefab);
        }

        private static bool IsHide(GameObject prefab)
        {
            ItemDrop? item = prefab.GetComponent<ItemDrop>();
            if (item == null || item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Material)
            {
                return false;
            }
            foreach (string word in HideWords)
            {
                if (prefab.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void SceneAwake()
        {
            try
            {
                RefreshMeats();
            }
            catch (Exception e)
            {
                // A throw here would stop ZNetScene from loading the world; the first drop check tries again.
                _scene = null;
                Core.Log.Warn($"meat list not read at scene start: {e.Message}");
            }
        }

        // Once per ZNetScene (one per session): every cooking station's raw inputs.
        private static void RefreshMeats()
        {
            ZNetScene? scene = ZNetScene.instance;
            if (scene == null || ReferenceEquals(scene, _scene))
            {
                return;
            }
            _scene = scene;
            Meats.Clear();
            foreach (GameObject prefab in scene.m_prefabs)
            {
                CookingStation? station = prefab != null ? prefab.GetComponent<CookingStation>() : null;
                if (station != null)
                {
                    AddInputs(station);
                }
            }
        }

        private static void AddInputs(CookingStation station)
        {
            foreach (CookingStation.ItemConversion conversion in station.m_conversion)
            {
                if (conversion?.m_from != null)
                {
                    Meats.Add(conversion.m_from.gameObject.name);
                }
            }
        }
    }
}
