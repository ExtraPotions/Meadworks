using HarmonyLib;

namespace Mantle.Ring
{
    /// <summary>
    /// Whether the key ring's pop-up is open: each player's own, never saved. The ring button toggles it
    /// (<see cref="KeyRingClicks"/>); the inventory opening or closing always closes it, so the inventory opens with the
    /// ring shut. The gamepad's selection follows each change (<see cref="KeyRingGamepad.Follow"/>).
    /// </summary>
    public static class KeyRingState
    {
        public static bool Open { get; private set; }

        public static void Toggle()
        {
            Open = !Open;
            if (Open)
                Tackle.TackleState.Close();   // the tacklebox's pop-up hangs in the same place
            KeyRingGamepad.Follow(Open);
        }

        public static void Close()
        {
            if (!Open)
                return;
            Open = false;
            KeyRingGamepad.Follow(false);
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
