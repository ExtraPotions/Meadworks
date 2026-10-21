// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Commands;
using ExtraPotions.Thing.Config;
using ExtraPotions.Thing.Localization;
using ExtraPotions.Thing.Moderation;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingLists
    {
        internal static IReadOnlyList<string> Read(bool admins)
        {
            var net = ZNet.instance;
            if (!Plugin.Ready || net == null || !net.IsServer() || !ThingServer.Active
                || !CruciblePlugin.Session.AuthorityReady || !ThingModules.ProductActive(out _))
                throw new InvalidOperationException("ListsUnavailable");
            var list = admins ? net.m_adminList : net.m_permittedList;
            if (list == null) throw new InvalidOperationException("ListsUnavailable");
            return Array.AsReadOnly(list.GetList().Take(10000).Where(GameListIdentity.Valid).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
        internal static IEnumerable<string> Run(ParsedCommand command, Actor admin, bool localAuthority)
        {
            var net = ZNet.instance;
            var isAdmins = command.Action == ThingAction.AdminListView || command.Action == ThingAction.AdminListEdit;
            if (!Plugin.Ready || net == null || !net.IsServer() || !ThingServer.Active || !CruciblePlugin.Session.AuthorityReady
                || !ThingModules.ProductActive(out _) || (!localAuthority && !net.IsAdmin(admin.Id)))
            { yield return ThingText.Text(AdminCheck.TextKey(Verdict.NotAdmin)); yield break; }
            if (command.Action == ThingAction.AdminListView || command.Action == ThingAction.PermitListView)
            {
                var rows = Read(isAdmins);
                yield return (isAdmins ? "admins" : "permitted") + " " + command.Minutes.ToString(CultureInfo.InvariantCulture) + "/" + rows.Count.ToString(CultureInfo.InvariantCulture);
                foreach (var id in rows.Skip(command.Minutes).Take(12)) yield return id;
                ThingServer.Audit(admin, command.Action, default, AuditOutcome.Done, "native list viewed");
                yield break;
            }
            if (isAdmins && (Plugin.Settings == null || !Plugin.Settings.Get(ThingSettings.General.AdminListEditable)))
            { yield return ThingText.Text(AdminCheck.TextKey(Verdict.DisabledByServer)); yield break; }
            var list = isAdmins ? net.m_adminList : net.m_permittedList;
            if (list == null || !GameListIdentity.Valid(command.Target))
            { yield return ThingText.Format(ThingTextKeys.CommandFailed, "InvalidPlatformId"); yield break; }
            var adding = command.Action == ThingAction.PermitAdd || command.Text == "add";
            var matches = list.GetList().Where(id => GameListFile.Contains(new[] { id }, command.Target)).ToArray();
            bool verified;
            try
            {
                if (adding) { if (matches.Length == 0) list.Add(command.Target); }
                else foreach (var id in matches) list.Remove(id);
                // SyncedList.Save swallows some write failures. Read its actual native destination before reporting Done.
                var location = list.m_fileLocation;
                var reader = new FileReader(location.m_path, location.m_fileSource, FileHelpers.FileHelperType.Stream);
                try
                {
                    var text = new StringBuilder(); var buffer = new char[4096]; int count;
                    while ((count = reader.m_stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (text.Length + count > 1048576) throw new InvalidOperationException("NativeListTooLarge");
                        text.Append(buffer, 0, count);
                    }
                    verified = GameListFile.Contains(GameListFile.Parse(text.ToString()), command.Target) == adding;
                }
                finally { reader.Dispose(); }
                if (isAdmins) net.SendAdminList();
            }
            catch { verified = false; }
            ThingServer.Audit(admin, command.Action, new Actor(command.Target, ""), verified ? AuditOutcome.Done : AuditOutcome.Failed,
                (adding ? "add" : "remove") + (verified ? " native list verified" : " native list outcome unverified; do not automatically retry"));
            yield return verified ? ThingText.Format(ThingTextKeys.CommandDone, ActionCatalog.Info(command.Action).Verb + " " + command.Target)
                : ThingText.Format(ThingTextKeys.CommandFailed, "NativeListOutcomeUnverified");
        }
    }
}
