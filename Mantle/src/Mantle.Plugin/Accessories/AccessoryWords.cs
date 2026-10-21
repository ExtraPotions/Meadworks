// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Mantle.Core;
using Mantle.Tackle;


namespace Mantle.Accessories
{
    /// <summary>
    /// The words of the ring and belt slots and items, in the seven languages of Mantle's C05 catalogs (assets/localization):
    /// the two slot captions, the "no room" message and each item's name and description. They are handed to the game's
    /// localization as $mantle_ tokens in the language in use, written again when the registry changes (a description says what
    /// the item gives now) and when the game's language changes (<see cref="Tick"/>). The game keeps no words of ours across
    /// a language change, and words added before its localization exists are lost, so the check runs every frame and costs two comparisons.
    /// A tacklebox whose name has a catalog entry (name_tacklebox_ + its word, the Deep North box) is named here too; the
    /// other tackleboxes keep the English names <see cref="TackleboxWords"/> gives them.
    /// </summary>
    public static class AccessoryWords
    {
        private static string shownLanguage;
        private static Localization shownInstance;

        public static string NoRoom { get; private set; } = "$mantle_accessory_noroom";

        public static void Register()
        {
            shownLanguage = null;
            shownInstance = null;
            DescribeAll();
        }

        /// <summary>Every frame: the words follow the game's language.</summary>
        public static void Tick()
        {
            string language = AccessoryText.Language;
            Localization instance = Localization.m_instance;
            if (language != null && instance != null && (language != shownLanguage || !ReferenceEquals(instance, shownInstance)))
                DescribeAll();
        }

        public static void DescribeAll()
        {
            if (AccessoryText.Language == null)
                return;
            if (Localization.m_instance != null)
            {
                // Words handed over before the game's localization exists are not installed; they are written again once it does.
                shownLanguage = AccessoryText.Language;
                shownInstance = Localization.m_instance;
            }
            Language.Add("mantle_ring", AccessoryText.Text("slot_ring"));
            Language.Add("mantle_belt", AccessoryText.Text("slot_belt"));
            NoRoom = Language.Add("mantle_accessory_noroom", AccessoryText.Text("accessory_noroom"));
            foreach (AccessoryKind kind in AccessoryKinds.All)
            {
                Language.Add(kind.Token.Substring(1), AccessoryText.Text("name_" + kind.Word));
                Language.Add(kind.DescriptionToken.Substring(1), Describe(kind));
            }
            foreach (TackleboxKind box in TackleboxCatalog.All)
            {
                string key = "name_tacklebox_" + box.Word;
                string name = AccessoryText.Text(key);
                if (name != key)
                    Language.Add(box.Token.Substring(1), name);
            }
        }

        /// <summary>The item's look, where it goes, and one line for each thing it gives (the registry's numbers).</summary>
        public static string Describe(AccessoryKind kind)
        {
            AccessoryEntry entry = kind.Entry;
            List<string> lines = new List<string>
            {
                AccessoryText.Text("look_" + kind.Word),
                AccessoryText.Text(entry.Slot == AccessorySlot.Ring ? "hint_ring" : "hint_belt"),
            };
            foreach (StatLine line in AccessoryDescription.Lines(entry.Stats))
                lines.Add(AccessoryText.Format(line.Key, line.Number));
            return string.Join("\n", lines);
        }
    }
}
