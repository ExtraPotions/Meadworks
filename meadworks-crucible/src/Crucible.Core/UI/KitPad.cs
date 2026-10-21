using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace ExtraPotions.Meadworks.Crucible {
    public enum KitPadAction { None, Move, Activate, Close }

    /// <summary>
    /// Gamepad control of a kit window (C07 integration): the D-pad moves the focus between the window's controls on a
    /// <see cref="GridNavigator"/> that skips hidden and disabled ones, A presses the focused control, B closes the
    /// window (and is consumed, so the game does not also close what is under it, as PackPanel's pop-ups do).
    /// </summary>
    public static class KitPad {
        public static IReadOnlyList<PadButton> Polled { get; }=new ReadOnlyCollection<PadButton>(new[]{PadButton.DPadUp,PadButton.DPadDown,PadButton.DPadLeft,PadButton.DPadRight,PadButton.A,PadButton.B});

        public static KitPadAction ActionOf(PadButton button){
            switch(button){
                case PadButton.A:return KitPadAction.Activate;
                case PadButton.B:return KitPadAction.Close;
                case PadButton.DPadUp:case PadButton.DPadDown:case PadButton.DPadLeft:case PadButton.DPadRight:return KitPadAction.Move;
                default:return KitPadAction.None;
            }
        }

        /// <summary>Where the focus goes from <paramref name="from"/> for a pad button; unchanged for a non-moving button.</summary>
        public static GridCell Next(GridNavigator navigator,GridCell from,PadButton button)=>
            ActionOf(button)==KitPadAction.Move?navigator.Move(from,GamepadMap.Direction(button)):from;

        /// <summary>The first shown control in reading order, or null when the window has none.</summary>
        public static GridCell? Start(GridNavigator navigator)=>navigator.First();
    }
}
