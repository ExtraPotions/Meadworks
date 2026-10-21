using ExtraPotions.Meadworks.Crucible.UI;

namespace Mantle.Core
{
    /// <summary>Messages through the game's HUD, localized through <see cref="Language"/>. Safe before the HUD exists.</summary>
    public static class Messages
    {
        public static void Center(string text) => Show(MessageHud.MessageType.Center, text);

        public static void TopLeft(string text) => Show(MessageHud.MessageType.TopLeft, text);

        private static void Show(MessageHud.MessageType type, string text)
        {
            if (MessageHud.instance == null || string.IsNullOrEmpty(text))
                return;
            string localized = Language.Localize(text);
            var source = global::Mantle.Plugin.Source;
            if (source != null && source.IsActive && CrucibleUiPlugin.Active)
                KitNotices.Show(source, new KitNotice(localized, type == MessageHud.MessageType.Center ? KitNoticeKind.Center : KitNoticeKind.TopLeft));
            else MessageHud.instance.ShowMessage(type, localized);
        }
    }
}
