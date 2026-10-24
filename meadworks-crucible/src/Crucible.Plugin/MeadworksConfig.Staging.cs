// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    public static partial class MeadworksConfig {
        sealed class OwnValueStagingMarker { }
        sealed class OwnValueStagingConstruction {
            int complete;
            internal bool Pending=>Volatile.Read(ref complete)==0;
            internal void Complete()=>Volatile.Write(ref complete,1);
        }
        static readonly ConditionalWeakTable<ConfigFile,OwnValueStagingMarker> ownValueStaging=new ConditionalWeakTable<ConfigFile,OwnValueStagingMarker>();
        static readonly OwnValueStagingMarker ownValueStagingMarker=new OwnValueStagingMarker();
        [ThreadStatic] static OwnValueStagingConstruction? ownValueStagingConstruction;

        /// <summary>True only for the exact private ConfigFile created for an own-value edit. The registry does not retain that file.</summary>
        public static bool IsOwnValueStaging(ConfigFile? file)=>file!=null&&ownValueStaging.TryGetValue(file,out _);

        /// <summary>Opaque constructor scope for observers that defer work until the exact returned instance can be marked.</summary>
        public static object? CurrentOwnValueStagingConstruction=>ownValueStagingConstruction;

        /// <summary>An observer must defer this scope while construction is unfinished, rather than classify its files by caller or name.</summary>
        public static bool IsOwnValueStagingConstructionPending(object? construction)=>construction is OwnValueStagingConstruction scope&&scope.Pending;

        static ConfigFile CreateOwnValueStaging(string path){
            var previous=ownValueStagingConstruction;
            var construction=new OwnValueStagingConstruction();
            ownValueStagingConstruction=construction;
            try{
                var file=new ConfigFile(path,false){SaveOnConfigSet=false};
                ownValueStaging.Add(file,ownValueStagingMarker);
                return file;
            }finally{
                // A constructor observer can run before the object returns, including on another thread.
                construction.Complete();
                ownValueStagingConstruction=previous;
            }
        }
    }
}
