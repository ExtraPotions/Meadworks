using HarmonyLib;

namespace Burrow.Shared
{
    /// <summary>
    /// The owning client writes the name of the player using a container into the ZDO string
    /// <c>Burrow.user</c> and clears it when the container is released. <c>Container.SetInUse(bool)</c> only
    /// acts on the owner and is called every frame while the panel is open, so the string is compared before it
    /// is written; the ZDO changes only when the name changes.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
    public static class UserNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Container __instance, bool inUse)
        {
            ZNetView view = __instance.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner() || __instance.m_inUse != inUse)
                return;
            string wanted = inUse && Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";
            ZDO zdo = view.GetZDO();
            if (zdo.GetString(SharedState.UserKey, "") != wanted)
                zdo.Set(SharedState.UserKey, wanted);
        }
    }
}
