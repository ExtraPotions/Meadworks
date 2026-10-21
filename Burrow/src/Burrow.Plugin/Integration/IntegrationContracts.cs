using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;

namespace Burrow.Integration
{
    /// <summary>
    /// Burrow's optional integration contracts with other Meadworks products, all through Crucible capabilities.
    /// Burrow references no other product and no other product references Burrow. Crucible resolves a capability by
    /// its exact contract <see cref="Type"/>, so every contract is a BCL delegate type, identical in every assembly.
    /// Every consumer path is inert when the provider is absent, ambiguous, of another major version, or throws.
    /// Providers and consumers call these delegates on the Unity main thread only. This file stays free of game types
    /// so the portable suite compiles it.
    /// </summary>
    public static class IntegrationContracts
    {
        /// <summary>KB1, provided by Kindle: <c>Func&lt;bool&gt;</c>, true while its Salvage tab is the active crafting tab.</summary>
        public const string SalvageTab = "meadworks.crafting.salvage_tab";

        /// <summary>KB2, provided by Kindle: <c>Func&lt;object, bool&gt;</c>, true when an <c>ItemDrop.ItemData</c> can be salvaged now.</summary>
        public const string SalvageCan = "meadworks.crafting.salvage_can";

        /// <summary>KB2, provided by Kindle: <c>Func&lt;object, object, bool&gt;</c>, salvages a whole stack (player, item); true when it did.</summary>
        public const string SalvageItem = "meadworks.crafting.salvage_item";

        /// <summary>KB1, provided by Burrow: <c>Func&lt;IReadOnlyCollection&lt;string&gt;&gt;</c>, the local character's favorite item prefab names (Crucible.Contracts <c>FavouritesContract</c>, same id).</summary>
        public const string Favourites = ExtraPotions.Meadworks.Contracts.FavouritesContract.Id;

        /// <summary>BS1, provided by Shepherd: <c>Func&lt;object, string&gt;</c>, the role of a <c>Container</c> or null.</summary>
        public const string ContainerRole = "meadworks.containers.role";

        /// <summary>BS2, provided by Burrow: <c>Func&lt;object, bool&gt;</c>, true when a <c>Container</c> carries the "Feed source" tag.</summary>
        public const string FeedSource = "meadworks.containers.feed_source";

        /// <summary>The container role that keeps a container out of Burrow's material sources and routing.</summary>
        public const string FeedRole = "feed";

        /// <summary>The ZDO bool on the container's net object that stores Burrow's "Feed source" tag.</summary>
        public const string FeedSourceKey = "burrow.feedSource";

        /// <summary>The version of every contract above (1.0). Consumers ask for this minimum.</summary>
        public static readonly System.Version ContractVersion = new System.Version(1, 0);
    }

    /// <summary>
    /// A lazy view of one capability another product may provide. It resolves at most once per tick (Burrow passes
    /// the frame count), so a provider that loads or unloads later is seen on the next tick, and a missing,
    /// ambiguous or failing registry gives null.
    /// </summary>
    public sealed class CapabilityLink<T> where T : class
    {
        private readonly string id;
        private readonly System.Version version;
        private readonly Func<CapabilityRegistry?> registry;
        private bool resolved;
        private long tick;
        private T? cached;

        public CapabilityLink(string id, System.Version version, Func<CapabilityRegistry?> registry)
        {
            this.id = id ?? throw new ArgumentNullException(nameof(id));
            this.version = version ?? throw new ArgumentNullException(nameof(version));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>The single provider of the capability, or null. Never throws.</summary>
        public T? Get(long now)
        {
            if (resolved && now == tick)
                return cached;
            resolved = true;
            tick = now;
            cached = Resolve();
            return cached;
        }

        private T? Resolve()
        {
            try
            {
                CapabilityRegistry? current = registry();
                return current != null && current.TryResolve(id, version, out T? service) ? service : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>The decisions Burrow takes from the contracts, kept free of game types.</summary>
    public static class IntegrationRules
    {
        private static readonly IReadOnlyCollection<string> None = new ReadOnlyCollection<string>(new string[0]);

        /// <summary>KB1: Burrow's requirement rows stay away while Kindle's Salvage tab is active. Inert without Kindle.</summary>
        public static bool SalvageTabActive(Func<bool>? provider)
        {
            if (provider == null)
                return false;
            try
            {
                return provider();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>KB2: Kindle can salvage this item. False without Kindle, for a missing item, or when the provider throws.</summary>
        public static bool CanSalvage(Func<object, bool>? provider, object? item)
        {
            if (provider == null || item == null)
                return false;
            try
            {
                return provider(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>KB2: Kindle salvaged the stack. False without Kindle, for a missing player or item, or when the provider throws.</summary>
        public static bool Salvage(Func<object, object, bool>? provider, object? player, object? item)
        {
            if (provider == null || player == null || item == null)
                return false;
            try
            {
                return provider(player, item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// BS1: the container has the <c>feed</c> role (a Shepherd trough), so it is no material source and no routing
        /// target. Inert without Shepherd, for a missing container, and when the provider throws.
        /// </summary>
        public static bool IsFeedContainer(Func<object, string?>? roles, object? container)
        {
            if (roles == null || container == null)
                return false;
            try
            {
                return string.Equals(roles(container), IntegrationContracts.FeedRole, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>BS2: the chest carries Burrow's "Feed source" tag and is not private (private chests never feed).</summary>
        public static bool IsFeedSource(bool tagged, bool privateChest) => tagged && !privateChest;

        /// <summary>BS2: the tag is written by the ZDO owner only, and never on a private chest.</summary>
        public static bool CanTagFeedSource(bool privateChest, bool owner) => owner && !privateChest;

        /// <summary>BS2: publishes the feed source query. The query never throws; a missing container or a failure reads as false.</summary>
        public static void RegisterFeedSource(ProductRegistration registration, Func<object, bool> query)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            registration.AddCapability<Func<object, bool>>(IntegrationContracts.FeedSource, IntegrationContracts.ContractVersion, container =>
            {
                if (container == null)
                    return false;
                try
                {
                    return query(container);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        /// <summary>KB1: a detached, read-only, sorted copy of favorite item names without blanks or duplicates.</summary>
        public static IReadOnlyCollection<string> FavouritesSnapshot(IEnumerable<string>? names)
        {
            if (names == null)
                return None;
            string[] copy = names.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            return new ReadOnlyCollection<string>(copy);
        }

        /// <summary>KB1: publishes the favorites reader. The reader never throws; a failing source reads as empty.</summary>
        public static void RegisterFavourites(ProductRegistration registration, Func<IEnumerable<string>?> source)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            registration.AddCapability<Func<IReadOnlyCollection<string>>>(IntegrationContracts.Favourites, ExtraPotions.Meadworks.Contracts.FavouritesContract.ContractVersion, () =>
            {
                try
                {
                    return FavouritesSnapshot(source());
                }
                catch (Exception)
                {
                    return None;
                }
            });
        }
    }
}
