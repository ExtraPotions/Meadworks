// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Almanac.Writing
{
    public enum WriteVerdict { Write, NoChange, RefusedInvalid, RefusedLocked }

    /// <summary>What happens to the file after a value is assigned.</summary>
    public enum SaveAction
    {
        None,
        /// <summary><c>ConfigFile.SaveOnConfigSet</c> is on: the file saves itself, the mod's own behavior with any manager.</summary>
        AutoSaveByFile,
        /// <summary>Save is off: the group shows "Not saved" with a Save button.</summary>
        MarkDirty,
        /// <summary>Save is off and the file holds server-provided values while connected: Almanac never saves it.</summary>
        SaveSuppressed,
        /// <summary>A synced entry written through its sync system's admin path: that system owns persistence.</summary>
        OwnedBySyncSystem,
        /// <summary>The admitted own entry is saved against the disk baseline, preserving other live server overlays.</summary>
        SaveEntry,
    }

    public enum HandlerOutcome { Keep, RestorePrevious, MarkRejected }

    /// <summary>Facts the plugin's <c>EntryWriter</c> gathers immediately before a write.</summary>
    public sealed class WriteRequest
    {
        /// <summary>Lock state (design section 5) allows this player to write now.</summary>
        public bool Editable { get; set; }
        public string? LockReason { get; set; }
        /// <summary>The new value parsed and passed acceptable-value validation.</summary>
        public bool ValueValid { get; set; }
        public string? Error { get; set; }
        public bool SameAsCurrent { get; set; }
        /// <summary>This process is a remote client of a server (not menu, single player or host).</summary>
        public bool ConnectedAsClient { get; set; }
        /// <summary>The file holds at least one entry whose current value comes from a server.</summary>
        public bool FileHasSyncedEntries { get; set; }
        public bool EntrySynced { get; set; }
        public bool SaveOnConfigSet { get; set; }
        public bool PersistOwnValue { get; set; }
    }

    public sealed class WriteDecision
    {
        internal WriteDecision(WriteVerdict verdict, SaveAction save, string? reason)
        {
            Verdict = verdict;
            Save = save;
            Reason = reason;
        }

        public WriteVerdict Verdict { get; }
        public SaveAction Save { get; }
        public string? Reason { get; }
        public override string ToString() => Verdict + " / " + Save + (Reason == null ? "" : " (" + Reason + ")");
    }

    /// <summary>
    /// Pure write rules (design 4.3): validation, then the lock check, then the save rule. A client never saves a file
    /// while it holds server-provided values; the sync system owns persistence there.
    /// </summary>
    public static class EntryWritePolicy
    {
        public static WriteDecision Decide(WriteRequest request)
        {
            if (!request.ValueValid) return new WriteDecision(WriteVerdict.RefusedInvalid, SaveAction.None, request.Error ?? "Invalid value");
            if (!request.Editable) return new WriteDecision(WriteVerdict.RefusedLocked, SaveAction.None, request.LockReason ?? "Locked");
            if (request.SameAsCurrent) return new WriteDecision(WriteVerdict.NoChange, SaveAction.None, null);
            SaveAction save;
            if (request.ConnectedAsClient && request.EntrySynced) save = SaveAction.OwnedBySyncSystem;
            else if (request.PersistOwnValue) save = SaveAction.SaveEntry;
            else if (request.SaveOnConfigSet) save = SaveAction.AutoSaveByFile;
            else save = MaySaveFile(request.ConnectedAsClient, request.FileHasSyncedEntries) ? SaveAction.MarkDirty : SaveAction.SaveSuppressed;
            return new WriteDecision(WriteVerdict.Write, save, null);
        }

        /// <summary>Almanac calls <c>ConfigFile.Save()</c> only when the file holds no server-provided value.</summary>
        public static bool MaySaveFile(bool connectedAsClient, bool fileHasSyncedEntries) => !(connectedAsClient && fileHasSyncedEntries);

        /// <summary>"Changed on disk: Reload" is disabled for files with synced entries while connected (design 7.3).</summary>
        public static bool MayReload(bool connectedAsClient, bool fileHasSyncedEntries) => MaySaveFile(connectedAsClient, fileHasSyncedEntries);

        /// <summary>Profiles and the converter never capture or write a server-provided value.</summary>
        public static bool MayCaptureForProfile(bool connectedAsClient, bool entrySynced) => !(connectedAsClient && entrySynced);

        /// <summary>The owning mod's <c>SettingChanged</c> handler threw: restore the previous value once, then mark the row rejected.</summary>
        public static HandlerOutcome AfterAssign(bool handlerThrew, bool restoreAlreadyTried)
        {
            if (!handlerThrew) return HandlerOutcome.Keep;
            return restoreAlreadyTried ? HandlerOutcome.MarkRejected : HandlerOutcome.RestorePrevious;
        }
    }
}
