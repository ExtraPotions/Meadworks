// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGatePickerEdits.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text;

using ExtraPotions.Threshold.Access;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>What a gate edit changes.</summary>
    internal enum SeaGateEditKind
    {
        Name = 1,
        Mode = 2
    }

    /// <summary>One gate edit: what, the value, and who asked. The identity is filled in by the server, never taken
    /// from the client that asked.</summary>
    internal readonly struct SeaGateEdit
    {
        public readonly SeaGateEditKind Kind;
        public readonly long PlayerId;
        public readonly bool IsAdmin;
        public readonly long Value;
        public readonly string Text;

        public SeaGateEdit(SeaGateEditKind kind, long playerId, bool isAdmin, long value, string text)
        {
            Kind = kind;
            PlayerId = playerId;
            IsAdmin = isAdmin;
            Value = value;
            Text = text ?? "";
        }

        /// <summary>The same edit on behalf of another player.</summary>
        public SeaGateEdit From(long playerId, bool isAdmin) => new SeaGateEdit(Kind, playerId, isAdmin, Value, Text);

        public ZPackage Write()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write((int)Kind);
            pkg.Write(PlayerId);
            pkg.Write(IsAdmin);
            pkg.Write(Value);
            pkg.Write(Text);
            return pkg;
        }

        /// <summary>False for a package that is not an edit (another build of the mod, a forged message).</summary>
        public static bool TryRead(ZPackage pkg, out SeaGateEdit edit)
        {
            edit = default;
            try
            {
                SeaGateEditKind kind = (SeaGateEditKind)pkg.ReadInt();
                edit = new SeaGateEdit(kind, pkg.ReadLong(), pkg.ReadBool(), pkg.ReadLong(), pkg.ReadString());
                return kind >= SeaGateEditKind.Name && kind <= SeaGateEditKind.Mode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>The two things players change on a gate - its name and its access mode - and who may.
    /// A client never sends an edit straight to the anchor's owner: when that owner is another client it cannot tell
    /// who sent it (a client's only peer is the server, so <see cref="SenderIdentity"/> there names the owner's own
    /// player, and an Alt+E would make the owner the gate's owner). The edit goes to the server instead
    /// (<see cref="EditRpc"/>), which knows every peer's player and admin rights and holds every ZDO. It checks the
    /// anchor rule (the pillar's id below its partner's) and the right to change the gate (<see cref="GateAccess.MayCycle"/>,
    /// the right to cycle its access mode), then writes the edit itself where it owns the anchor or nobody does, and
    /// otherwise forwards it with the requester's identity to the anchor's owner (<see cref="SeaGateFields.SetNameRpc"/>,
    /// <see cref="SeaGateFields.SetModeRpc"/> on the pillar's ZNetView). The owner takes it only from the server, checks
    /// the right again on its own copy of the ZDO and writes.</summary>
    internal static class SeaGatePickerEdits
    {
        internal const int NameLimit = 20;

        /// <summary>Routed to the server: the anchor's ZDOID and a <see cref="SeaGateEdit"/>.</summary>
        internal const string EditRpc = "threshold.SeaGateEdit";

        private static bool On => SeaGateConfig.On;

        /// <summary>On every new session's <c>ZRoutedRpc</c>, every machine (only the server answers).</summary>
        internal static void Register(ZRoutedRpc rpc) => rpc.Register<ZDOID, ZPackage>(EditRpc, OnRequest);

        /// <summary>A rename typed into the text box.</summary>
        internal static void SendName(SeaGatePillar anchor, string name) =>
            Send(anchor, new SeaGateEdit(SeaGateEditKind.Name, 0L, false, 0L, name));

        /// <summary>Alt+E: the gate's next access mode.</summary>
        internal static void SendMode(SeaGatePillar anchor, AccessMode mode) =>
            Send(anchor, new SeaGateEdit(SeaGateEditKind.Mode, 0L, false, (long)mode, ""));

        private static void Send(SeaGatePillar anchor, SeaGateEdit edit)
        {
            if (anchor == null || !anchor.IsValid || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.InvokeRoutedRPC(EditRpc, anchor.Zdo.m_uid, edit.Write());
        }

        /// <summary>Server: an edit from a player. The identity is the sender's, resolved here.</summary>
        private static void OnRequest(long sender, ZDOID anchorId, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
                return;
            if (!SeaGateEdit.TryRead(pkg, out SeaGateEdit asked))
                return;
            ZDO zdo = ZDOMan.instance.GetZDO(anchorId);
            SeaGateEdit edit = asked.From(PlayerOf(sender), SenderIdentity.IsAdmin(sender));
            if (zdo == null || !Allowed(zdo, edit))
                return;
            if (zdo.IsOwner() || !zdo.HasOwner())
                Apply(zdo, edit);
            else
                ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), zdo.m_uid, RpcOf(edit.Kind), edit.Write());
        }

        /// <summary>The sender's player on the server: a connected peer's, or this machine's own for a call it routed to
        /// itself (a host or single player); 0 for a peer that has already left.</summary>
        private static long PlayerOf(long sender)
        {
            bool known = sender == ZRoutedRpc.instance.m_id || ZNet.instance.GetPeer(sender) != null;
            return known ? SenderIdentity.PlayerId(sender) : 0L;
        }

        /// <summary>Anchor owner: an edit the server checked and forwarded under <paramref name="kind"/>'s RPC.</summary>
        internal static void OnForwarded(SeaGatePillar pillar, long sender, SeaGateEditKind kind, ZPackage pkg)
        {
            if (!SenderIdentity.IsFromServer(sender) || pillar == null || !pillar.IsValid || !pillar.View.IsOwner())
                return;
            if (SeaGateEdit.TryRead(pkg, out SeaGateEdit edit) && edit.Kind == kind && Allowed(pillar.Zdo, edit))
                Apply(pillar.Zdo, edit);
        }

        private static string RpcOf(SeaGateEditKind kind) =>
            kind == SeaGateEditKind.Name ? SeaGateFields.SetNameRpc : SeaGateFields.SetModeRpc;

        private static bool Allowed(ZDO zdo, SeaGateEdit edit)
        {
            if (!On || edit.PlayerId == 0L || !HoldsGateFields(zdo) || !GateAccess.MayCycle(zdo, edit.PlayerId, edit.IsAdmin))
                return false;
            return edit.Kind != SeaGateEditKind.Mode || GateAccess.MaySet(edit.Value, edit.IsAdmin);
        }

        /// <summary>A pillar ZDO that is its pair's anchor (the smaller id), judged from its own ZDO alone so it works
        /// where the partner is not loaded.</summary>
        private static bool HoldsGateFields(ZDO zdo)
        {
            long id = SeaGateFields.GetId(zdo);
            long partner = SeaGateFields.GetPartner(zdo);
            return SeaGateFields.IsPillar(zdo) && id != 0L && partner != 0L && id < partner;
        }

        private static void Apply(ZDO zdo, SeaGateEdit edit)
        {
            if (edit.Kind == SeaGateEditKind.Name)
                zdo.Set(SeaGateFields.NameKey, Clean(edit.Text));
            else
                GateAccess.SetModeAndOwner(zdo, (AccessMode)edit.Value, edit.PlayerId);
        }

        /// <summary>Trimmed, at most <see cref="NameLimit"/> characters, without control characters or the angle brackets
        /// of rich text, since the name is shown in other players' hover texts and messages.</summary>
        private static string Clean(string name)
        {
            StringBuilder clean = new StringBuilder();
            foreach (char c in name ?? "")
            {
                if (!char.IsControl(c) && c != '<' && c != '>')
                    clean.Append(c);
            }
            string trimmed = clean.ToString().Trim();
            return trimmed.Length > NameLimit ? trimmed.Substring(0, NameLimit).TrimEnd() : trimmed;
        }
    }
}
