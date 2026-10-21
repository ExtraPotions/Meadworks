using System;
namespace ExtraPotions.Wildroot {
 public static class GrowthRules {
  static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
  public static GrowthResult Evaluate(PlantState state,double worldTime,PlantDefinition definition){
   if(definition==null)throw new ArgumentNullException(nameof(definition));if(!Finite(worldTime)||!Finite(state.Started)||!Finite(state.Duration)||state.Duration<0)throw new ArgumentException("Invalid growth clock");
   if(state.Picked&&definition.RespawnSeconds==0)return new GrowthResult(GrowthPhase.Exhausted,1,0);
   var duration=state.Picked?definition.RespawnSeconds:state.Duration;var elapsed=Math.Max(0,worldTime-state.Started);var fraction=duration==0?1:Math.Min(1,elapsed/duration);var remaining=Math.Max(0,duration-elapsed);
   return new GrowthResult(state.Blocked?GrowthPhase.Blocked:remaining==0?GrowthPhase.Ready:state.Picked?GrowthPhase.Regrowing:GrowthPhase.Growing,fraction,remaining);
  }
 }
}
