using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Burrow.Stow
{
    /// <summary>
    /// What players put into a chest by hand. A chest changes by hand while it is open (the player's own moves, and
    /// with the Shared module the other players' requests the owner applies); the game opens a chest only on its owner,
    /// so the owner sees both ends. When a chest is taken into use, what it holds is noted; when it is released, the
    /// prefabs that grew are added by hand and wait for the chest's next look, which follows a few seconds later.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
    public static class TidyHands
    {
        private const float CloseDelay = 3f;

        private static readonly Dictionary<Container, Dictionary<string, float>> opened = new Dictionary<Container, Dictionary<string, float>>();
        private static readonly Dictionary<Container, HashSet<string>> added = new Dictionary<Container, HashSet<string>>();

        [HarmonyPrefix]
        public static void Prefix(Container __instance, out bool __state)
        {
            __state = __instance.m_inUse;
        }

        [HarmonyPostfix]
        public static void Postfix(Container __instance, bool __state)
        {
            ZNetView view = __instance.m_nview;
            if (!StowSettings.AutoTidy.Value || __state == __instance.m_inUse || view == null || !view.IsValid() || !view.IsOwner())
                return;
            if (opened.Count > 32)
                opened.Clear();
            if (__instance.m_inUse)
                opened[__instance] = TidyProfiles.Held(__instance.GetInventory());
            else
                Closed(__instance);
        }

        /// <summary>The prefabs put in by hand since the chest's last look; null when none.</summary>
        internal static HashSet<string> Added(Container chest) => added.TryGetValue(chest, out HashSet<string> set) ? set : null;

        internal static void Clear(Container chest) => added.Remove(chest);

        private static void Closed(Container chest)
        {
            if (opened.TryGetValue(chest, out Dictionary<string, float> before))
            {
                opened.Remove(chest);
                Grew(chest, before);
            }
            TidySchedule.Soon(chest, CloseDelay);
        }

        private static void Grew(Container chest, Dictionary<string, float> before)
        {
            if (added.Count > 64)
                added.Clear();
            foreach (KeyValuePair<string, float> pair in TidyProfiles.Held(chest.GetInventory()))
            {
                before.TryGetValue(pair.Key, out float was);
                if (pair.Value <= was + 0.001f)
                    continue;
                if (!added.TryGetValue(chest, out HashSet<string> set))
                    added[chest] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(pair.Key);
            }
        }
    }
}
