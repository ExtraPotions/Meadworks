// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Thing.Admin
{
    public static class SettingsAccess
    {
        public static bool CanConfigure(bool localAuthority, bool listedAdmin, bool delegated, bool remoteEditing) =>
            localAuthority || listedAdmin && delegated && remoteEditing;
    }
}
