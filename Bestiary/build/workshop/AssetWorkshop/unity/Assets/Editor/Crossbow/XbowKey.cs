namespace Workshop.Crossbow
{
    /// <summary>
    /// Where the crossbow is in a key (it rides the left fist, <see cref="XbowGrip"/>): carried at the low ready, across
    /// the belly as the carry clips hold it (<see cref="XbowCarry"/>); on its way up; at the right shoulder, aimed;
    /// kicked up by the shot; lowered in front of the belly to be spanned and loaded.
    /// </summary>
    public enum Bow { Carry, Raised, Aim, Kick, Lowered }

    /// <summary>
    /// What the right hand does in a key: as in the idle; round the stock's wrist, a finger on the trigger lever;
    /// hooking the let-go string; holding it in the nut; on its way to the quiver; in the quiver on a bolt; lifting it
    /// out; laying it in the groove; letting go of it.
    /// </summary>
    public enum Hand { Idle, Wrist, StringGrab, Spanned, ToQuiver, InQuiver, BoltOut, Lay, Pat }

    /// <summary>
    /// One key of a crossbowman clip. Yaw turns the chest (positive takes the right shoulder back, the stance for a stock
    /// at the right shoulder); Lean bows the back forward over the lowered crossbow (degrees); Cheek lays the head onto
    /// the stock to sight along it (0 to 1); Down bows the head to watch the hands (degrees); Grip closes the right
    /// fingers (0 as in the idle, 1 closed, below zero opened). The left fist is always closed round the fore-stock.
    /// </summary>
    public sealed class XbowKey
    {
        public float Time;
        public Bow Bow;
        public Hand Right;
        public float Yaw;
        public float Lean;
        public float Cheek;
        public float Down;
        public float Grip;

        public XbowKey(float time) => Time = time;
    }
}
