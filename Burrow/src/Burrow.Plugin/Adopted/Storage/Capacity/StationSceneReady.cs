using HarmonyLib;

namespace Burrow.Capacity
{
    /// <summary>
    /// When the scene's prefabs exist (ZNetScene.Awake, low priority so other mods' prefabs are registered) the
    /// station list is generated into a still-default Burrow.Stations.yml and the caps are applied to the prefabs
    /// and the loaded stations. Instances copy the prefab's fields when they are instantiated (ZNetScene.CreateObject
    /// and Player.PlacePiece both instantiate the prefab), so that alone covers new stations; the Smelter.Awake
    /// postfix only guards a station made from another copy of its prefab.
    /// </summary>
    public static class StationSceneReady
    {
        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        private static class ScenePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            private static void Postfix()
            {
                StationTemplate.GenerateIfDefault();
                StationCapacities.ApplyAll();
            }
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.Awake))]
        private static class StationPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Smelter __instance) => StationCapacities.ApplyTo(__instance);
        }
    }
}
