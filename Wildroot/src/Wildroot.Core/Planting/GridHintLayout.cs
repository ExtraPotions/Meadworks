using System;
using System.Collections.Generic;
namespace ExtraPotions.Wildroot {
 public static class GridHintLayout {
  public static double[] Positions(IReadOnlyList<double> widths,double gap) {
   if(gap<0||double.IsNaN(gap)||double.IsInfinity(gap))throw new ArgumentOutOfRangeException(nameof(gap));
   var positions=new double[widths.Count];double next=0;
   for(int i=0;i<widths.Count;i++) {
    if(widths[i]<0||double.IsNaN(widths[i])||double.IsInfinity(widths[i]))throw new ArgumentOutOfRangeException(nameof(widths));
    positions[i]=next;next+=widths[i]+gap;
   }
   return positions;
  }
  public static double Scale(double available,double required)=>required<=0?1:Math.Min(1,Math.Max(0,available)/required);
 }
}
