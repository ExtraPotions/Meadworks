using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    public enum PadButton { A, B, X, Y, LeftBumper, RightBumper, LeftTrigger, RightTrigger, Back, Start, LeftStick, RightStick, DPadUp, DPadDown, DPadLeft, DPadRight }

    /// <summary>Gamepad buttons to Valheim <c>ZInput</c> button names (verified against assembly_utils), labels and grid directions.</summary>
    public static class GamepadMap {
        static readonly string[] ZInput={"JoyButtonA","JoyButtonB","JoyButtonX","JoyButtonY","JoyLBumper","JoyRBumper","JoyLTrigger","JoyRTrigger","JoyBack","JoyStart","JoyLStick","JoyRStick","JoyDPadUp","JoyDPadDown","JoyDPadLeft","JoyDPadRight"};
        static readonly string[] Labels={"A","B","X","Y","LB","RB","LT","RT","Back","Start","LS","RS","D-pad Up","D-pad Down","D-pad Left","D-pad Right"};
        static readonly Dictionary<string,PadButton> Names=BuildNames();
        public static string ZInputName(PadButton button)=>ZInput[Index(button)];
        public static string Label(PadButton button)=>Labels[Index(button)];
        /// <summary>Accepts enum names, ZInput names, short labels (LB, RS) and Wildroot pad aliases (PadUp, PadRightStick).</summary>
        public static bool TryParse(string? name,out PadButton button){button=default;return name!=null&&Names.TryGetValue(name.Trim(),out button);}
        /// <summary>D-pad step in grid space (x right, y down); zero for other buttons.</summary>
        public static GridCell Direction(PadButton button){
            switch(button){
                case PadButton.DPadUp:return new GridCell(0,-1);case PadButton.DPadDown:return new GridCell(0,1);
                case PadButton.DPadLeft:return new GridCell(-1,0);case PadButton.DPadRight:return new GridCell(1,0);
                default:return new GridCell(0,0);
            }
        }
        static int Index(PadButton button){var index=(int)button;if(index<0||index>=ZInput.Length)throw new ArgumentOutOfRangeException(nameof(button));return index;}
        static Dictionary<string,PadButton> BuildNames(){
            var names=new Dictionary<string,PadButton>(StringComparer.OrdinalIgnoreCase);
            foreach(PadButton button in Enum.GetValues(typeof(PadButton))){names[button.ToString()]=button;names[ZInputName(button)]=button;names[Label(button).Replace(" ","").Replace("-","")]=button;}
            names["Select"]=PadButton.Back;names["Menu"]=PadButton.Start;
            names["PadUp"]=PadButton.DPadUp;names["PadDown"]=PadButton.DPadDown;names["PadLeft"]=PadButton.DPadLeft;names["PadRight"]=PadButton.DPadRight;
            names["PadLeftStick"]=PadButton.LeftStick;names["PadRightStick"]=PadButton.RightStick;
            return names;
        }
    }
}
