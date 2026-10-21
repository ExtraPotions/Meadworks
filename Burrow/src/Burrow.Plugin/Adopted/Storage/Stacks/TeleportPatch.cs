using HarmonyLib;

namespace Burrow.Stacks
{
    /// <summary>
    /// "Ignore Teleport Restriction": Inventory.IsTeleportable(bool allowAllItems) is what the portal asks about the
    /// player's inventory (TeleportWorld through Humanoid.IsTeleportable); with the setting on it answers yes for
    /// every item.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsTeleportable))]
    public static class TeleportPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            if (!StacksSettings.Enabled.Value || !StacksSettings.IgnoreTeleportRestriction.Value)
                return true;
            __result = true;
            return false;
        }
    }
}
