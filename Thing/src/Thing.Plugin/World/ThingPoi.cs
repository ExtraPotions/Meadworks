// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original C8 native-data provider using Crucible Operations and the live Mischief gate.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.Moderation;
using ExtraPotions.Thing.WorldAdmin;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingPoi
    {
        static IDisposable? operation;
        sealed class Codec : IOperationCodec<bool, IReadOnlyList<Tuple<string, float, float, float, bool>>>
        {
            public byte[] EncodeRequest(bool request) => request ? new byte[] { 1 } : throw new ArgumentException("Invalid POI request.");
            public bool DecodeRequest(byte[] bytes) => bytes != null && bytes.Length == 1 && bytes[0] == 1 ? true : throw new FormatException("Invalid POI request.");
            public byte[] EncodeReply(IReadOnlyList<Tuple<string, float, float, float, bool>> reply) => PoiData.Encode(reply);
            public IReadOnlyList<Tuple<string, float, float, float, bool>> DecodeReply(byte[] bytes) => PoiData.Decode(bytes);
        }
        internal static void Initialize(ProductRegistration registration)
        {
            operation = CruciblePlugin.Services.Operations.Register(registration,
                new OperationDescriptor(PoiData.OperationId, PoiData.Version), new Codec(), Apply);
            registration.AddCapability<Func<CancellationToken, Task<IReadOnlyList<Tuple<string, float, float, float, bool>>>>>(
                PoiData.CapabilityId, PoiData.Version, Read);
        }
        public static async Task<IReadOnlyList<Tuple<string, float, float, float, bool>>> Read(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (operation == null || !Plugin.Ready || !ThingModules.ProductActive(out _)
                || !ActionCatalog.Visible(ThingAction.PoiService, ThingModules.StepAside.AdminToolLoaded)
                || !MeadworksMischief.Allows(Plugin.Guid, MischiefCategory.Information, out _))
                throw new InvalidOperationException("PoiUnavailable");
            var result = await CruciblePlugin.RequestClient.InvokeAsync<bool, IReadOnlyList<Tuple<string, float, float, float, bool>>>(
                PoiData.OperationId, true, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (result.State != OperationState.Succeeded) throw new InvalidOperationException(result.Code);
            return result.Reply;
        }
        static OperationExecution<IReadOnlyList<Tuple<string, float, float, float, bool>>> Apply(AuthorityContext context, bool request)
        {
            var net = ZNet.instance;
            if (!request || !Plugin.Ready || !net || !net.IsServer() || !ThingServer.Active || Plugin.Settings == null
                || !ThingModules.ProductActive(out _) || !ActionCatalog.Visible(ThingAction.PoiService, ThingModules.StepAside.AdminToolLoaded)
                || !context.Session.AuthorityReady
                || context.Session.Generation != CruciblePlugin.Session.Generation)
                return OperationExecution<IReadOnlyList<Tuple<string, float, float, float, bool>>>.Rejected("PoiUnavailable");
            var peer = !context.IsLocal && long.TryParse(context.SenderId, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var uid)
                ? net.GetPeers().FirstOrDefault(p => p != null && p.IsReady() && p.m_uid == uid && p.m_rpc != null && p.m_rpc.IsConnected()) : null;
            var allowed = context.IsLocal
                ? MeadworksMischief.Allows(Plugin.Guid, MischiefCategory.Information, out _)
                : peer != null && MeadworksMischief.DecideForPeer(peer.m_rpc, MischiefCategory.Information).Allowed;
            var actor = new Actor(context.IsLocal ? "host" : peer?.m_socket?.GetHostName() ?? "peer:" + context.SenderId,
                context.IsLocal ? "console" : peer?.m_playerName ?? "");
            if (!allowed)
            {
                ThingServer.Audit(actor, ThingAction.PoiService, default, AuditOutcome.Refused, "Information category denied");
                return OperationExecution<IReadOnlyList<Tuple<string, float, float, float, bool>>>.Rejected("MischiefDenied");
            }
            var zone = ZoneSystem.instance;
            if (!zone || !zone.LocationsGenerated || zone.m_locationInstances == null)
                return OperationExecution<IReadOnlyList<Tuple<string, float, float, float, bool>>>.Rejected("PoiWorldNotReady");
            try
            {
                var rows = PoiData.Select(zone.m_locationInstances.Values.Select(location => Tuple.Create(
                    location.m_location?.m_prefabName ?? "", location.m_position.x, location.m_position.y, location.m_position.z, location.m_placed)));
                ThingServer.Audit(actor, ThingAction.PoiService, default, AuditOutcome.Done, "native location data requested");
                return OperationExecution<IReadOnlyList<Tuple<string, float, float, float, bool>>>.Success(rows);
            }
            catch (InvalidOperationException)
            { return OperationExecution<IReadOnlyList<Tuple<string, float, float, float, bool>>>.Rejected("PoiLimit"); }
        }
        internal static void Shutdown() { operation?.Dispose(); operation = null; }
    }
}
