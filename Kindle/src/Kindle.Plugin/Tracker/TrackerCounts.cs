// Changed by expDARE, 2026: retained source adaptation notices.
// Kindle - Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from OpenKeep 3.0.0, ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// OpenKeep/OpenKeep/src/Tracker/TrackerCounts.cs. Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE: Crucible async 2.0 total, main-thread polling, current-session cancellation and carried fallback.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Contracts;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime
{
    internal static class TrackerCounts
    {
        private sealed class Count {internal Task<int>? Pending;internal int? Available;}
        private static readonly Dictionary<string,Count> counts=new Dictionary<string,Count>(StringComparer.Ordinal);
        private static IAsyncResourceProvider? provider;
        private static CancellationTokenSource? cancellation;
        private static Player? actor;
        private static string session="";
        private static long world,generation;
        internal static bool NearbyAvailable=>CruciblePlugin.IsReady && CruciblePlugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),out _);
        internal static bool Shows(Piece.Requirement requirement,TrackedRecipe entry)=>requirement!=null && requirement.m_resItem
            && requirement.m_upgraderResource==entry.Upgrader && requirement.GetAmount(entry.Quality)>0;
        internal static int Need(Piece.Requirement requirement,TrackedRecipe entry)=>(int)Math.Min(int.MaxValue,(long)requirement.GetAmount(entry.Quality)*entry.Amount);
        internal static int Have(Piece.Requirement requirement)
        {
            var player=Player.m_localPlayer;if(!player)return 0;
            string id=requirement.m_resItem.name;
            return provider!=null && counts.TryGetValue(id,out var count) && count.Available.HasValue ? count.Available.Value
                : player.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name);
        }
        internal static void Shutdown()
        {
            cancellation?.Cancel();cancellation?.Dispose();cancellation=null;counts.Clear();provider=null;actor=null;session="";world=generation=0;
        }
        internal static void Tick(IReadOnlyList<TrackedRecipe> tracked)
        {
            var current=CruciblePlugin.Session;
            IAsyncResourceProvider? next=null;
            if(KindleTracker.Active && current.AuthorityReady && current.WorldId!=0 && !string.IsNullOrEmpty(current.SessionId)
                && KindleTracker.Settings.Get(KindleSettings.Tracker.CountNearbyChests))
                CruciblePlugin.Services.Capabilities.TryResolve<IAsyncResourceProvider>("meadworks.resources.async",new System.Version(2,0),out next);
            if(!ReferenceEquals(provider,next) || !ReferenceEquals(actor,Player.m_localPlayer) || session!=current.SessionId || world!=current.WorldId || generation!=current.Generation)
            {
                Shutdown();provider=next;actor=Player.m_localPlayer;session=current.SessionId;world=current.WorldId;generation=current.Generation;
                if(provider!=null)cancellation=new CancellationTokenSource();
            }
            if(provider==null || !actor)return;
            var materials=tracked.Where(entry=>entry.Recipe).SelectMany(entry=>entry.Recipe!.m_resources.Where(requirement=>Shows(requirement,entry)))
                .Select(requirement=>requirement.m_resItem.name).Distinct(StringComparer.Ordinal).ToArray();
            foreach(var unused in counts.Keys.Except(materials).ToArray())counts.Remove(unused);
            foreach(string id in materials)
            {
                if(!counts.TryGetValue(id,out var count))counts.Add(id,count=new Count());
                if(count.Pending!=null)
                {
                    if(!count.Pending.IsCompleted)continue;
                    count.Available=count.Pending.Status==TaskStatus.RanToCompletion && count.Pending.Result>=0 ? count.Pending.Result : (int?)null;
                    count.Pending=null;
                }
                try
                {
                    var context=new AsyncResourceContext(actor.GetPlayerID().ToString(CultureInfo.InvariantCulture),world,session,Guid.NewGuid().ToString("N"));
                    count.Pending=provider.GetAvailableAsync(context,id,cancellation!.Token);
                }
                catch(Exception){count.Available=null;count.Pending=null;}
            }
        }
    }
}
