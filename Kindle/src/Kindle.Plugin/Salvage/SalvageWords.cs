// OpenKeep 3.0.0 blocker/message keys adapted to Kindle's shared localization.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    public static class SalvageWords
    {
        public static string Salvage => KindleText.Text("salvage");
        public static string Ask => KindleText.Text("salvage_ask");
        public static string Empty => KindleText.Text("salvage_empty");
        public static string Nothing => KindleText.Text("salvage_nothing");
        public static string NoFit => KindleText.Text("salvage_nofit");
        public static string Off => KindleText.Text("salvage_off");
        public static string Cannot => KindleText.Text("salvage_cannot");
        public static string NoRecipe => KindleText.Text("salvage_norecipe");
        public static string Favourite => KindleText.Text("salvage_favorite");
        public static string Excluded => KindleText.Text("salvage_excluded");
        public static string ModData => KindleText.Text("salvage_moddata");
        public static string Equipped => KindleText.Text("salvage_equipped");
        public static string Unknown => KindleText.Text("salvage_unknown");
        public static string NoStation => KindleText.Text("salvage_nostation");
        public static string Done => KindleText.Text("salvage_done");
    }
    internal static class Messages
    {
        internal static void Center(string text) { if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, text); }
        internal static void TopLeft(string text) { if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text); }
    }
}
