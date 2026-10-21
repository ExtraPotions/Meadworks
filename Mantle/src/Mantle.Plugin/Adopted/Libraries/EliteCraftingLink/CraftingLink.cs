using System;

namespace ExtraPotions.Mantle.Embedded.EliteCraftingLink
{
    /// <summary>
    /// EliteCrafting's API, its shape (api.md section 1): whether it is there and which version. The endpoints are in
    /// <see cref="CraftingClasses"/> (item classes and levels), <see cref="CraftingInscriptions"/> (inscriptions,
    /// pools, external effects, totals), <see cref="CraftingItems"/> (an item's rarity and inscriptions, rolls) and
    /// <see cref="CraftingHooks"/> (equipment providers, filters, listeners, creature loot). Every wrapper does nothing
    /// and answers false, null or 0 while EliteCrafting is absent, older than API version 1, or lacks the endpoint.
    /// <para>
    /// EliteCrafting must have registered its API (in its Awake) before the first call that registers anything: order the
    /// plugin after it with a soft <c>BepInDependency</c> and register in Awake.
    /// Registrations are code: every peer runs the same mods, so register the same things on every peer.
    /// </para>
    /// </summary>
    public static class CraftingLink
    {
        private static readonly Endpoint<Func<string?>> pluginVersion = new Endpoint<Func<string?>>("GetPluginVersion");
        private static readonly Endpoint<Func<string, bool>> hasEndpoint = new Endpoint<Func<string, bool>>("HasEndpoint");
        private static readonly Endpoint<Func<string[]?>> endpointNames = new Endpoint<Func<string[]?>>("GetEndpointNames");

        /// <summary>EliteCrafting is loaded with API version 1 or later.</summary>
        public static bool Present => ApiBinding.Api != null;

        /// <summary>EliteCrafting's API version (0 when absent).</summary>
        public static int ApiVersion => ApiBinding.Version;

        /// <summary>EliteCrafting's plugin version, or null.</summary>
        public static string? PluginVersion => Safe.Call(pluginVersion.Call, null);

        /// <summary>Whether EliteCrafting has an endpoint of this name (a newer one than this library knows, too).</summary>
        public static bool HasEndpoint(string name) => Safe.Call(hasEndpoint.Call, name, false);

        /// <summary>Every endpoint name EliteCrafting has; empty when absent.</summary>
        public static string[] EndpointNames => Safe.Call(endpointNames.Call, null) ?? Array.Empty<string>();
    }
}
