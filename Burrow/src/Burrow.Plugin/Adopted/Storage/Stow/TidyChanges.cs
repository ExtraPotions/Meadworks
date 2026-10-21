using HarmonyLib;

namespace Burrow.Stow
{
    /// <summary>
    /// The game's change callback of a container's inventory (<c>Container.OnContainerChanged</c>, which saves it on the
    /// owner): a change on a closed chest this client owns - ground pickup, a quick stack or route into it, Reach taking
    /// from it, another mod - gets the chest a look soon and wakes the waiting chests near it. Changes while the chest is
    /// open wait for its release (<see cref="TidyHands"/>), loading from the ZDO is no change, and Auto Tidy's own moves
    /// are left out.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.OnContainerChanged))]
    public static class TidyChanges
    {
        [HarmonyPostfix]
        public static void Postfix(Container __instance)
        {
            if (!StowSettings.Enabled.Value || !StowSettings.AutoTidy.Value || TidySweep.Moving || __instance.m_loading || Player.m_localPlayer == null)
                return;
            ZNetView view = __instance.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner() || __instance.IsInUse())
                return;
            TidySchedule.Changed(__instance);
        }
    }
}
