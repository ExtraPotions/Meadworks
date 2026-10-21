using System;
using System.Collections.Generic;
using System.Linq;
namespace Wayfarer.Core {
 /// <summary>A rect inside the map panel: top-left origin, y grows downward, in panel units.</summary>
 public readonly struct PanelRect {
  public float X{get;} public float Y{get;} public float Width{get;} public float Height{get;}
  public PanelRect(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;}
  public float Right=>X+Width; public float Bottom=>Y+Height;
 }
 public enum SlotKind{Text,Button,Icon,Tab,Switch}
 /// <summary>
 /// One control of a panel screen. Focusable slots sit on the gamepad grid at <see cref="Row"/>, covering columns
 /// <see cref="Column"/> to <see cref="Column"/>+<see cref="Span"/>-1, so wide controls are reachable from every column above them.
 /// </summary>
 public readonly struct PanelSlot {
  public string Name{get;} public SlotKind Kind{get;} public PanelRect Rect{get;} public int Row{get;} public int Column{get;} public int Span{get;}
  public PanelSlot(string name,SlotKind kind,PanelRect rect,int row=-1,int column=0,int span=1){Name=name;Kind=kind;Rect=rect;Row=row;Column=column;Span=span;}
  public bool Focusable=>Row>=0;
  public IEnumerable<(int X,int Y)> Cells(){for(var i=0;i<Span;i++)yield return (Column+i,Row);}
 }
 public sealed class PanelScreen {
  public string Id{get;} public float Height{get;} public int Columns{get;} public IReadOnlyList<PanelSlot> Slots{get;}
  public PanelScreen(string id,float height,int columns,IReadOnlyList<PanelSlot> slots){Id=id;Height=height;Columns=columns;Slots=slots;}
  public PanelSlot this[string name]=>Slots.First(s=>s.Name==name);
  public bool Has(string name)=>Slots.Any(s=>s.Name==name);
 }
 /// <summary>
 /// Wayfarer map panel geometry for the kit UI (the 0.4.0 positions, with kit tabs), kept
 /// portable so sizes, fit and the gamepad grid are testable without the game.
 /// </summary>
 public static class PanelLayout {
  public const float Width=370f,Left=12f,Inner=346f,Top=62f,Row=46f,ButtonHeight=40f;
  public const float MinScale=.3f,Edge=24f;
  public const float TabHeight=26f,TabBetween=6f;
  public const int PageSize=6;
  public const int PickerColumns=6;
  /// <summary>The panel's fixed place as screen ratios; it cannot be moved (no drag, no saved position).</summary>
  public const float DefaultLeft=.084f,DefaultTop=.392f;
  public static readonly string[] MainActions={"Bed","Stones","Map","Advanced"};

  // Title row only: the settings icon and its screen moved to Almanac (F8 > Wayfarer), so the grid starts at the first control.
  static PanelSlot[] Header()=>new[]{
   new PanelSlot("Title",SlotKind.Text,new PanelRect(12,4,270,30)),
   new PanelSlot("Brand",SlotKind.Text,new PanelRect(12,31,270,20))};

  /// <summary>Main screen: allowed actions in fixed order (Bed, Stones, Map, Advanced); none shows the policy text.</summary>
  public static PanelScreen Main(IEnumerable<string> allowed) {
   var actions=MainActions.Where(allowed.Contains).ToArray();
   var slots=Header().ToList();
   for(var i=0;i<actions.Length;i++)slots.Add(new PanelSlot(actions[i],SlotKind.Button,new PanelRect(Left,Top+Row*i,Inner,ButtonHeight),i));
   if(actions.Length==0)slots.Add(new PanelSlot("PolicyDenied",SlotKind.Text,new PanelRect(Left,Top,Inner,ButtonHeight)));
   return new PanelScreen("main",124+Row*Math.Max(0,actions.Length-1),1,slots);
  }

  /// <summary>Equal tab widths across the inner width, as the kit's tab strip places them.</summary>
  public static PanelRect Tab(int index,int count) {
   if(count<=0||index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));
   var each=(Inner-TabBetween*(count-1))/count;return new PanelRect(Left+index*(each+TabBetween),Top,each,TabHeight);
  }

  /// <summary>Picker screen for 1 to 3 category tabs and <paramref name="shown"/> destinations on the current page.</summary>
  public static PanelScreen Picker(int categories,int shown) {
   if(categories<1||categories>3)throw new ArgumentOutOfRangeException(nameof(categories));
   if(shown<0||shown>PageSize)throw new ArgumentOutOfRangeException(nameof(shown));
   var slots=Header().ToList();var span=PickerColumns/categories;
   for(var i=0;i<categories;i++)slots.Add(new PanelSlot("Category"+i,SlotKind.Tab,Tab(i,categories),0,i*span,span));
   slots.Add(new PanelSlot("Help",SlotKind.Text,new PanelRect(12,96,346,64)));
   if(shown==0)slots.Add(new PanelSlot("Empty",SlotKind.Text,new PanelRect(12,168,346,48)));
   for(var i=0;i<shown;i++)slots.Add(new PanelSlot("Destination"+i,SlotKind.Button,new PanelRect(Left,168+i*39,Inner,34),1+i,0,PickerColumns));
   var pager=1+shown;
   slots.Add(new PanelSlot("Previous",SlotKind.Button,new PanelRect(12,409,46,30),pager,0,2));
   slots.Add(new PanelSlot("Page",SlotKind.Text,new PanelRect(74,409,100,30)));
   slots.Add(new PanelSlot("Next",SlotKind.Button,new PanelRect(180,409,46,30),pager,2,2));
   slots.Add(new PanelSlot("Refresh",SlotKind.Button,new PanelRect(232,409,126,30),pager,4,2));
   slots.Add(new PanelSlot("Cancel",SlotKind.Button,new PanelRect(Left,450,Inner,ButtonHeight),pager+1,0,PickerColumns));
   return new PanelScreen("picker",510,PickerColumns,slots);
  }

  /// <summary>Uniform scale so the panel fits a parent of the given size with an <see cref="Edge"/> margin; never below <see cref="MinScale"/>.</summary>
  public static float Scale(float parentWidth,float parentHeight,float height)=>Math.Max(MinScale,Math.Min(1f,Math.Min((parentHeight-Edge)/height,(parentWidth-Edge)/Width)));

  /// <summary>Top-left offset (x right, y down) for screen ratios (the fixed default), clamped so the scaled panel stays inside the parent.</summary>
  public static (float X,float Y) Position(float ratioX,float ratioY,float parentWidth,float parentHeight,float scaledWidth,float scaledHeight)=>
   (Clamp(ratioX*parentWidth,0,Math.Max(0,parentWidth-scaledWidth)),Clamp(ratioY*parentHeight,0,Math.Max(0,parentHeight-scaledHeight)));
  static float Clamp(float value,float min,float max)=>value<min?min:value>max?max:value;

  /// <summary>Destination pages: at least one, <see cref="PageSize"/> per page; the page index is clamped.</summary>
  public static (int Pages,int Page,int Shown) Page(int count,int page) {
   var pages=Math.Max(1,(count+PageSize-1)/PageSize);var clamped=Math.Max(0,Math.Min(page,pages-1));
   return (pages,clamped,Math.Max(0,Math.Min(PageSize,count-clamped*PageSize)));
  }
 }
}
