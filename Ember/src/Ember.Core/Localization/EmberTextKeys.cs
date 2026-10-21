// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Ember uses (tokens meadworks_ember_<key>, design section 11).
using System;
using System.Collections.Generic;

namespace ExtraPotions.Ember.Localization
{
    public readonly struct TextKey
    {
        public TextKey(string key, int arguments)
        {
            Key = key;
            Arguments = arguments;
        }

        public string Key { get; }

        /// <summary>Number of <c>{0}</c>-style placeholders, numbered from 0.</summary>
        public int Arguments { get; }
    }

    /// <summary>Every key of assets/localization/*.json; the tests hold the catalog to this table.</summary>
    public static class EmberTextKeys
    {
        public const string StepAsideSleep = "step_aside_sleep";
        public const string StepAsideTorches = "step_aside_torches";
        public const string StepAsideDoors = "step_aside_doors";
        public const string OffNoCrucible = "off_no_crucible";
        public const string OffNoEmber = "off_no_ember";
        public const string DoorsOn = "doors_on";
        public const string DoorsOff = "doors_off";
        public const string DoorsNotAllowed = "doors_not_allowed";
        public const string PresetLocked = "preset_locked";
        public const string HudSleeping = "hud_sleeping";
        public const string HudBoost = "hud_boost";
        public const string ImportDone = "import_done";
        public const string CheatSleepAnyTime = "cheat_sleep_any_time";
        public const string CheatTestingMode = "cheat_testing_mode";
        public const string CheatEndlessTorches = "cheat_endless_torches";
        public const string TorchNightfall = "torch_nightfall";
        public const string TorchKeep = "torch_keep";
        public const string TorchSchedule = "torch_schedule";
        public const string TorchKept = "torch_kept";
        public const string TorchScheduled = "torch_scheduled";
        public const string TorchRefused = "torch_refused";
        public const string BedChoiceCountdown = "bed_choice_countdown";
        public const string BedChoiceNearest = "bed_choice_nearest";
        public const string BedChoiceClick = "bed_choice_click";
        public const string BedChoiceKeys = "bed_choice_keys";

        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(new[]
        {
            new TextKey(StepAsideSleep, 1),
            new TextKey(StepAsideTorches, 1),
            new TextKey(StepAsideDoors, 1),
            new TextKey(OffNoCrucible, 0),
            new TextKey(OffNoEmber, 0),
            new TextKey(DoorsOn, 0),
            new TextKey(DoorsOff, 0),
            new TextKey(DoorsNotAllowed, 0),
            new TextKey(PresetLocked, 0),
            new TextKey(HudSleeping, 2),
            new TextKey(HudBoost, 1),
            new TextKey(ImportDone, 2),
            new TextKey(CheatSleepAnyTime, 0),
            new TextKey(CheatTestingMode, 0),
            new TextKey(CheatEndlessTorches, 0),
            new TextKey(TorchNightfall, 0),
            new TextKey(TorchKeep, 0),
            new TextKey(TorchSchedule, 0),
            new TextKey(TorchKept, 0),
            new TextKey(TorchScheduled, 0),
            new TextKey(TorchRefused, 0),
            new TextKey(BedChoiceCountdown, 1),
            new TextKey(BedChoiceNearest, 0),
            new TextKey(BedChoiceClick, 0),
            new TextKey(BedChoiceKeys, 0),
        });
    }
}
