// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: when an opened container is converted (Inscribe 0.2.0). A remote opener's ownership of the chest's data
// arrives after the window opens, so the conversion waits for it; each opening converts at most once.

namespace ExtraPotions.Inscribe.Enhancement
{
    /// <summary>
    /// Decides, per opened container, the one moment its contents convert: at the opening when this machine already owns
    /// the container, otherwise on the first frame it does while the same container is still open. Closing it or opening
    /// another drops the wait. Containers are identified by an id the game adapter chooses (the instance id).
    /// </summary>
    public sealed class OpenContainerConversion
    {
        private int? waiting;

        /// <summary>The id still waiting for ownership, or null.</summary>
        public int? Waiting => waiting;

        /// <summary>A container window opened; true when it converts now.</summary>
        public bool Opened(int id, bool owned)
        {
            waiting = owned ? (int?)null : id;
            return owned;
        }

        /// <summary>Each frame: the open container's id (null when none is open) and whether this machine owns it; true when it converts now.</summary>
        public bool Frame(int? openId, bool owned)
        {
            if (waiting == null) return false;
            if (openId != waiting)
            {
                waiting = null;
                return false;
            }
            if (!owned) return false;
            waiting = null;
            return true;
        }

        public void Reset() => waiting = null;
    }
}
