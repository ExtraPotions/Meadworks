using System;
using System.Collections.Generic;
namespace ExtraPotions.Wildroot {
 public readonly struct Point3 {public double X{get;}public double Y{get;}public double Z{get;}public Point3(double x,double y,double z){X=x;Y=y;Z=z;}}
 public readonly struct Point2 : IEquatable<Point2> { public double X{get;}public double Z{get;}public Point2(double x,double z){X=x;Z=z;}public bool Equals(Point2 other)=>X==other.X&&Z==other.Z;public override bool Equals(object? obj)=>obj is Point2 p&&Equals(p);public override int GetHashCode()=>X.GetHashCode()^Z.GetHashCode(); }
}
