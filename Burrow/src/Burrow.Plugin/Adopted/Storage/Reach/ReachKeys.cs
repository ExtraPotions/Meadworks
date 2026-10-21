using HarmonyLib;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>Polls the Toggle Key and the Link Key once per frame for the local player (<c>Player.Update</c>).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    public static class ReachKeys
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !ReachSettings.Enabled.Value)
                return;
            if (Keys.Pressed(ReachSettings.ToggleKey))
                Toggle();
            if (Keys.Pressed(ReachSettings.LinkKey) && ReachSettings.ShowLinks.Value && !ReachRules.PlayerOff)
                ReachLinks.ShowAll();
        }

        private static void Toggle()
        {
            bool off = !ReachRules.PlayerOff;
            ReachRules.SetPlayerOff(off);
            Messages.Center("$burrow_reach: " + (off ? "$burrow_off" : "$burrow_on"));
        }
    }
}
