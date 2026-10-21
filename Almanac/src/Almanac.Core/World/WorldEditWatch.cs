// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Almanac.World
{
    /// <summary>Presentation state for a submitted world edit, never an authority or persistence service.</summary>
    public sealed class WorldEditWatch
    {
        readonly long world, generation;
        readonly string? requested;
        public bool Pending { get; private set; }
        public bool Stale { get; private set; }
        public WorldEditWatch(long world,long generation,string? requested,bool applied)
        {this.world=world;this.generation=generation;this.requested=requested;Pending=!applied;}
        /// <summary>Only a readable native world snapshot in the submitted session can confirm the edit.</summary>
        public void Observe(long currentWorld,long currentGeneration,string? nativeWorldText,bool readable)
        {
            if(!Pending)return;
            if(currentWorld!=world||currentGeneration!=generation){Pending=false;Stale=true;return;}
            if(readable&&string.Equals(requested,nativeWorldText,StringComparison.Ordinal))Pending=false;
        }
    }
}
