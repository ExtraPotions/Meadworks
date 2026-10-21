using HarmonyLib;

namespace Burrow.Capacity
{
    /// <summary>Discover containers after scene startup or Jotunn registration, then apply configured sizes.</summary>
    public static class SceneReady
    {
        public static void Initialize() => Jotunn.Managers.PrefabManager.OnPrefabsRegistered += Registered;
        public static void Shutdown() => Jotunn.Managers.PrefabManager.OnPrefabsRegistered -= Registered;
        private static void Registered() => ExtraPotions.Burrow.Embedded.PatchGuard.Guard.Run("container discovery", ApplyPrefabs);

        private static void ApplyPrefabs()
        {
            ContainerTemplate.GenerateIfDefault();
            ContainerSizes.ApplyAll();
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        private static class ScenePatch
        {
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            private static void Postfix()
            {
                ApplyPrefabs();
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
        private static class ContainerPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Container __instance) => ContainerSizes.ApplyTo(__instance);
        }
    }
}
