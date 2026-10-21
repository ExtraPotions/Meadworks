// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: retains one authenticated server decision until reconnect is decided after disconnect.
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Joining
{
    public sealed class ReconnectServerPolicy
    {
        public ServerAddress? Target { get; private set; }
        bool denied;

        // Every actual join starts a fresh connection, including a manual join to the same address.
        public void Begin(ServerAddress? target) { Target = target; denied = false; }

        public void Observe(ServerAddress? target, bool authenticated, bool allowsReconnect)
        {
            if (!authenticated || target == null || target != Target) return;
            denied = !allowsReconnect;
        }

        public bool LockedFor(ServerAddress? target) => denied && target != null && target == Target;
        public void Reset() { Target = null; denied = false; }
    }
}
