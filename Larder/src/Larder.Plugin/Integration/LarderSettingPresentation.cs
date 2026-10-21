// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Larder.Foods;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Larder.Runtime
{
    /// <summary>
    /// Presentation tag for the per-food, per-mead and per-station entries: the translated templates of
    /// <see cref="SettingTemplates"/> formatted with the item's name in the game's language. English keeps the
    /// config text as bound. Names are read on each call, so a language change shows at the next redraw.
    /// </summary>
    internal static class LarderSettingPresentation
    {
        /// <param name="heading">The section's subject (food, mead or station name).</param>
        /// <param name="help">The help template's arguments.</param>
        /// <param name="label">The entry's label; null uses the template name for the key.</param>
        internal static ConfigPresentation<float> For(string kind, string key, Func<string> heading, Func<object[]> help, Func<string>? label = null)
            => new ConfigPresentation<float>(Plugin.Guid, (_, language) =>
            {
                var text = LarderText.Translations;
                if (text == null || MeadworksLanguages.Normalize(language) == MeadworksLanguages.English)
                    return null;
                return new ConfigPresentationText(
                    label != null ? label() : text.Text(SettingTemplates.NameKey(kind, key), language),
                    text.Format(SettingTemplates.HeadingKey(kind), language, heading()),
                    text.Format(SettingTemplates.HelpKey(kind, key), language, help()));
            });
    }
}
