// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Wildblood lore entry points (design H4/WB14) and the read-only `wildblood` console route. The lore lives on one page
// of the game's own Compendium (Texts) list; the MeadWorks menu entry, `wildblood lore`, the Lore Window Key and the
// optional pad chord open the inventory on that page. The console route offers only safe verbs (`lore`, `reference`,
// `tier`, `inspect`, `theft status|recover`), is registered only while its name is free and is removed only while the
// registry still holds this exact command. The optional pad chord follows the C07 all-held/one-down/consume pattern.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.AdoptedCm;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class WildbloodLore
{
    // How long a requested open waits for the inventory to come up before it gives up.
    const float OpenWait = 2f;
    static Terminal.ConsoleCommand? command;
    static float pendingUntil = -1f;
    static string? padText;
    static IReadOnlyList<PadButton> padChord = Array.Empty<PadButton>();

    /// <summary>Lore content may be read: a graphical client with ready rules and an active Wildblood module.</summary>
    internal static bool ContentAvailable => Plugin.NativeRulesReady && Plugin.AllowsUi
        && (WildbloodModules.Active(WildbloodModule.Traits) || WildbloodModules.Active(WildbloodModule.Bosses) || WildbloodModules.Active(WildbloodModule.Stars));
    static bool Available => ContentAvailable && Player.m_localPlayer != null && CruciblePlugin.Session.AuthorityReady;
    /// <summary>The MeadWorks menu lists the lore entry while the Compendium page could open.</summary>
    internal static bool MenuAvailable => Available;
    /// <summary>The game's Compendium is open in the inventory.</summary>
    internal static bool IsOpen => CreatureCompendium.Showing;

    static string T(string key) => WildbloodText.Text(key);

    internal static void Tick()
    {
        EnsureCommand();
        if (!Available) { pendingUntil = -1f; return; }
        var key = Plugin.Settings!.Shortcut(WildbloodSettings.Display.LoreWindowKey);
        var context = IsOpen ? InputContext.Window : InputContext.Gameplay;
        string text = Plugin.Settings.Get(WildbloodSettings.Display.LoreWindowPadChord);
        if (text != padText) { padText = text; padChord = PadChord.Parse<PadButton>(text, GamepadMap.TryParse); }
        // Evaluate both routes so a simultaneous pad chord is consumed, then toggle once.
        bool pressed = MeadworksInput.Pressed(key, context);
        bool pad = PadChord.Pressed(padChord, MeadworksInput.GamepadActive, MeadworksInput.Allows(context),
            MeadworksInput.PadHeld, MeadworksInput.PadDown, MeadworksInput.PadConsume);
        if (pressed || pad)
        { if (IsOpen) Release(); else Open(); }
        if (pendingUntil >= 0f && (CreatureCompendium.TryShow() || Time.unscaledTime > pendingUntil)) pendingUntil = -1f;
    }

    /// <summary>Opens the inventory on the Wildblood Compendium page; the page itself appears once the inventory is up.</summary>
    internal static bool Open()
    {
        if (!Available || InventoryGui.instance == null) return false;
        if (CreatureCompendium.TryShow()) { pendingUntil = -1f; return true; }
        InventoryGui.instance.Show(null);
        pendingUntil = Time.unscaledTime + OpenWait;
        return true;
    }

    static void EnsureCommand()
    {
        if (!Plugin.AllowsUi || command != null || Terminal.commands.ContainsKey(LoreBook.Command)) return;
        command = new Terminal.ConsoleCommand(LoreBook.Command, T("lore_help"), (Terminal.ConsoleEventArgs args) =>
        {
            var route = LoreBook.Route(args.Args);
            if (!Permitted(route, args.Context)) return;
            switch (route)
            {
                case LoreRoute.Spawn:
                    Guard.Run("wildblood spawn", () => Adopted.Commands.SpawnCommand.Run(args));
                    break;
                case LoreRoute.Purge:
                    Guard.Run("wildblood purge", () => Adopted.Commands.PurgeCommand.Run(args));
                    break;
                case LoreRoute.Effects:
                    Guard.Run("wildblood effects", () => Adopted.Commands.EffectsCommand.Run(args));
                    break;
                case LoreRoute.Karma:
                    Guard.Run("wildblood karma", () => Adopted.Commands.KarmaCommand.Run(args));
                    break;
                case LoreRoute.Lore:
                    if (!Open()) args.Context?.AddString(T("lore_unavailable"));
                    break;
                case LoreRoute.Reference:
                    Reference(args.Context);
                    break;
                case LoreRoute.Tier:
                    if (!ContentAvailable) args.Context?.AddString(T("lore_unavailable"));
                    else Adopted.Commands.TierCommand.Run(args); // reads the world keys this machine holds; changes nothing
                    break;
                case LoreRoute.Inspect:
                    if (!ContentAvailable) args.Context?.AddString(T("lore_unavailable"));
                    else if (!LoreBook.InspectAllowed(CruciblePlugin.Session.AuthorityReady, CruciblePlugin.Session.IsAdmin,
                        Plugin.Settings?.Get(WildbloodSettings.Traits.PlayersMayInspect) == true)) args.Context?.AddString(T("inspect_locked"));
                    else Adopted.Commands.InspectCommand.Run(args); // reads already-synced creature data only
                    break;
                case LoreRoute.TheftStatus:
                    TheftStatus(args.Context);
                    break;
                case LoreRoute.TheftRecover:
                    TheftRecover(args.Context);
                    break;
                default:
                    args.Context?.AddString(T("lore_help"));
                    args.Context?.AddString(T("lore_help_more"));
                    break;
            }
        });
    }

    // The admin verbs (effects) and the Creature Commands cheat verbs (spawn, purge, karma) need a ready authority and a
    // server-confirmed admin (Crucible C02); the cheat verbs also need the cheat on, which a locked server evaluates to off.
    static bool Permitted(LoreRoute route, Terminal? context)
    {
        bool on = Plugin.Settings != null && Plugin.Settings.Get(WildbloodSettings.Cheats.CreatureCommands);
        switch (LoreBook.Gate(route, CruciblePlugin.Session.AuthorityReady, CruciblePlugin.Session.IsAdmin, on))
        {
            case CommandGate.Allowed:
                return true;
            case CommandGate.AuthorityNotReady:
                context?.AddString(MeadworksBlockedOptions.Reason(ConfigBlockReason.AuthorityPending));
                return false;
            case CommandGate.NotAdmin:
                context?.AddString(MeadworksBlockedOptions.Reason(ConfigBlockReason.NotAdmin));
                return false;
            default:
                Plugin.Settings?.TryUseCheat(WildbloodSettings.Cheats.CreatureCommands, T("cheat_creature_commands"));
                context?.AddString(T("commands_off"));
                return false;
        }
    }

    // Read-only: the local player's retained theft intents. Nothing is sent, refunded or resent.
    static void TheftStatus(Terminal? context)
    {
        if (!Adopted.Runtime.ThievingRpc.Active || Player.m_localPlayer == null) { context?.AddString(T("theft_unavailable")); return; }
        var lines = TheftTransfers.Status();
        if (lines.Count == 0) { context?.AddString(T("theft_status_none")); return; }
        foreach (var line in lines)
            context?.AddString(WildbloodText.Format("theft_status_line", line.Root, line.Counter, T("theft_phase_" + line.Phase), line.Legacy ? T("theft_legacy") : ""));
    }

    // Explicit recovery: one status query per eligible v2 intent to the thief's current owner. Only that owner's exact
    // answer is recorded (later, by the answer handler); Unknown and legacy intents stay retained as they are.
    static void TheftRecover(Terminal? context)
    {
        if (!Adopted.Runtime.ThievingRpc.Active || Player.m_localPlayer == null) { context?.AddString(T("theft_unavailable")); return; }
        var (asked, unreachable, legacy) = TheftTransfers.Recover(Adopted.Runtime.ThievingStatus.Ask);
        context?.AddString(WildbloodText.Format("theft_recover_done", asked, unreachable, legacy));
    }

    // Collects the game's own registered creatures and writes the local report file; no world or creature state changes.
    static void Reference(Terminal? context)
    {
        if (!ContentAvailable) { context?.AddString(T("lore_unavailable")); return; }
        if (ZNetScene.instance == null) { context?.AddString(T("lore_reference_world")); return; }
        string? path = Adopted.Commands.ReferenceCommand.Export(out int count);
        context?.AddString(path != null ? WildbloodText.Format("lore_reference_done", count, LoreBook.Literal(path, 260)) : T("lore_reference_failed"));
    }

    /// <summary>Closes the inventory when it shows the Compendium and drops a pending open.</summary>
    internal static void Release()
    {
        pendingUntil = -1f;
        if (IsOpen) CreatureCompendium.Close();
    }

    internal static void Shutdown()
    {
        pendingUntil = -1f;
        if (command != null && Terminal.commands.TryGetValue(LoreBook.Command, out var current) && ReferenceEquals(current, command))
            Terminal.commands.Remove(LoreBook.Command);
        command = null; padText = null; padChord = Array.Empty<PadButton>();
    }
}
