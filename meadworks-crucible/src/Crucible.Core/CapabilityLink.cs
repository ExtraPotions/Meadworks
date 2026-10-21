// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// One optional link to another product's capability, resolved again only when the registry changed
    /// (<see cref="CapabilityRegistry.Generation"/>), so a per-frame read costs an integer compare and never allocates.
    /// It never pins an answer: a provider that registers later (after the consumer's Awake) or leaves is seen on the next
    /// read. Replaces BepInEx plugin lookups between MeadWorks products (user decision 2026-10-12 22:41). Main thread only.
    /// </summary>
    public sealed class CapabilityLink<T> where T:class {
        readonly string id;
        readonly Version minimum;
        CapabilityRegistry? seen;
        int generation=-1;
        T? service;

        public CapabilityLink(string id,Version minimumVersion){
            Validation.Id(id);Validation.Version(minimumVersion);
            this.id=id;minimum=minimumVersion;
        }
        public string Id=>id;

        /// <summary>The provider now, or false when there is none (or more than one, or another shape or major version).</summary>
        public bool TryGet(CapabilityRegistry? registry,out T? provider){
            if(registry==null){provider=null;return false;}
            if(!ReferenceEquals(registry,seen)||registry.Generation!=generation){
                seen=registry;generation=registry.Generation;
                if(!registry.TryResolve<T>(id,minimum,out service))service=null;
            }
            provider=service;
            return provider!=null;
        }
    }

    /// <summary>
    /// Feature ownership (<c>meadworks.owns.&lt;feature&gt;</c>, a <c>Func&lt;bool&gt;</c>; ids in Crucible.Contracts'
    /// <c>FeatureOwnerContract</c>): who owns a feature and whether it is on, with the consumer's fallback when nobody does.
    /// </summary>
    public sealed class FeatureLink {
        readonly CapabilityLink<Func<bool>> link;
        public FeatureLink(string capabilityId)=>link=new CapabilityLink<Func<bool>>(capabilityId,new Version(1,0));
        public string Id=>link.Id;

        /// <summary>
        /// True when a product owns the feature, with <paramref name="on"/> its answer; false when no product does (or its
        /// check threw), and the caller's standalone fallback applies.
        /// </summary>
        public bool TryOwned(CapabilityRegistry? registry,out bool on){
            on=false;
            if(!link.TryGet(registry,out var owner))return false;
            try{on=owner!();return true;}catch{on=false;return false;}
        }

        /// <summary>A product owns the feature and it is on now.</summary>
        public bool On(CapabilityRegistry? registry)=>TryOwned(registry,out bool on)&&on;
    }
}
