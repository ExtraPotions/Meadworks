// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using ExtraPotions.Almanac.Index;
using ExtraPotions.Almanac.Runtime.Discovery;
using ExtraPotions.Almanac.Runtime.Editing;
using ExtraPotions.Almanac.Writing;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Almanac.Runtime.Utilities
{
    /// <summary>Bound native file actions and read-only unbound inspection; no arbitrary config text writes.</summary>
    public sealed class ConfigFileActions
    {
        readonly ConfigDiscovery discovery;
        readonly Func<ConfigEntryBase,NativeWriteFacts> resolve;
        readonly Func<int> backupCount;
        readonly string backups;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        readonly Dictionary<string,string> stamps=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly HashSet<string> changed=new HashSet<string>(StringComparer.Ordinal);
        float nextPoll;
        public string? Warning { get; private set; }
        public ConfigFileActions(ConfigDiscovery discovery,Func<ConfigEntryBase,NativeWriteFacts> resolve,string configDirectory,Func<int>? backupCount=null)
        {
            this.discovery=discovery??throw new ArgumentNullException(nameof(discovery));this.resolve=resolve??throw new ArgumentNullException(nameof(resolve));
            this.backupCount=backupCount??(()=>10);backups=Path.Combine(Path.GetFullPath(configDirectory),"Meadworks","almanac","backups");
        }
        public void Tick(bool visible,float now)
        {
            Check();if(!visible||!MeadworksHeadless.AllowsUi||now<nextPoll)return;nextPoll=now+2;
            foreach(var file in discovery.Index.Files.Take(4096))
            {
                try{string key=Key(file.Path),stamp=Stamp(file.Path);if(stamps.TryGetValue(key,out var before)&&stamp!=before)changed.Add(key);else if(!stamps.ContainsKey(key))stamps[key]=stamp;}
                catch{changed.Add(Key(file.Path));}
            }
        }
        public bool ChangedOnDisk(string path){Check();Indexed(path);return changed.Contains(Key(path));}
        /// <summary>Acknowledge an external change without reloading. Also call after Almanac's own entry writes.</summary>
        public void KeepMine(string path){Check();Indexed(path);stamps[Key(path)]=Stamp(path);changed.Remove(Key(path));}
        public bool CanSaveOrReload(string path)
        {
            Check();var file=discovery.LiveFile(path);if(file==null)return false;
            bool synced=file.Any(pair=>{var facts=resolve(pair.Value);return !facts.Known||facts.EntrySynced||facts.FileHasSyncedEntries;});
            return EntryWritePolicy.MaySaveFile(Remote,synced);
        }
        public void Save(string path){Check();var file=Writable(path);file.Save();KeepMine(path);}
        public void Reload(string path){Check();var file=Writable(path);file.Reload();discovery.Refresh(path);KeepMine(path);}
        public IReadOnlyList<EntryKey> Unused(string path){Check();Native(path);return discovery.Orphans(path).Keys.Take(4096).ToArray();}
        /// <summary>Authority only. Preserve the actual BepInEx serializer and publish atomically through C09.</summary>
        public string? RemoveUnused(string path)
        {
            Check();if(Remote)throw new InvalidOperationException("Unused-key removal is unavailable on a remote client.");
            var file=Writable(path);var orphanProperty=typeof(ConfigFile).GetProperty("OrphanedEntries",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            var orphans=orphanProperty?.GetValue(file) as IDictionary<ConfigDefinition,string>??throw new NotSupportedException("BepInEx orphaned entries are unavailable.");
            if(orphans.Count==0)return null;if(orphans.Count>4096)throw new InvalidOperationException("Too many unused keys for one operation.");
            var before=orphans.ToArray();string? backup=File.Exists(file.ConfigFilePath)?Backup(file.ConfigFilePath):null;
            try
            {
                if(!ReferenceEquals(discovery.LiveFile(path),file)||Remote)throw new InvalidOperationException("The native file or authority changed.");
                orphans.Clear();MeadworksConversion.Target(file).WriteAtomic(new Dictionary<string,object>());
            }
            catch{orphans.Clear();foreach(var pair in before)orphans.Add(pair);throw;}
            discovery.Refresh(path);KeepMine(path);Warning=null;
            try{Prune(file.ConfigFilePath);}catch(Exception error){Warning="Unused keys were removed; backup retention failed: "+error.GetBaseException().Message;}
            return backup;
        }
        public IReadOnlyList<KeyValuePair<string,string>> InspectUnbound(string path)
        {
            Check();if(Indexed(path).Kind!=FileKind.UnboundConfig)throw new InvalidOperationException("This action inspects unbound config files only.");
            return MeadworksConversion.ReadSourceConfig(path,Path.GetFileNameWithoutExtension(path)).Take(4096)
                .Select(pair=>new KeyValuePair<string,string>(pair.Key,Sensitive(pair.Key)?"[redacted]":pair.Value)).ToArray();
        }
        static bool Sensitive(string key)
        {
            var name=new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return new[]{"password","passwd","token","secret","apikey","credential","privatekey"}.Any(name.Contains);
        }
        ConfigFile Writable(string path){var file=Native(path);if(!CanSaveOrReload(path))throw new InvalidOperationException("Saving or reloading this file could overwrite server-provided values.");return file;}
        ConfigFile Native(string path){Indexed(path);return discovery.LiveFile(path)??throw new InvalidOperationException("No current bound native config file.");}
        IndexedFile Indexed(string path)=>discovery.Index.Find(path)??throw new ArgumentException("The file is not in the current config index.",nameof(path));
        bool Remote=>CruciblePlugin.IsReady&&CruciblePlugin.Session.Role==SessionRole.RemoteClient
            ||ZNet.instance!=null&&!ZNet.instance.IsServer()&&ZNet.instance.GetServerRPC()?.IsConnected()==true;
        static string Key(string path)=>Path.DirectorySeparatorChar=='\\'?Path.GetFullPath(path).ToUpperInvariant():Path.GetFullPath(path);
        static string Stamp(string path){var file=new FileInfo(path);return file.Exists?file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)+":"+file.Length.ToString(CultureInfo.InvariantCulture):"missing";}
        string Folder(string path)
        {using(var hash=SHA256.Create())return Path.Combine(backups,string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(Key(path))).Select(value=>value.ToString("x2",CultureInfo.InvariantCulture))));}
        string Backup(string path)
        {
            string folder=Folder(path);Directory.CreateDirectory(folder);var utc=DateTime.UtcNow;
            var old=BackupNaming.ToPrune(Path.GetFileName(path),Directory.GetFiles(folder).Select(Path.GetFileName),0);
            if(old.Count>100)throw new IOException("Backup retention must succeed before creating more backups.");
            string? newest=old.LastOrDefault();int dot=Path.GetFileName(path).LastIndexOf('.');int start=(dot>0?dot:Path.GetFileName(path).Length)+1;
            if(newest!=null&&newest.Length>=start+15&&DateTime.TryParseExact(newest.Substring(start,15),BackupNaming.StampFormat,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var last)&&last>=utc)utc=last.AddSeconds(1);
            for(int i=0;i<100;i++){string target=Path.Combine(folder,BackupNaming.Name(path,utc.AddSeconds(i)));if(File.Exists(target))continue;File.Copy(path,target,false);return target;}
            throw new IOException("The backup namespace is full.");
        }
        void Prune(string path)
        {
            string folder=Folder(path);foreach(string name in BackupNaming.ToPrune(Path.GetFileName(path),Directory.GetFiles(folder).Select(Path.GetFileName),Math.Max(1,Math.Min(100,backupCount()))))
                File.Delete(Path.Combine(folder,name));
        }
        void Check(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Config file actions must run on their owning thread.");}
    }
}
