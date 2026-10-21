// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), MIT. Adapted from DoorOpenerBruh 2.0.10 at
// dafb2af2418710dd1f87907e8b30d4efb1b68a20, DoorOpenerBruh/Components/DoorStatus.cs (CheckPlayerPositionForDoors:
// hysteresis, auto-opened tracking, inverted open and closed text, SetState close).
// The MIT notice is retained in THIRD_PARTY_NOTICES/DoorOpenerBruh-MIT.txt.
// Changed by expDARE, 2026: one portable tracker per door returning an action (the door manager calls Door.Interact
// or sends UseDoor); close only when the door is still open; D11 Keep Open While Others Near (no close while another
// player is within Close Distance, the close waits until they leave; no close after someone else changed the state);
// a door the automation may not open is not adopted while shut, so a key picked up at the door opens it at once;
// namespace Ember.
namespace ExtraPotions.Ember.Doors
{
    /// <summary>What the door manager does for one door this check.</summary>
    public enum DoorAction
    {
        None,

        /// <summary>Open through the game's Door.Interact (key, ward and CanInteract apply as for a key press).</summary>
        Open,

        /// <summary>Send the game's UseDoor with <see cref="DoorTracker.CloseState"/>.</summary>
        Close,
    }

    /// <summary>D4: door state values (ZDOVars.s_state) and the inverted open and closed text rule.</summary>
    public static class DoorState
    {
        /// <summary>Open: state not 0 for a normal door (1 or -1, one per swing side); state 0 for an inverted-text door.</summary>
        public static bool IsOpen(int state, bool invertedText) => invertedText ? state == 0 : state != 0;

        public static int ClosedState(bool invertedText) => invertedText ? 1 : 0;
    }

    /// <summary>One check's inputs for a door, from the door manager.</summary>
    public readonly struct DoorTick
    {
        public double Distance { get; }
        public float OpenDistance { get; }
        public float CloseDistance { get; }

        /// <summary><see cref="DoorRules.AutomationEnabled"/> for this door and player.</summary>
        public bool AutomationEnabled { get; }

        public int State { get; }
        public bool InvertedText { get; }

        /// <summary>Doors.Keep Open While Others Near (D11).</summary>
        public bool KeepOpenWhileOthersNear { get; }

        /// <summary>Another player (from the game's loaded player list) is within this door's Close Distance.</summary>
        public bool OtherPlayerWithinClose { get; }

        public DoorTick(double distance, float openDistance, float closeDistance, bool automationEnabled, int state, bool invertedText,
            bool keepOpenWhileOthersNear, bool otherPlayerWithinClose)
        {
            Distance = distance;
            OpenDistance = openDistance;
            CloseDistance = closeDistance;
            AutomationEnabled = automationEnabled;
            State = state;
            InvertedText = invertedText;
            KeepOpenWhileOthersNear = keepOpenWhileOthersNear;
            OtherPlayerWithinClose = otherPlayerWithinClose;
        }
    }

    /// <summary>D1, D4, D5, D11: per-door open and close decisions for the local player.</summary>
    public sealed class DoorTracker
    {
        /// <summary>The player is inside the door's hysteresis band (entered at Open, left over Close).</summary>
        public bool InRange { get; private set; }

        /// <summary>The automation owns this door's close: it opened it, or found it open on approach (D5).</summary>
        public bool AutoOpened { get; private set; }

        /// <summary>The open state seen after this client took the door; a different state later means someone else moved it.</summary>
        public int? ExpectedState { get; private set; }

        /// <summary>Someone else changed the door's state since this client took it (D11).</summary>
        public bool ForeignChange { get; private set; }

        public static int CloseState(bool invertedText) => DoorState.ClosedState(invertedText);

        public DoorAction Step(DoorTick tick)
        {
            var (open, close) = DoorDistances.Normalize(tick.OpenDistance, tick.CloseDistance);
            if (InRange)
            {
                if (tick.Distance > close)
                    InRange = false;
            }
            else if (tick.Distance <= open)
            {
                InRange = true;
            }

            bool isOpen = DoorState.IsOpen(tick.State, tick.InvertedText);
            if (AutoOpened)
            {
                if (ExpectedState == null)
                {
                    if (isOpen)
                        ExpectedState = tick.State;
                }
                else if (tick.State != ExpectedState.Value)
                {
                    ForeignChange = true;
                }
            }

            if (InRange)
            {
                if (tick.AutomationEnabled && !isOpen)
                {
                    if (AutoOpened)
                        return DoorAction.None;
                    Take(null);
                    return DoorAction.Open;
                }
                // A shut door the automation may not open now (no key yet, a ward) is not adopted: if the reason goes
                // away while the player stands here (a key enters the inventory), the next check opens it.
                if (!AutoOpened && isOpen)
                    Take(tick.State);
                return DoorAction.None;
            }

            if (!AutoOpened)
                return DoorAction.None;

            bool keepRules = tick.KeepOpenWhileOthersNear;
            if (!tick.AutomationEnabled || !isOpen || (keepRules && ForeignChange))
            {
                Release();
                return DoorAction.None;
            }
            if (keepRules && tick.OtherPlayerWithinClose)
                return DoorAction.None;
            Release();
            return DoorAction.Close;
        }

        /// <summary>Forget the door (player changed, door unloaded, module turned off).</summary>
        public void Reset()
        {
            InRange = false;
            Release();
        }

        void Take(int? expected)
        {
            AutoOpened = true;
            ExpectedState = expected;
            ForeignChange = false;
        }

        void Release()
        {
            AutoOpened = false;
            ExpectedState = null;
            ForeignChange = false;
        }
    }
}
