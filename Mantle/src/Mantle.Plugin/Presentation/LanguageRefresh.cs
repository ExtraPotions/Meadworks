using ExtraPotions.Mantle.Embedded.PatchGuard;
using Mantle.Panels;
using Mantle.Ring;

namespace Mantle.Presentation
{
    /// <summary>
    /// A language change (<c>Localization.SetLanguage</c>, which raises <c>OnLanguageChange</c> after Mantle's words are
    /// installed) rewrites at once everything Mantle wrote: slot captions, tabs, stat sheet, key ring notice and the
    /// comparison cards. The open crafting list and recipe text are rewritten too; the game rewrites the list only when it
    /// changes, so it kept the old language. Nothing waits for the next drawn frame, so no old-language text is ever shown.
    /// </summary>
    public static class LanguageRefresh
    {
        public static void Install() => Localization.OnLanguageChange += Relabel;

        public static void Uninstall() => Localization.OnLanguageChange -= Relabel;

        private static void Relabel()
        {
            InventoryGui gui = InventoryGui.instance;
            Guard.Run("language change", () =>
            {
                SlotElements.Relabel(gui);
                KeyRingNotice.Relabel();
                if (gui != null && InventoryGui.IsVisible() && Player.m_localPlayer != null)
                {
                    gui.UpdateCraftingPanel();
                    gui.UpdateRecipe(Player.m_localPlayer, 0f);
                }
            });
        }
    }
}
