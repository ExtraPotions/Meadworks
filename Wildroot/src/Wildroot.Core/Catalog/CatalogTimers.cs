using System;

namespace ExtraPotions.Wildroot {
 /// <summary>
 /// Timer decisions the game does not repeat after a pickable is already loaded.
 /// Pickable.Awake starts UpdateRespawn only while m_respawnTimeMinutes is already above zero, on a 60-second poll.
 /// ShouldRespawn treats any positive elapsed time as ready when that field is zero, so a later zero must stop the poll.
 /// The picked time stays in the ZDO; only the instance duration changes.
 /// </summary>
 public static class CatalogTimers {
  public const string RespawnMethod="UpdateRespawn";
  /// <summary>Same first delay and repeat the game uses when it does start the poll.</summary>
  public const float RespawnFirstDelaySeconds=1f;
  public const float RespawnRepeatSeconds=60f;
  public static bool PollsRespawn(float minutes)=>minutes>0f;
  /// <summary>Prefab id of a placed or spawned object. Unity can append (Clone) more than once.</summary>
  public static string PrefabName(string name){
   if(string.IsNullOrEmpty(name))return "";
   const string suffix="(Clone)";
   while(name.EndsWith(suffix,StringComparison.Ordinal))name=name.Substring(0,name.Length-suffix.Length);
   return name;
  }
 }
}
