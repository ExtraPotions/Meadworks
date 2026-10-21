using System.Reflection;
using HarmonyLib;
namespace ExtraPotions.Wildroot {
 /// <summary>Private game fields read on per-frame paths, looked up once.</summary>
 internal static class NativeFields {
  internal static readonly FieldInfo PlacementGhost=AccessTools.Field(typeof(Player),"m_placementGhost");
  internal static readonly FieldInfo PlacementStatus=AccessTools.Field(typeof(Player),"m_placementStatus");
  internal static readonly FieldInfo NoPlacementCost=AccessTools.Field(typeof(Player),"m_noPlacementCost");
  internal static readonly FieldInfo PlantStatus=AccessTools.Field(typeof(Plant),"m_status");
  internal static readonly FieldInfo PlantHealthy=AccessTools.Field(typeof(Plant),"m_healthy");
  internal static readonly FieldInfo PlantUnhealthy=AccessTools.Field(typeof(Plant),"m_unhealthy");
  internal static readonly MethodInfo GetRightItem=AccessTools.Method(typeof(Humanoid),"GetRightItem");
  internal static readonly MethodInfo CanBePicked=AccessTools.Method(typeof(Pickable),"CanBePicked");
  internal static readonly MethodInfo GetPicked=AccessTools.Method(typeof(Pickable),"GetPicked");
  internal static readonly MethodInfo GetHoneyLevel=AccessTools.Method(typeof(Beehive),"GetHoneyLevel");
  internal static readonly MethodInfo TimeSincePlanted=AccessTools.Method(typeof(Plant),"TimeSincePlanted");
  internal static readonly MethodInfo GetGrowTime=AccessTools.Method(typeof(Plant),"GetGrowTime");
  internal static readonly FieldInfo HudRoot=AccessTools.Field(typeof(Hud),"m_rootObject");
 }
}
