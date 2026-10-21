using System.Globalization;
using Mantle.Core;

namespace Mantle.Backpacks
{
    /// <summary>
    /// The backpacks' $mantle_backpack_ words: each pack's name and its description, which says what it gives (so it is
    /// written again when the backpacks file changes), the message when there is no room to take a pack off, and the
    /// tooltip's line for what items in a pack weigh (<see cref="BackpackTooltip"/>).
    /// </summary>
    public static class BackpackWords
    {
        public static string NoRoom { get; private set; }

        public static string Load { get; private set; } = "$mantle_backpack_load";

        public static void Register()
        {
            foreach (BackpackKind kind in BackpackCatalog.All)
                Language.Add(kind.Token.Substring(1), kind.Name);
            NoRoom = Language.Add("mantle_backpack_noroom", "No room in your inventory to take the backpack off");
            Load = Language.Add("mantle_backpack_load", "Weight of items in it");
            DescribeAll();
        }

        public static void DescribeAll()
        {
            foreach (BackpackKind kind in BackpackCatalog.All)
                Language.Add(kind.DescriptionToken.Substring(1), Describe(kind));
        }

        private static string Describe(BackpackKind kind)
        {
            BackpackStats stats = kind.Stats;
            string gives = $"+{stats.Slots} inventory slots";
            if (stats.Carry > 0f)
                gives += $" and +{stats.Carry.ToString("0", CultureInfo.InvariantCulture)} carry weight";
            string portal = stats.Portal ? " What its slots hold may go through portals, if the server allows it." : "";
            return $"{kind.Look} Put it in your Backpack slot to wear it: {gives} while it is on your back. Taking it off drops what does not fit. Upgrade it at its station: each level slows you less and lightens what it holds.{portal}";
        }
    }
}
