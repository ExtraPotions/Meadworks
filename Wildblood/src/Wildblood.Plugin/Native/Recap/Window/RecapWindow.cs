// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// Opens and closes the death recap window, building it on first use and again after the screen it lives beside is
    /// rebuilt (a new world). While open, the game treats it like its own windows (free cursor, no attacks, Esc closes;
    /// see the ExtraPotions.Wildblood.AdoptedLibs.WindowInput library), and nothing is recorded.
    /// </summary>
    internal static class RecapWindow
    {
        private static WindowView? _view;

        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>False when there is no game screen to build it on (the main menu).</summary>
        public static bool Open()
        {
            if (_view == null)
            {
                _view = WindowBuilder.Build();
            }
            if (_view == null)
            {
                return false;
            }
            _view.Show();
            return true;
        }

        public static void Close()
        {
            if (IsOpen)
            {
                _view!.Hide();
            }
        }
    }
}
