using System;
using System.Collections.Generic;
namespace ExtraPotions.Wildroot {
 /// <summary>World-space vector using the Valheim/Unity convention: Y is up, yaw rotates clockwise seen from above.</summary>
 public readonly struct Vec3 : IEquatable<Vec3> {
  public double X{get;} public double Y{get;} public double Z{get;}
  public Vec3(double x,double y,double z){X=x;Y=y;Z=z;}
  public static readonly Vec3 Zero=new Vec3(0,0,0);
  public static readonly Vec3 Forward=new Vec3(0,0,1);
  public static Vec3 operator+(Vec3 a,Vec3 b)=>new Vec3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
  public static Vec3 operator-(Vec3 a,Vec3 b)=>new Vec3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
  public static Vec3 operator-(Vec3 a)=>new Vec3(-a.X,-a.Y,-a.Z);
  public static Vec3 operator*(Vec3 a,double s)=>new Vec3(a.X*s,a.Y*s,a.Z*s);
  public double SqrMagnitude=>X*X+Y*Y+Z*Z;
  public double Magnitude=>Math.Sqrt(SqrMagnitude);
  public Vec3 Normalized{get{var m=Magnitude;return m>1e-9?this*(1/m):Zero;}}
  public static double Dot(Vec3 a,Vec3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
  /// <summary>Cross(up, v): the right-hand side of a horizontal direction.</summary>
  public static Vec3 CrossUp(Vec3 v)=>new Vec3(v.Z,0,-v.X);
  /// <summary>Horizontal unit direction; zero stays zero.</summary>
  public Vec3 DirectionXZ=>new Vec3(X,0,Z).Normalized;
  /// <summary>Rotates around the up axis by a yaw in degrees (Unity Quaternion.Euler(0,yaw,0) * v).</summary>
  public Vec3 RotateYaw(double degrees){var r=degrees*Math.PI/180;double c=Math.Cos(r),s=Math.Sin(r);return new Vec3(X*c+Z*s,Y,Z*c-X*s);}
  /// <summary>Signed yaw in degrees from forward to this horizontal direction.</summary>
  public double Yaw=>Math.Atan2(X,Z)*180/Math.PI;
  public bool Equals(Vec3 other)=>X==other.X&&Y==other.Y&&Z==other.Z;
  public override bool Equals(object? obj)=>obj is Vec3 v&&Equals(v);
  public override int GetHashCode()=>X.GetHashCode()^(Y.GetHashCode()*397)^(Z.GetHashCode()*7919);
  public override string ToString()=>"("+X.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+", "+Y.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+", "+Z.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+")";
 }
 /// <summary>Row and column step vectors of a planting grid, already scaled by the cell spacing.</summary>
 public readonly struct GridAxes {
  public Vec3 Row{get;} public Vec3 Column{get;}
  public GridAxes(Vec3 row,Vec3 column){Row=row;Column=column;}
  /// <summary>
  /// Default grid directions when nothing is snapped. Rows advance along the ghost's forward direction when
  /// directions are globally aligned, otherwise along the player-to-ghost direction; both are then turned by the
  /// ghost yaw. Columns extend to the right of rows.
  /// </summary>
  public static GridAxes Default(double ghostYaw,bool globallyAligned,Vec3 ghost,Vec3 player,double spacing){
   if(!Finite(ghostYaw)||!Finite(spacing)||spacing<=0)throw new ArgumentException("Invalid grid axes");
   var row=globallyAligned?Vec3.Forward:(ghost-player).DirectionXZ;var column=Vec3.CrossUp(row);
   return new GridAxes(row.RotateYaw(ghostYaw)*spacing,column.RotateYaw(ghostYaw)*spacing);
  }
  static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
 }
 public readonly struct GridCell {
  public int Index{get;} public int Row{get;} public int Column{get;} public Vec3 Position{get;}
  public GridCell(int index,int row,int column,Vec3 position){Index=index;Row=row;Column=column;Position=position;}
 }
 public static class GridLayout {
  /// <summary>Largest rows or columns value: a grid is at most 10 x 10 whatever the configuration, a hand-edited file or a synced value says.</summary>
  public const int MaxDimension=10;
  public static int Clamp(int dimension)=>Math.Min(MaxDimension,Math.Max(1,dimension));
  /// <summary>The grid size to use: the configured rows and columns clamped to 1 through 10, or 1 x 1 when grid planting is off.</summary>
  public static (int rows,int columns) Size(bool enabled,int rows,int columns)=>enabled?(Clamp(rows),Clamp(columns)):(1,1);
  /// <summary>Number of cells actually planted: rows x columns, capped by the concurrent placement limit.</summary>
  public static int CellCount(int rows,int columns,int maxConcurrent){
   if(maxConcurrent<1)throw new ArgumentException("Invalid grid size");
   return (int)Math.Min((long)Clamp(rows)*Clamp(columns),maxConcurrent);
  }
  /// <summary>Cells in index order: index i lies at row i / columns and column i % columns, root first.</summary>
  public static IReadOnlyList<GridCell> Cells(Vec3 root,GridAxes axes,int rows,int columns,int maxConcurrent){
   int count=CellCount(rows,columns,maxConcurrent);columns=Clamp(columns);var result=new List<GridCell>(count);
   for(int i=0;i<count;i++){int row=i/columns,column=i%columns;result.Add(new GridCell(i,row,column,root+axes.Row*row+axes.Column*column));}
   return result.AsReadOnly();
  }
  /// <summary>Clamped resize: dimensions stay between one and <see cref="MaxDimension"/>.</summary>
  public static int Resize(int value,int delta)=>Clamp(value>int.MaxValue-Math.Max(0,delta)?int.MaxValue:value+delta);
 }
 /// <summary>Facts about a piece needed to derive its grid spacing.</summary>
 public readonly struct SpacingFacts {
  public bool IsPlant{get;} public bool IsSapling{get;} public double GrowRadius{get;} public double ColliderRadius{get;} public double PickableSpacing{get;}
  public SpacingFacts(bool isPlant,bool isSapling,double growRadius,double colliderRadius,double pickableSpacing){IsPlant=isPlant;IsSapling=isSapling;GrowRadius=growRadius;ColliderRadius=colliderRadius;PickableSpacing=pickableSpacing;}
 }
 public sealed class SpacingOptions {
  public bool Minimize{get;} public double ExtraCrop{get;} public double ExtraSapling{get;}
  public SpacingOptions(bool minimize,double extraCrop,double extraSapling){Minimize=minimize;ExtraCrop=extraCrop;ExtraSapling=extraSapling;}
 }
 public static class PlantSpacing {
  /// <summary>
  /// Grid spacing of a piece. Saplings (a tree in their own or grown prefabs) use 2.2 times the growth radius.
  /// Crops use twice the growth radius, or 1.1 times plus the largest collider radius when spacing is minimized.
  /// Pickables use their configured pickable spacing. Plants add the extra crop or sapling spacing.
  /// </summary>
  public static double Compute(SpacingFacts facts,SpacingOptions options){
   if(options==null)throw new ArgumentNullException(nameof(options));
   if(!facts.IsPlant)return facts.PickableSpacing;
   if(facts.IsSapling)return facts.GrowRadius*2.2+options.ExtraSapling;
   double extra=(options.Minimize?facts.ColliderRadius:0)+options.ExtraCrop;
   return facts.GrowRadius*(options.Minimize?1.1:2)+extra;
  }
 }
 /// <summary>Source of uniform random numbers so scatter stays reproducible in tests.</summary>
 public interface IScatterRandom {double Range(double min,double max);int Range(int min,int maxExclusive);}
 public sealed class SeededScatterRandom:IScatterRandom {
  readonly Random random;public SeededScatterRandom(int seed){random=new Random(seed);}
  public double Range(double min,double max)=>min+random.NextDouble()*(max-min);
  public int Range(int min,int maxExclusive)=>random.Next(min,maxExclusive);
 }
 public sealed class ScatterOptions {
  public bool RandomizeRotation{get;} public bool Enabled{get;} public double PositionRadius{get;} public double TiltAngle{get;}
  public ScatterOptions(bool randomizeRotation,bool enabled,double positionRadius,double tiltAngle){RandomizeRotation=randomizeRotation;Enabled=enabled;PositionRadius=positionRadius;TiltAngle=tiltAngle;}
 }
 public readonly struct ScatteredPlacement {
  public Vec3 Position{get;} public double Yaw{get;} public double Pitch{get;} public double Roll{get;}
  public ScatteredPlacement(Vec3 position,double yaw,double pitch,double roll){Position=position;Yaw=yaw;Pitch=pitch;Roll=roll;}
 }
 public static class PlacementScatter {
  /// <summary>
  /// Final placement of one cell. Position scatter moves X and Z independently within +/- radius. A randomized
  /// rotation picks one of sixteen 22.5 degree yaw steps; otherwise the ghost yaw is kept. Rotation scatter tilts
  /// pitch and roll within +/- angle and never changes the yaw. Zero radius or angle disables that scatter.
  /// </summary>
  public static ScatteredPlacement Apply(Vec3 position,double ghostYaw,ScatterOptions options,IScatterRandom random){
   if(options==null||random==null)throw new ArgumentNullException(nameof(options));
   double radius=options.PositionRadius,angle=options.TiltAngle;
   if(options.Enabled&&radius!=0)position=new Vec3(position.X+random.Range(-radius,radius),position.Y,position.Z+random.Range(-radius,radius));
   double yaw=options.RandomizeRotation?22.5*random.Range(0,16):ghostYaw,pitch=0,roll=0;
   if(options.Enabled&&angle!=0){pitch=random.Range(-angle,angle);roll=random.Range(-angle,angle);}
   return new ScatteredPlacement(position,yaw,pitch,roll);
  }
 }
}
