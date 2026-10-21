using Burrow.Core;

namespace Burrow.Shared
{
    /// <summary>The "$burrow_shared_..." words of the module (SPEC 9.4 plus the few the spec left to the module).</summary>
    public static class SharedWords
    {
        public const string InUse = "$burrow_shared_inuse";
        public const string Moving = "$burrow_shared_moving";
        public const string NoAnswer = "$burrow_shared_noanswer";
        public const string Denied = "$burrow_shared_denied";
        public const string ReadOnly = "$burrow_shared_readonly";
        public const string Someone = "$burrow_shared_someone";
        public const string NoFit = "$burrow_shared_nofit";
        public const string ChestFull = "$burrow_shared_chestfull";
        public const string Unavailable = "$burrow_shared_unavailable";

        public static void Register()
        {
            Language.Add("burrow_shared_inuse", "in use by");
            Language.Add("burrow_shared_moving", "is moving this");
            Language.Add("burrow_shared_noanswer", "The chest did not answer");
            Language.Add("burrow_shared_denied", "Someone else got there first");
            Language.Add("burrow_shared_readonly", "Viewing only");
            Language.Add("burrow_shared_someone", "another player");
            Language.Add("burrow_shared_nofit", "Not enough room in your inventory");
            Language.Add("burrow_shared_chestfull", "No room in the chest");
            Language.Add("burrow_shared_unavailable", "The chest cannot be changed right now");
        }
    }
}
