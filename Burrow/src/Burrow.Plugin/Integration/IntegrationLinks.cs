using System;
using Burrow.Stow;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace Burrow.Integration
{
    /// <summary>
    /// The game-side ends of <see cref="IntegrationContracts"/>: the capabilities Burrow reads from other products,
    /// resolved at most once per frame through Crucible, and the ones Burrow publishes. Main thread only.
    /// </summary>
    internal static class IntegrationLinks
    {
        private static readonly CapabilityLink<Func<bool>> SalvageTab =
            new CapabilityLink<Func<bool>>(IntegrationContracts.SalvageTab, IntegrationContracts.ContractVersion, Registry);

        /// <summary>KB1: Kindle's Salvage tab is active, so Burrow leaves the requirement rows alone. False without Kindle.</summary>
        public static bool SalvageTabActive => IntegrationRules.SalvageTabActive(SalvageTab.Get(Time.frameCount));

        private static readonly CapabilityLink<Func<object, string>> ContainerRole =
            new CapabilityLink<Func<object, string>>(IntegrationContracts.ContainerRole, IntegrationContracts.ContractVersion, Registry);

        /// <summary>BS1: a Shepherd trough (role <c>feed</c>); never a material source or routing target. False without Shepherd.</summary>
        public static bool IsFeedContainer(Container container) =>
            container != null && IntegrationRules.IsFeedContainer(ContainerRole.Get(Time.frameCount), container);

        /// <summary>Publishes Burrow's capabilities for other products.</summary>
        public static void Register(ProductRegistration registration)
        {
            IntegrationRules.RegisterFavourites(registration, Favourites.FavouriteItemNames);
            IntegrationRules.RegisterFeedSource(registration, FeedSources.Query);
            // Feature ownership read by Mantle (user decision 2026-10-12 22:41): Burrow places the stow row whenever it is
            // installed (presence, as Mantle's old plugin check), and Stacks answers with its live "4. Stacks / Enabled".
            var version = ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.ContractVersion;
            registration.AddCapability<Func<bool>>(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Id(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.StowRow), version, () => true);
            registration.AddCapability<Func<bool>>(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Id(ExtraPotions.Meadworks.Contracts.FeatureOwnerContract.Stacks), version,
                () => Burrow.Stacks.StacksSettings.Enabled != null && Burrow.Stacks.StacksSettings.Enabled.Value);
        }

        private static CapabilityRegistry Registry() => CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null;
    }

    /// <summary>KB2: Trash Uses Salvage hands a whole inventory stack to Kindle's salvage when Kindle offers it.</summary>
    internal static class SalvageLink
    {
        private static readonly CapabilityLink<Func<object, bool>> Can =
            new CapabilityLink<Func<object, bool>>(IntegrationContracts.SalvageCan, IntegrationContracts.ContractVersion, Registry);

        private static readonly CapabilityLink<Func<object, object, bool>> Run =
            new CapabilityLink<Func<object, object, bool>>(IntegrationContracts.SalvageItem, IntegrationContracts.ContractVersion, Registry);

        public static bool Available => Can.Get(Time.frameCount) != null && Run.Get(Time.frameCount) != null;

        public static bool CanSalvage(ItemDrop.ItemData item) =>
            Run.Get(Time.frameCount) != null && IntegrationRules.CanSalvage(Can.Get(Time.frameCount), item);

        public static bool Salvage(Player player, ItemDrop.ItemData item) =>
            IntegrationRules.Salvage(Run.Get(Time.frameCount), player, item);

        private static CapabilityRegistry Registry() => CruciblePlugin.IsReady ? CruciblePlugin.Services.Capabilities : null;
    }
}
