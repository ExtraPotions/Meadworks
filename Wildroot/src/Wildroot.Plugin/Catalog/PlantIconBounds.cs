using System;
namespace ExtraPotions.Wildroot.Runtime {
 internal static class PlantIconBounds {
  internal static (int x,int y,int size) Square(int width,int height,int left,int top,int right,int bottom){
   int size=Math.Min(Math.Min(width,height),Math.Max(right-left+1,bottom-top+1)+4);
   int x=Math.Max(0,Math.Min(width-size,(left+right+1-size)/2));
   int y=Math.Max(0,Math.Min(height-size,(top+bottom+1-size)/2));
   return (x,y,size);
  }
 }
}
