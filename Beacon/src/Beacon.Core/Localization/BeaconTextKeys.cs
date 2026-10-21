// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the localization keys Beacon uses (tokens meadworks_beacon_<key>, design section 11). Server names are
// shown as saved and are not keys. No text ever carries a password.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Beacon.Localization
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
    public static class BeaconTextKeys
    {
        public const string StepAsideJoin = "step_aside_join";
        public const string StorageWindows = "storage_windows";
        public const string StorageKeyring = "storage_keyring";
        public const string StorageSession = "storage_session";
        public const string StorageDisabled = "storage_disabled";
        public const string SecretStoreFailed = "secret_store_failed";
        public const string SecretStoredElsewhere = "secret_stored_elsewhere";
        public const string OfferSave = "offer_save";
        public const string PasswordSaved = "password_saved";
        public const string PasswordForgotten = "password_forgotten";
        public const string PasswordsForgottenAll = "passwords_forgotten_all";
        public const string AutoJoinCountdown = "autojoin_countdown";
        public const string AutoJoinCancelled = "autojoin_canceled";
        public const string AutoJoinUnknownServer = "autojoin_unknown_server";
        public const string Reconnecting = "reconnecting";
        public const string ReconnectLocked = "reconnect_locked";
        public const string FavoriteRemoved = "favorite_removed";
        public const string ImportNone = "import_none";
        public const string ImportDone = "import_done";
        public const string ImportPasswordNotStored = "import_password_not_stored";
        public const string ImportSourcePlain = "import_source_plain";
        public const string ImportBlankDone = "import_blank_done";
        public const string CommandHelp = "cmd_help";
        public const string CommandStatus = "cmd_status";
        public const string CommandUsage = "cmd_usage";
        public const string CommandUnknownVerb = "cmd_unknown_verb";
        public const string CommandConfirmBlank = "cmd_confirm_blank";
        public const string CommandJoinUnknown = "cmd_join_unknown";
        public const string CommandJoinNotReady = "cmd_join_not_ready";
        public const string CommandJoinStarted = "cmd_join_started";
        public const string ReconnectCancelled = "reconnect_canceled";
        public const string ReconnectStopped = "reconnect_stopped";
        public const string CommandCancelNone = "cmd_cancel_none";
        public const string AutoJoinUnknownCharacter = "autojoin_unknown_character";
        public const string CardAutoJoin = "card_autojoin";
        public const string CardReconnect = "card_reconnect";
        public const string CardCancelHint = "card_cancel_hint";

        public static IReadOnlyList<TextKey> All { get; } = Array.AsReadOnly(new[]
        {
            new TextKey(StepAsideJoin, 1),
            new TextKey(StorageWindows, 0), new TextKey(StorageKeyring, 0), new TextKey(StorageSession, 0), new TextKey(StorageDisabled, 0),
            new TextKey(SecretStoreFailed, 0), new TextKey(SecretStoredElsewhere, 1),
            new TextKey(OfferSave, 1), new TextKey(PasswordSaved, 1), new TextKey(PasswordForgotten, 1), new TextKey(PasswordsForgottenAll, 1),
            new TextKey(AutoJoinCountdown, 2), new TextKey(AutoJoinCancelled, 0), new TextKey(AutoJoinUnknownServer, 1),
            new TextKey(Reconnecting, 2), new TextKey(ReconnectLocked, 0),
            new TextKey(FavoriteRemoved, 1),
            new TextKey(ImportNone, 0), new TextKey(ImportDone, 2), new TextKey(ImportPasswordNotStored, 1), new TextKey(ImportSourcePlain, 0),
            new TextKey(ImportBlankDone, 1),
            new TextKey(CommandHelp, 0), new TextKey(CommandStatus, 3), new TextKey(CommandUsage, 0), new TextKey(CommandUnknownVerb, 1),
            new TextKey(CommandConfirmBlank, 0),
            new TextKey(CommandJoinUnknown, 1), new TextKey(CommandJoinNotReady, 0), new TextKey(CommandJoinStarted, 1),
            new TextKey(ReconnectCancelled, 0), new TextKey(ReconnectStopped, 1), new TextKey(CommandCancelNone, 0), new TextKey(AutoJoinUnknownCharacter, 1),
            new TextKey(CardAutoJoin, 2), new TextKey(CardReconnect, 3), new TextKey(CardCancelHint, 0),
            new TextKey("ui_servers", 0), new TextKey("ui_previous", 0), new TextKey("ui_next", 0),
            new TextKey("ui_add", 0), new TextKey("ui_edit", 0), new TextKey("ui_join", 0), new TextKey("ui_remove", 0),
            new TextKey("ui_up", 0), new TextKey("ui_down", 0), new TextKey("ui_default", 0), new TextKey("ui_close", 0),
            new TextKey("ui_forget", 0), new TextKey("ui_forget_all", 0), new TextKey("ui_confirm", 0), new TextKey("ui_cancel", 0),
            new TextKey("ui_save", 0), new TextKey("ui_name", 0), new TextKey("ui_address", 0), new TextKey("ui_aliases", 0),
            new TextKey("ui_password", 0), new TextKey("ui_password_hint", 0), new TextKey("ui_stored", 0), new TextKey("ui_empty", 0),
            new TextKey("ui_read_only", 0), new TextKey("ui_step_aside", 0), new TextKey("ui_write_failed", 0), new TextKey("ui_invalid", 0),
            new TextKey("ui_forget_all_confirm", 0), new TextKey("ui_alias_count", 1), new TextKey("ui_remove_confirm", 1), new TextKey("ui_forget_confirm", 1),
            new TextKey("ui_import", 0),
            new TextKey("import_preview_again", 0), new TextKey("import_write_failed", 0), new TextKey("import_password_pending", 0),
            new TextKey("ui_import_apply", 0), new TextKey("ui_import_blank", 0),
            new TextKey("ui_import_blank_confirm", 0),
            new TextKey("ui_import_results", 0),
            new TextKey("ui_undo", 0), new TextKey("ui_undo_conflict", 0), new TextKey("ui_favorite_save_failed", 0),
            new TextKey("ui_help_servers", 0),
            new TextKey("ui_help_previous", 0),
            new TextKey("ui_help_next", 0),
            new TextKey("ui_help_add", 0),
            new TextKey("ui_help_edit", 0),
            new TextKey("ui_help_join", 0),
            new TextKey("ui_help_remove", 0),
            new TextKey("ui_help_up", 0),
            new TextKey("ui_help_down", 0),
            new TextKey("ui_help_default", 0),
            new TextKey("ui_help_close", 0),
            new TextKey("ui_help_forget", 0),
            new TextKey("ui_help_forget_all", 0),
            new TextKey("ui_help_import", 0),
            new TextKey("ui_help_select", 0),
            new TextKey("ui_help_cancel", 0),
            new TextKey("ui_help_save", 0),
            new TextKey("ui_help_name", 0),
            new TextKey("ui_help_address", 0),
            new TextKey("ui_help_aliases", 0),
            new TextKey("ui_help_save_password", 0),
            new TextKey("ui_help_undo", 0),
            new TextKey("ui_help_import_apply", 0),
        });
    }
}
