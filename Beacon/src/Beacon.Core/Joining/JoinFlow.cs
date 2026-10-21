// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original, behavior only: sending the stored password, saving on accept and re-asking after a rejection follow
// AutoConnector 1.0.0's README; asking once before saving an unknown server is a Meadworks change (design F3). No code read.
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Joining
{
    public enum JoinAction
    {
        /// <summary>Nothing for Beacon to do.</summary>
        None,
        /// <summary>Answer the game's password request with the stored password; the prompt is not shown (F2).</summary>
        SendStored,
        /// <summary>Let the game show its password prompt.</summary>
        AskPlayer,
        /// <summary>The stored password was rejected: reconnect and ask the player (F4).</summary>
        ReconnectAndAsk,
        /// <summary>The server accepted the password the player typed for a saved server: store it (replacing any old one).</summary>
        StoreTyped,
        /// <summary>The server accepted a typed password for an unsaved server: ask once whether to save it (F3).</summary>
        OfferSave,
    }

    /// <summary>
    /// The password part of one connection attempt (design F2 to F4). The plugin feeds it the game's events and acts on the
    /// returned <see cref="JoinAction"/>; the password itself never passes through this class.
    /// </summary>
    public sealed class JoinAttempt
    {
        readonly bool hasStored;
        readonly bool skipPrompt;
        readonly bool offerToSave;
        readonly bool alreadyOffered;
        readonly bool storedRejected;
        bool sentStored;
        bool typed;

        /// <param name="entry">The saved server this address belongs to, or null.</param>
        /// <param name="hasStored">The secret store holds a password for <paramref name="entry"/>.</param>
        /// <param name="skipPrompt">Setting Skip Password Prompt.</param>
        /// <param name="offerToSave">Setting Offer To Save Passwords.</param>
        /// <param name="alreadyOffered">Beacon already asked about this address (once per server).</param>
        public JoinAttempt(ServerEntry? entry, bool hasStored, bool skipPrompt, bool offerToSave, bool alreadyOffered)
            : this(entry, hasStored, skipPrompt, offerToSave, alreadyOffered, false)
        {
        }

        JoinAttempt(ServerEntry? entry, bool hasStored, bool skipPrompt, bool offerToSave, bool alreadyOffered, bool storedRejected)
        {
            Entry = entry;
            this.hasStored = hasStored;
            this.skipPrompt = skipPrompt;
            this.offerToSave = offerToSave;
            this.alreadyOffered = alreadyOffered;
            this.storedRejected = storedRejected;
        }

        public ServerEntry? Entry { get; }

        public JoinAction OnPasswordRequested()
        {
            if (Entry != null && hasStored && skipPrompt && !storedRejected)
            {
                sentStored = true;
                return JoinAction.SendStored;
            }
            return JoinAction.AskPlayer;
        }

        public void OnPlayerTyped() => typed = true;

        public JoinAction OnRejected()
        {
            if (sentStored)
            {
                sentStored = false;
                return JoinAction.ReconnectAndAsk;
            }
            typed = false;
            return JoinAction.None;
        }

        public JoinAction OnAccepted()
        {
            if (!typed) return JoinAction.None;
            if (Entry != null) return JoinAction.StoreTyped;
            return offerToSave && !alreadyOffered ? JoinAction.OfferSave : JoinAction.None;
        }

        /// <summary>The reconnect after <see cref="JoinAction.ReconnectAndAsk"/>: the stored password is not sent again.</summary>
        public JoinAttempt AfterRejection() => new JoinAttempt(Entry, hasStored, skipPrompt, offerToSave, alreadyOffered, true);
    }
}
