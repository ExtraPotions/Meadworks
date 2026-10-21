// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (user parity row 125, 2026-10-10): an admin's own item pickup radius, 1 to 10 meters, on the admin's
// own machine (the game picks items up on the client). Valheim_Admin_Tool's admin pickup radius is a behaviour reference
// only (no code used). Magpie scales its own pickup by the live game radius, so it follows this too.
using System;
using System.Globalization;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Integration;
using ExtraPotions.Thing.WorldAdmin;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingPickup
    {
        static float? wanted;
        static MeadworksLog? log;

        public static void Initialize(MeadworksLog owner) => log = owner;

        /// <summary>The radius the admin chose, or null while the game's own radius applies.</summary>
        public static float? Wanted => wanted;

        public static bool Run(string[] args)
        {
            if (args.Length == 0 || !string.Equals(args[0], "pickup", StringComparison.OrdinalIgnoreCase)) return false;
            global::Console.instance?.Print(Apply(args.Length == 2 ? args[1] : null));
            return true;
        }

        /// <summary>Sets ("1".."10") or resets ("reset") the local admin's pickup radius; the line to show.</summary>
        public static string Apply(string? text)
        {
            if (!ThingModules.Active(ThingModule.WorldTools)) return ThingText.Text("refused_product_off");
            if (!ThingPanel.CanAdmin) return ThingText.Text("refused_not_admin");
            if (text == null || !PickupRadius.TryParse(text, out var radius)) return ThingText.Text("pickup_usage");
            wanted = radius;
            var player = Player.m_localPlayer;
            if (player != null) player.m_autoPickupRange = radius ?? Base();
            log?.Info("Thing pickup radius " + (radius.HasValue ? Metres(radius.Value) + " m" : "reset to " + Metres(Base()) + " m") + ".");
            return radius.HasValue ? ThingText.Format("pickup_set", Metres(radius.Value)) : ThingText.Format("pickup_reset", Metres(Base()));
        }

        /// <summary>Every frame: a new character (respawn, another world) gets the chosen radius again; losing admin resets it.</summary>
        public static void Tick()
        {
            var player = Player.m_localPlayer;
            if (!wanted.HasValue || player == null) return;
            if (!ThingPanel.CanAdmin)
            {
                player.m_autoPickupRange = Base();
                wanted = null;
                log?.Info("Thing pickup radius reset: no longer an admin here.");
                return;
            }
            if (PickupRadius.Reapply(wanted, player.m_autoPickupRange)) player.m_autoPickupRange = wanted!.Value;
        }

        public static void Shutdown()
        {
            if (wanted.HasValue && Player.m_localPlayer != null) Player.m_localPlayer.m_autoPickupRange = Base();
            wanted = null;
        }

        /// <summary>The game's own radius, from the player prefab.</summary>
        public static float Base()
        {
            var prefab = Game.instance != null ? Game.instance.m_playerPrefab : null;
            var template = prefab != null ? prefab.GetComponent<Player>() : null;
            return template != null ? template.m_autoPickupRange : 2f;
        }

        static string Metres(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
