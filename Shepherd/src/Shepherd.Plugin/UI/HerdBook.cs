// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Uses Crucible's existing PackPanel window/stat sheet/buttons and the native tame naming interaction.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Runtime.Breeding;
using ExtraPotions.Shepherd.Runtime.Integration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.UI
{
    internal static class HerdBook
    {
        const int PageSize = 8;
        // The MeadWorks menu (F8, Esc > MeadWorks) lists the herd book; Herd Book Key stays unbound by default.
        const string MenuId = "shepherd";
        static IDisposable? menuEntry;
        static KitWindow? window;
        static KitStatSheet? sheet;
        static KitEmptyState? empty;
        static TMP_Text? title, count;
        static Button? previous, next, rename, close;
        static readonly List<Button> buttons = new List<Button>();
        static readonly List<Tameable> animals = new List<Tameable>();
        static ZDOID selected;
        static Player? actor;
        static ZNet? network;
        static long generation = -1;
        static int page;
        static float refresh;
        static string language = "";
        internal static bool Active => Plugin.Ready && Plugin.AllowsUi && NativeHusbandry.Active
            && Player.m_localPlayer != null && !Player.m_localPlayer.IsDead()
            && Player.m_localPlayer.m_nview != null && Player.m_localPlayer.m_nview.IsOwner();
        static string Text(string key) => ShepherdLocalization.Text(key);
        static string Local(string token) => LoreText.Literal(global::Localization.instance != null ? global::Localization.instance.Localize(token) : token);
        static bool Valid(Tameable tameable) => tameable != null && tameable.IsTamed() && tameable.m_character != null
            && !tameable.m_character.IsDead() && tameable.m_nview != null && tameable.m_nview.IsValid()
            && Player.m_localPlayer != null && (tameable.transform.position - Player.m_localPlayer.transform.position).sqrMagnitude
                <= ShepherdSettings.BookRange.Value * ShepherdSettings.BookRange.Value;
        static Tameable? Selection() => animals.FirstOrDefault(t => Valid(t) && t.m_nview.GetZDO().m_uid == selected);
        internal static void Tick()
        {
            if (Plugin.Ready && Plugin.AllowsUi)
                menuEntry ??= MeadworksMenu.Register(MenuId, () => Text("book_title"), OpenFromMenu, () => Active && KitCanvas.Font != null);
            if (!Active) { Clear(); return; }
            if (actor != Player.m_localPlayer || network != ZNet.instance || generation != CruciblePlugin.Session.Generation
                || window != null && window.Panel != null && window.Panel.parent != KitCanvas.Front) Clear();
            actor = Player.m_localPlayer; network = ZNet.instance; generation = CruciblePlugin.Session.Generation;
            var context = window?.IsOpen == true ? InputContext.Window : InputContext.Gameplay;
            var ownedPanel = window?.Panel;
            bool foreground = window == null || !window.IsOpen || ownedPanel != null && ownedPanel.parent != null && ownedPanel.GetSiblingIndex() == ownedPanel.parent.childCount - 1;
            if (foreground && KitAlmanacDialog.Active == null && (MeadworksInput.Pressed(ShepherdSettings.BookKey.Entry, context)
                || MeadworksInput.Allows(context) && PadPressed(ShepherdSettings.BookPadChord.Value)))
            { if (window?.IsOpen == true) window.Close(); else Open(); }
            if (window?.IsOpen != true) return;
            if (Time.unscaledTime >= refresh || language != MeadworksBlockedOptions.Language)
            { refresh = Time.unscaledTime + 1; language = MeadworksBlockedOptions.Language; Scan(); Draw(); }
            var selectedAnimal = Selection();
            if (rename != null) rename.interactable = CanRename(selectedAnimal);
            sheet?.Refresh(selectedAnimal != null ? selectedAnimal.m_nview.GetZDO().m_uid.GetHashCode() : 0, Fill);
        }
        static bool PadPressed(string text)
        {
            if (!MeadworksInput.GamepadActive || string.IsNullOrWhiteSpace(text) || text.Length > 128) return false;
            var parsed = text.Split('+').Select(s => s.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            var buttons = new List<PadButton>();
            foreach (var name in parsed) { if (!GamepadMap.TryParse(name, out var button)) return false; buttons.Add(button); }
            if (buttons.Count == 0 || !buttons.All(MeadworksInput.PadHeld) || !buttons.Any(MeadworksInput.PadDown)) return false;
            foreach (var button in buttons) MeadworksInput.PadConsume(button);
            return true;
        }
        static void OpenFromMenu()
        {
            if (Menu.IsVisible()) Menu.instance?.OnClose();
            if (window?.IsOpen != true) Open();
        }
        internal static void Open()
        {
            if (!Active || KitCanvas.Font == null || MeadworksInput.Typing || Menu.IsVisible()) return;
            if (window == null)
            {
                window = KitWindow.Create("Shepherd_HerdBook", new Vector2(850, 570));
                if (window == null) return;
                title = KitAlmanacDialog.Label(window.Panel, "Title", "", new Vector2(20, -15), new Vector2(800, 34), 23);
                if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
                count = KitAlmanacDialog.Label(window.Panel, "Count", "", new Vector2(20, -52), new Vector2(800, 30), 15);
                for (int i = 0; i < PageSize; i++)
                {
                    int index = i;
                    var button = KitButton.Create(window.Panel, "Animal" + i, "", () => Select(index),
                        new Vector2(20, -90 - i * 48), new Vector2(375, 44));
                    buttons.Add(button); window.Focusable(button, 0, i);
                    foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) { label.richText = false; label.fontSize = 13; }
                }
                // The painted empty state fills the list and sheet area while no animal is near.
                var area = new GameObject("EmptyArea", typeof(RectTransform)).GetComponent<RectTransform>();
                area.gameObject.layer = window.Panel.gameObject.layer;
                area.SetParent(window.Panel, false);
                area.anchorMin = area.anchorMax = area.pivot = new Vector2(0, 1);
                area.anchoredPosition = new Vector2(20, -90); area.sizeDelta = new Vector2(810, 385);
                empty = KitForged.EmptyState(area, "Empty", KitForgedArt.EmptyBoar, "", "", 220f);
                sheet = KitStatSheet.Create(window.Panel, "AnimalDetails", new Vector2(415, -90), new Vector2(415, 385));
                previous = KitButton.Create(window.Panel, "Previous", "", () => { page = Math.Max(0, page - 1); Draw(); }, new Vector2(20, -495), new Vector2(180, 42));
                next = KitButton.Create(window.Panel, "Next", "", () => { page++; Draw(); }, new Vector2(215, -495), new Vector2(180, 42));
                rename = KitButton.Create(window.Panel, "Rename", "", Rename, new Vector2(415, -495), new Vector2(200, 42));
                close = KitButton.Create(window.Panel, "Close", Text("book_close"), () => window?.Close(), new Vector2(630, -495), new Vector2(200, 42));
                window.Focusable(previous, 0, PageSize); window.Focusable(next, 1, PageSize);
                window.Focusable(rename, 2, PageSize); window.Focusable(close, 3, PageSize);
            }
            actor = Player.m_localPlayer; network = ZNet.instance; generation = CruciblePlugin.Session.Generation;
            Scan(); Draw(); window.Open();
        }
        static void Scan()
        {
            animals.Clear();
            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.IsPlayer()) continue;
                var tameable = Herd.TameableOf(character);
                if (tameable != null && Valid(tameable)) animals.Add(tameable);
            }
            animals.Sort((a,b) => Vector3.SqrMagnitude(a.transform.position-actor!.transform.position)
                .CompareTo(Vector3.SqrMagnitude(b.transform.position-actor!.transform.position)));
            if (Selection() == null) selected = animals.Count > 0 ? animals[0].m_nview.GetZDO().m_uid : ZDOID.None;
        }
        static string State(Tameable animal)
        {
            string status = Text(animal.IsHungry() ? "book_hungry" : "book_fed");
            var parent = animal.GetComponent<Procreation>();
            return parent != null && parent.IsPregnant() ? status + " · " + Text("book_pregnant") : status;
        }
        static string Variant(Tameable animal)
        {
            var equipment = animal.GetComponent<VisEquipment>();
            return equipment != null ? equipment.GetModelIndex().ToString() : Text("lore_unknown");
        }
        static void Draw()
        {
            if (window == null) return;
            page = Mathf.Clamp(page, 0, Math.Max(0, (animals.Count - 1) / PageSize));
            title!.text = Text("book_title");
            count!.text = ShepherdLocalization.Format("book_count", animals.Count, ShepherdSettings.BookRange.Value, page + 1, Math.Max(1, (animals.Count + PageSize - 1) / PageSize));
            for (int i = 0; i < buttons.Count; i++)
            {
                int at = page * PageSize + i;
                buttons[i].gameObject.SetActive(at < animals.Count);
                if (at >= animals.Count) continue;
                var animal = animals[at];
                string caption = LoreText.Literal(animal.GetHoverName()) + " · " + Local(animal.m_character.m_name) + "\n"
                    + ShepherdLocalization.Format("book_summary", Math.Max(0, animal.m_character.GetLevel() - 1), Variant(animal), State(animal));
                GameButtons.Caption(buttons[i].gameObject, caption);
                KitTooltip.Set(buttons[i].gameObject, Local(animal.m_character.m_name), Text("book_help_animal") + "\n" + State(animal));
            }
            bool none = animals.Count == 0;
            empty?.Words(Text("book_empty_title"), Text("book_empty_hint"));
            empty?.Show(none);
            if (sheet != null) sheet.Area.gameObject.SetActive(!none);
            rename!.gameObject.SetActive(!none);
            previous!.interactable = page > 0; next!.interactable = (page + 1) * PageSize < animals.Count;
            GameButtons.Caption(previous.gameObject, Text("book_previous")); GameButtons.Caption(next.gameObject, Text("book_next"));
            GameButtons.Caption(rename!.gameObject, Text("book_rename"));
            GameButtons.Caption(close!.gameObject, Text("book_close"));
            KitTooltip.Set(previous.gameObject, Text("book_previous"), Text("book_help_previous"));
            KitTooltip.Set(next.gameObject, Text("book_next"), Text("book_help_next"));
            KitTooltip.Set(close.gameObject, Text("book_close"), Text("book_help_close"));
            KitTooltip.Set(rename.gameObject, Text("book_rename"), Text("book_rename_reach"));
            sheet?.Invalidate();
        }
        static void Select(int index)
        {
            int at = page * PageSize + index;
            if (!Active || at < 0 || at >= animals.Count || !Valid(animals[at])) return;
            selected = animals[at].m_nview.GetZDO().m_uid; sheet?.Invalidate();
        }
        static bool CanRename(Tameable? animal) => Active && animal != null && Valid(animal)
            && Vector3.Distance(animal.transform.position, Player.m_localPlayer.transform.position) <= 5;
        static void Rename()
        {
            var animal = Selection();
            if (!CanRename(animal) || MeadworksInput.Typing || Menu.IsVisible()) return;
            // The native interaction keeps platform UGC privileges, name length/censoring and its owner-routed SetName RPC.
            animal!.Interact(Player.m_localPlayer, false, true);
        }
        static void Fill(KitSheet target)
        {
            var animal = Selection();
            if (animal == null) { target.Add(Text("book_empty"), ""); return; }
            target.Section(LoreText.Literal(animal.GetHoverName()));
            target.Add(Text("book_species"), Local(animal.m_character.m_name));
            target.Add(Text("book_stars"), Math.Max(0, animal.m_character.GetLevel()-1).ToString());
            target.Add(Text("book_variant"), Variant(animal)); target.Add(Text("book_state"), State(animal));
            var traits = WildbloodLabels(animal);
            if (traits.Length > 0) target.Add(Text("book_traits"), traits);
            var record = BreedingStore.ReadLineage(new ZdoKeyStore(animal.m_nview.GetZDO()));
            if (record == null) { target.Add(Text("book_lineage"), Text("lore_unknown")); return; }
            target.Section(Text("book_lineage"));
            target.Add(Text("book_generation"), record.Generation.ToString());
            target.Add(Text("book_born"), record.BornDay > 0 ? record.BornDay.ToString() : Text("lore_unknown"));
            target.Add(Text("book_breeder"), record.Breeder.Length > 0 ? LoreText.Literal(record.Breeder) : Text("lore_unknown"));
            Parent(target, "book_mother", record.MotherToken, record.MotherName);
            Parent(target, "book_sire", record.SireToken, record.SireName);
        }
        static string WildbloodLabels(Tameable animal)
        {
            const string id = "meadworks.creatures.trait_labels";
            try
            {
                var registry = CruciblePlugin.Services.Capabilities;
                var providers = registry.Providers(id);
                if (providers.Count != 1 || providers[0].Owner != "com.meadworks.wildblood"
                    || !registry.TryResolve<Func<object,string>>(id, new System.Version(1,0), out var read) || read == null) return "";
                string labels = read(animal.m_character) ?? "";
                labels = labels.Replace('<', '‹').Replace('>', '›').Replace('\n', ' ').Replace('\r', ' ');
                return labels.Length <= 1024 ? labels : labels.Substring(0,1024);
            }
            catch { return ""; }
        }
        static void Parent(KitSheet target, string key, string token, string name)
        {
            target.Add(Text(key), name.Length > 0 ? Local(name) : Text("lore_unknown"));
            if (token.Length == 0) return;
            // Loaded parents only; no world-wide scan/database or lineage creation from this UI.
            var matches = Character.GetAllCharacters().Where(character => character != null && character.m_nview != null
                && character.m_nview.IsValid() && character.m_nview.GetZDO().GetString(BreedingKeys.Instance) == token).Take(2).ToArray();
            if (matches.Length != 1) return;
            var parent = BreedingStore.ReadLineage(new ZdoKeyStore(matches[0].m_nview.GetZDO()));
            if (parent != null && (parent.MotherName.Length > 0 || parent.SireName.Length > 0))
                target.Add(Text("book_grandparents"), Local(parent.MotherName) + " / " + Local(parent.SireName));
        }
        internal static void Shutdown()
        {
            menuEntry?.Dispose(); menuEntry = null;
            Clear();
        }
        internal static void Clear()
        {
            var events = EventSystem.current;
            var focus = events != null ? events.currentSelectedGameObject : null;
            if (focus != null && window?.Panel != null && focus.transform.IsChildOf(window.Panel)) events!.SetSelectedGameObject(null);
            window?.Close(); window?.Destroy(); window = null; sheet = null; empty = null; title = count = null;
            previous = next = rename = close = null; buttons.Clear(); animals.Clear(); selected = ZDOID.None;
            actor = null; network = null; generation = -1; page = 0; refresh = 0; language = "";
        }
    }
}
