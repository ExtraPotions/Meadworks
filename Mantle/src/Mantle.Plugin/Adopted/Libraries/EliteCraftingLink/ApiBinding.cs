using System;
using System.Collections.Generic;
using BepInEx.Logging;
using ExtraPotions.Meadworks.Contracts;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Mantle.Embedded.EliteCraftingLink
{
    /// <summary>
    /// Finds EliteCrafting's API: Meadworks' EliteCrafting (Inscribe) publishes its endpoints as named delegates under the
    /// Crucible capability <c>meadworks.crafting.api</c> (Crucible.Contracts' CraftingApiContract), with
    /// <c>GetApiVersion</c> among them. The capability is resolved again only when Crucible's registry changes, so an
    /// absent EliteCrafting costs an integer compare per call and a provider that registers later is still found. Endpoints
    /// are the published typed delegates (<see cref="Create{T}"/>), so a call costs what a direct call costs. Nothing of
    /// EliteCrafting is referenced or reflected into. Main thread only.
    /// </summary>
    internal static class ApiBinding
    {
        // Mantle, 2026-10-10 (parity choice #94): Meadworks' EliteCrafting is Inscribe, which keeps EliteCrafting 0.7's public
        // API (API version 1); since 2026-10-13 (user decision 22:41) it is reached through Crucible, not by plugin lookup.

        /// <summary>The API version this library is written against; an older EliteCrafting counts as absent.</summary>
        public const int Required = 1;

        private static readonly CapabilityLink<IReadOnlyDictionary<string, Delegate>> link =
            new CapabilityLink<IReadOnlyDictionary<string, Delegate>>(CraftingApiContract.Id, CraftingApiContract.ContractVersion);
        private static IReadOnlyDictionary<string, Delegate>? _seen;
        private static IReadOnlyDictionary<string, Delegate>? _api;
        private static int _version;
        private static ManualLogSource? _log;

        /// <summary>The published endpoints, or null: EliteCrafting absent (so far), or older than <see cref="Required"/>.</summary>
        public static IReadOnlyDictionary<string, Delegate>? Api
        {
            get
            {
                Settle();
                return _api;
            }
        }

        /// <summary>EliteCrafting's API version; 0 while it is absent or has no API.</summary>
        public static int Version
        {
            get
            {
                Settle();
                return _version;
            }
        }

        /// <summary>The endpoint as the delegate type asked for, or null when the API is absent or lacks it (logged once per name).</summary>
        public static T? Create<T>(string name) where T : Delegate
        {
            var api = Api;
            if (api == null) return null;
            if (CraftingApiContract.TryGet<T>(api, name, out var endpoint)) return endpoint;
            Warn($"Inscribe's API has no {name} of the expected shape; that call does nothing");
            return null;
        }

        /// <summary>A call into EliteCrafting threw (its endpoints should not): logged, the caller gets the empty answer.</summary>
        public static void Failed(Exception e) => Warn($"a call into Inscribe's API failed: {e}");

        private static void Settle()
        {
            link.TryGet(CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null, out var found);
            if (ReferenceEquals(found, _seen)) return;
            _seen = found;
            _api = null;
            _version = CraftingApiContract.ApiVersion(found);
            if (found == null) return;
            if (_version < Required)
            {
                Warn($"Inscribe has no API version {Required} or later (found {_version}); its integration is off");
                return;
            }
            _api = found;
        }

        private static void Warn(string message)
        {
            _log ??= Logger.CreateLogSource("Mantle.InscribeLink");
            _log.LogWarning(message);
        }
    }
}
