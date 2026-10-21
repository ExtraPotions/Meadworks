// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Presentation owner over the adopted WorldTier and CM Karma read paths.
using System;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Patches;
using ExtraPotions.Wildblood.AdoptedCm;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using NativeTier = ExtraPotions.Wildblood.Adopted.Runtime.WorldTier;

namespace ExtraPotions.Wildblood.Runtime;

internal static class WildbloodStatus
{
    static readonly System.Version Version = new(1, 0);
    static readonly Tuple<string, string> EmptyHud = Tuple.Create("", "");
    static Tuple<string, string> hud = EmptyHud;
    static Player? actor;
    static ZNet? network;
    static long generation = -1, world, profile;
    static string session = "";
    static bool warnedHud;
    // The tier line is formatted again only when the tier, the language or the catalog's texts change (it was every frame).
    static int tierShown = int.MinValue, tierLanguage = -1;
    static long tierRevision = -1;
    static string tierLine = "";
    // The Karma line was rebuilt every frame (sector keys, the ledger lookup and a formatted string). It is now worked out
    // at most four times a second, and at once when the line comes back (visible again, a new session, a reset).
    static readonly RefreshInterval karmaRefresh = new(0.25f);

    internal static void Register(ProductRegistration registration)
    {
        registration.AddCapability<Func<Tuple<string, string>>>("meadworks.wildblood.hud", Version, ReadHud);
    }
    static bool Available => EnemyHudPatch.Available && CrucibleUiPlugin.Active && Player.m_localPlayer != null
        && Player.m_localPlayer.m_nview != null && Player.m_localPlayer.m_nview.IsValid() && Player.m_localPlayer.m_nview.IsOwner();
    static bool Current => Available && actor == Player.m_localPlayer && ReferenceEquals(network, ZNet.instance)
        && generation == CruciblePlugin.Session.Generation && world == CruciblePlugin.Session.WorldId
        && session == CruciblePlugin.Session.SessionId && profile == Player.m_localPlayer.GetPlayerID();
    static bool TierEnabled => WildbloodModules.Active(WildbloodModule.Stars) && Plugin.Settings!.Get(WildbloodSettings.WorldTier.Enabled);
    // The Karma line keeps its own gates; the world tier no longer depends on them (user ruling 2026-10-12: always visible).
    // It stays under the Esc menu, as Solo's does, and hides while a MeadWorks window is open, as the game's HUD does.
    static bool KarmaVisible => Current && !Hud.IsUserHidden() && !Minimap.IsOpen() && !InventoryGui.IsVisible() && !KitWindows.HudBlocked
        && Plugin.Settings!.Get(WildbloodSettings.Display.ShowKarmaUnderMinimap);
    static string Bounded(string text, int max)
    {
        if (text.Length > max) return "";
        foreach (char c in text) if (char.IsControl(c) || c == '\u2028' || c == '\u2029') return "";
        return text;
    }
    static string TierLine()
    {
        int tier = NativeTier.Current();
        if (tier != tierShown || WildbloodText.LanguageVersion != tierLanguage || WildbloodText.TextRevision != tierRevision)
        {
            tierLine = Bounded(WildbloodText.Format("world_tier_label", tier), 256);
            tierShown = tier; tierLanguage = WildbloodText.LanguageVersion; tierRevision = WildbloodText.TextRevision;
        }
        return tierLine;
    }

    // meadworks.wildblood.hud is (world tier line, Karma line); Mantle reads the tier number from Item1 for its globe square, tier 0 included.
    // The tier line is there whenever the world has tiers (Stars module and World Tier on); the consumer decides when its column shows.
    // Provider callbacks read only cached owned data and live gates; they never resolve or invoke consumers.
    internal static Tuple<string, string> ReadHud()
    {
        if (!Current) return EmptyHud;
        string tier = TierEnabled ? hud.Item1 : "";
        string karma = KarmaVisible && WildbloodModules.Active(WildbloodModule.Pressure) ? hud.Item2 : "";
        return tier == hud.Item1 && karma == hud.Item2 ? hud : Tuple.Create(tier, karma);
    }

    internal static void Tick()
    {
        if (!Available) { Reset(); return; }
        if (!Current)
        {
            Reset(); actor = Player.m_localPlayer; network = ZNet.instance;
            generation = CruciblePlugin.Session.Generation; world = CruciblePlugin.Session.WorldId;
            session = CruciblePlugin.Session.SessionId; profile = actor!.GetPlayerID();
        }
        string line = TierEnabled ? TierLine() : "";
        string karma;
        if (!(KarmaVisible && WildbloodModules.Active(WildbloodModule.Pressure))) { karma = ""; karmaRefresh.Reset(); }
        else if (karmaRefresh.Due(Time.unscaledTime)) karma = Bounded(CreatureKarmaManager.GetMinimapStatus(actor!.transform.position), 256);
        else karma = hud.Item2;
        // Same tuple while both lines are unchanged: no new object every frame.
        if (line != hud.Item1 || karma != hud.Item2) hud = Tuple.Create(line, karma);
        RefreshHud();
    }
    internal static void RefreshHud()
    {
        var data = ReadHud();
        TierHud.Draw(data.Item2, data.Item2.Length > 0 && Consumed("meadworks.hud.lines", ref warnedHud));
    }
    static bool Consumed(string id, ref bool warned)
    {
        if (!Current) return false;
        try
        {
            return CruciblePlugin.Services.Capabilities.TryResolve<Func<string, bool>>(id, Version, out var showing)
                && showing != null && showing(Plugin.Guid);
        }
        catch (Exception error)
        {
            if (!warned) Plugin.NativeLogger?.LogWarning("Wildblood optional display unavailable: " + error.GetType().Name);
            warned = true; return false;
        }
    }
    internal static void Reset()
    {
        hud = EmptyHud; actor = null; network = null;
        generation = -1; world = profile = 0; session = "";
        TierHud.Reset(); warnedHud = false;
        tierShown = int.MinValue; tierLine = "";
        karmaRefresh.Reset();
    }
}
