// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// Tells every player when the world tier rises, because a world that silently got harder looks like bad luck. The
    /// server decides: the game routes every new global key to it, and it compares the tier before and after the key
    /// lands. A rise goes out over the game's routed-RPC bus to <c>Everybody</c>, the host included, and each client
    /// shows it on its own screen. A tier that falls (an admin's removekey) is not announced; `wildblood tier` reports it.
    /// </summary>
    public static class TierAnnounce
    {
        public const string Rpc = "ecr_tier";

        private static ZRoutedRpc? _registeredOn;

        /// <summary>Registers the handler once per routed-RPC bus, so leaving a world and joining another re-registers.</summary>
        public static void EnsureRegistered() => TryEnsureRegistered();

        public static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || !Active)
            {
                return false;
            }
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.Tier, () => {
                bus.Register<int, int>(Rpc, OnTier);
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Rpc)) return false;
            _registeredOn = bus;
            return true;
        }

        /// <summary>Server side, after a global key has landed: announce when it lifted the tier above <paramref name="before"/>.</summary>
        public static void IfRisen(int before)
        {
            if (!Active || !Rules.RuleState.Active.Tiers.Announce || ZNet.instance == null || !ZNet.instance.IsServer()) return;
            int after = WorldTier.Current();
            if (after <= before || ZRoutedRpc.instance == null)
            {
                return;
            }
            if (!TryEnsureRegistered()) return;
            Log.Info($"world tier rose from {before} to {after}");
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, after, WorldTier.Ceiling());
        }

        private static bool Active => Plugin.NativeRulesReady && WildbloodModules.Active(WildbloodModule.Stars);

        private static void OnTier(long sender, int tier, int ceiling)
        {
            ZRoutedRpc? bus = ZRoutedRpc.instance;
            if (!Active || bus == null || !ReferenceEquals(bus, _registeredOn)
                || !NativeRpcOwnership.OwnsRouted(bus, NativeRpcOwner.Tier)
                || sender != bus.GetServerPeerID() || tier < 0 || ceiling < tier
                || ceiling != WorldTier.Ceiling() || tier != WorldTier.Current()) return;
            Guard.Run("TierAnnounce.OnTier", () => Show(tier, ceiling));
        }

        // A dedicated server has no HUD and simply skips this.
        private static void Show(int tier, int ceiling)
        {
            if (WildbloodModules.Active(WildbloodModule.Display) && Plugin.AllowsUi && MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                    $"The world hardens: tier {tier} of {ceiling}");
            }
        }
    }
}
