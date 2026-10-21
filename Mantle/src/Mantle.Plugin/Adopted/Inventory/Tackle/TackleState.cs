using HarmonyLib;
using Mantle.Ring;

namespace Mantle.Tackle
{
    /// <summary>
    /// Whether the tacklebox's pop-up is open: each player's own, never saved. A right click on the box in its slot toggles
    /// it (<see cref="TackleboxUse"/>); the inventory opening or closing always closes it. The key ring's pop-up hangs in
    /// the same place, so opening one shuts the other. The gamepad's selection follows each change (<see cref="TackleGamepad"/>).
    /// </summary>
    public static class TackleState
    {
        public static bool Open { get; private set; }

        public static void Toggle()
        {
            Open = !Open;
            if (Open)
                KeyRingState.Close();
            TackleGamepad.Follow(Open);
        }

        public static void Close()
        {
            if (!Open)
                return;
            Open = false;
            TackleGamepad.Follow(false);
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public static class Shown
        {
            [HarmonyPostfix]
            public static void Postfix() => Close();
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        public static class Hidden
        {
            [HarmonyPostfix]
            public static void Postfix() => Close();
        }
    }
}
