// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx.Bootstrap;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// This peer's MeadWorks build fingerprint: SHA256 of every loaded Crucible assembly and every com.meadworks.* plugin
    /// assembly, read once from disk. Published as the versioned capability <see cref="Capability"/> so the hosted
    /// exchange (batch 4A) and other tools compare builds through <see cref="BuildComparison"/>. Diagnostics only: it
    /// never affects admission, and a hash proves nothing about code a modified client actually runs.
    /// </summary>
    internal static class MeadworksBuildHashes {
        internal const string Capability=BuildComparison.Capability;
        internal static readonly System.Version CapabilityVersion=new System.Version(1,0);
        static IReadOnlyList<BuildEntry>? cached;

        internal static void Register(ProductRegistration crucible)=>
            crucible.AddCapability<Func<IReadOnlyList<BuildEntry>>>(Capability,CapabilityVersion,Local);

        internal static IReadOnlyList<BuildEntry> Local(){
            if(cached!=null)return cached;
            var result=new List<BuildEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);
            var assemblies=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("Crucible",StringComparison.Ordinal))
                .Concat(Chainloader.PluginInfos.Values.Where(p=>p.Metadata.GUID.StartsWith("com.meadworks.",StringComparison.Ordinal)&&p.Instance!=null).Select(p=>p.Instance.GetType().Assembly));
            foreach(var assembly in assemblies){
                var name=assembly.GetName().Name;
                if(!seen.Add(name))continue;
                try{
                    if(assembly.IsDynamic||string.IsNullOrEmpty(assembly.Location)||!File.Exists(assembly.Location))continue;
                    using var sha=SHA256.Create();using var stream=File.OpenRead(assembly.Location);
                    result.Add(new BuildEntry(name,assembly.GetName().Version?.ToString()??"",BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")));
                }catch(Exception e){Plugin.Log?.LogWarning("Build hash of "+name+" unavailable: "+e.GetType().Name);}
            }
            return cached=result.OrderBy(e=>e.Assembly,StringComparer.Ordinal).ToList();
        }

        /// <summary>One line per assembly for the read-only diagnostics command (first 12 hex characters).</summary>
        internal static IEnumerable<string> Describe()=>Local().Select(e=>"build: "+e.Assembly+" "+e.Version+" "+e.Sha256.Substring(0,12));
    }
}
