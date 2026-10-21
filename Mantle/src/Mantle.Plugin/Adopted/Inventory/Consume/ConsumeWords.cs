using Mantle.Core;

namespace Mantle.Consume
{
    /// <summary>The $mantle_ words of the Food Key and the Mead Slot keys.</summary>
    public static class ConsumeWords
    {
        public static string NothingToEat { get; private set; }
        public static string SlotEmpty { get; private set; }

        public static void Register()
        {
            NothingToEat = Language.Add("mantle_nothingtoeat", "Nothing in your food slots can be eaten now");
            SlotEmpty = Language.Add("mantle_meadslotempty", "That mead slot is empty");
        }
    }
}
