// Pinned OpenKeep YAML model/file-set behavior adapted to Kindle's existing Jotunn config carrier.
// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Extensions;
using Jotunn.Managers;
using Newtonsoft.Json;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Kindle.Runtime.Salvage
{
    internal static class SalvageYaml
    {
        static ConfigEntry<string> carrier;
        static long synchronizedGeneration = -1, observedGeneration = -1;
        static KindleSyncReceipt syncReceipt = new KindleSyncReceipt();
        static string applied;
        static float nextRead;
        static bool initialized;
        static int ownerThread;
        static readonly Dictionary<string,IDisposable> documents=new Dictionary<string,IDisposable>(StringComparer.OrdinalIgnoreCase);
        internal static bool Ready { get; private set; }
        internal static IReadOnlyDictionary<string,string> Files { get; private set; } = new Dictionary<string,string>();
        internal static void Initialize(ConfigFile config, ProductRegistration registration)
        {
            bool save = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                carrier = config.BindConfig("Salvage", "Managed YAML Content", "", "Managed salvage rules; edit Kindle.Salvage*.yml instead.", synced:true,
                    configAttributes:new ConfigurationManagerAttributes { Browsable=false, ReadOnly=true });
            }
            finally { config.SaveOnConfigSet = save; }
            config.Save();
            SynchronizationManager.OnConfigurationSynchronized += Synchronized;
            registration.AddCapability<Func<IReadOnlyDictionary<string,string>>>("meadworks.crafting.salvage_yaml", new System.Version(1,0), () => Files);
            ownerThread=Thread.CurrentThread.ManagedThreadId;initialized = true;
            Reload();
        }
        static void Synchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (CruciblePlugin.IsReady && args.UpdatedPluginGUIDs != null && args.UpdatedPluginGUIDs.Contains(Plugin.Guid))
            { syncReceipt.Observe(); synchronizedGeneration = syncReceipt.CurrentGeneration; nextRead = 0; }
        }
        internal static void Tick()
        {
            if (!initialized || !CruciblePlugin.IsReady) return;
            long generation = CruciblePlugin.Session.Generation;
            if (observedGeneration != generation)
            {
                observedGeneration = generation; applied = null; Ready = false;
                SalvageRules.Apply(null); nextRead = 0;
            }
            if (Time.unscaledTime < nextRead) return;
            nextRead = Time.unscaledTime + 5f;
            Reload();
        }
        internal static void Reload()
        {
            if (!initialized || !CruciblePlugin.IsReady) return;
            try
            {
                bool remote = CruciblePlugin.Session.Role == SessionRole.RemoteClient;
                var decision = MeadworksAuthority.Decide(Plugin.Guid, ConfigScope.ServerAuthoritative);
                if (remote && decision.Source == ConfigValueSource.Server)
                {
                    synchronizedGeneration = syncReceipt.CurrentGeneration;
                    if (synchronizedGeneration != CruciblePlugin.Session.Generation) { Ready=false; return; }
                    Apply(MeadworksConfig.Effective(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, ""));
                    return;
                }
                if (decision.Source == ConfigValueSource.Locked) { Ready=false; return; }
                string main = Path.Combine(Paths.ConfigPath, "Kindle.Salvage.yml");
                var paths = Directory.GetFiles(Paths.ConfigPath, "Kindle.Salvage*.yml")
                    .OrderBy(p => Path.GetFileName(p).Equals("Kindle.Salvage.yml",StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(p => Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToArray();
                if (paths.Length == 0)
                {
                    using (var stream = typeof(SalvageYaml).Assembly.GetManifestResourceStream("Kindle.config.Kindle.Salvage.yml"))
                    {
                        if (stream == null) throw new InvalidOperationException("Embedded salvage rules missing.");
                        using (var reader = new StreamReader(stream))
                        using (var output = new StreamWriter(new FileStream(main,FileMode.CreateNew,FileAccess.Write,FileShare.Read)))
                            output.Write(reader.ReadToEnd());
                    }
                    paths = new[]{main};
                }
                var files = new Dictionary<string,string>();
                foreach (string path in paths) files.Add(Path.GetFileName(path), File.ReadAllText(path));
                RegisterDocuments(files.Keys);
                string text = JsonConvert.SerializeObject(files);
                if (!Apply(text)) return;
                if (!remote && carrier.Value != text)
                    MeadworksConfig.Amend(carrier, Plugin.Guid, ConfigScope.ServerAuthoritative, text);
            }
            catch (Exception error) { KindleSalvage.Warn("Salvage YAML reload refused: " + error.GetType().Name); }
        }
        static bool Apply(string text)
        {
            if (text == applied && Ready) return true;
            var files = JsonConvert.DeserializeObject<Dictionary<string,string>>(text ?? "");
            if (files == null || files.Count == 0) return false;
            foreach (var file in files)
                if (Path.GetFileName(file.Key) != file.Key || !file.Key.StartsWith("Kindle.Salvage",StringComparison.OrdinalIgnoreCase)
                    || !file.Key.EndsWith(".yml",StringComparison.OrdinalIgnoreCase) || file.Value == null)
                    throw new InvalidOperationException("Invalid salvage file-set shape.");
            var model = new SalvageModel();
            model.LoadAll(files);
            if (model.Errors.Count != 0) { KindleSalvage.Warn("Salvage YAML rejected; previous rules retained ("+model.Errors.Count+" errors)."); return false; }
            if (model.Warnings.Count != 0) KindleSalvage.Warn("Salvage YAML applied with "+model.Warnings.Count+" warnings.");
            Files = new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(files);
            SalvageRules.Apply(model); applied = text; Ready = true;
            KindleSalvage.Rebuild();
            RegisterDocuments(files.Keys);
            return true;
        }
        static void RegisterDocuments(IEnumerable<string> names)
        {
            var current=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);
            foreach(var name in documents.Keys.Where(name=>!current.Contains(name)).ToArray()){documents[name].Dispose();documents.Remove(name);}
            foreach(var name in current)
            {
                if(documents.ContainsKey(name))continue;
                string captured=name,path=Path.Combine(Paths.ConfigPath,name);
                documents.Add(name,MeadworksDataFiles.Register(new DataFileRegistration(Plugin.Guid,name,path,
                    ()=>ReadDocument(captured),text=>ValidateDocument(captured,text),(original,text)=>ApplyDocument(captured,original,text))));
            }
        }
        static void DocumentOwner()
        {
            if(!initialized||!CruciblePlugin.IsReady||Thread.CurrentThread.ManagedThreadId!=ownerThread)
                throw new InvalidOperationException("Salvage documents require their live owning thread.");
        }
        static string ReadDocument(string name)
        {
            DocumentOwner();
            // Clients see the effective server file-set, never an unrelated stale local file.
            if(CruciblePlugin.Session.Role==SessionRole.RemoteClient)
            {
                if(!Ready||observedGeneration!=CruciblePlugin.Session.Generation||!Files.TryGetValue(name,out var text))throw new InvalidOperationException("Current-session salvage content is not ready.");
                return text;
            }
            return KindleAlmanac.ReadText(Path.Combine(Paths.ConfigPath,name));
        }
        static Dictionary<string,string> DiskDocuments()=>Directory.GetFiles(Paths.ConfigPath,"Kindle.Salvage*.yml")
            .ToDictionary(path=>Path.GetFileName(path),path=>KindleAlmanac.ReadText(path),StringComparer.OrdinalIgnoreCase);
        static IReadOnlyList<string> ValidateDocument(string name,string text)
        {
            DocumentOwner();
            if(text==null||System.Text.Encoding.UTF8.GetByteCount(text)>KindleAlmanac.MaxBytes)return new[]{"Salvage text exceeds 1 MiB."};
            var current=CruciblePlugin.Session.Role==SessionRole.RemoteClient
                ?Files.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase):DiskDocuments();
            if(!current.ContainsKey(name))return new[]{"The registered salvage file no longer exists."};
            current[name]=text;var model=new SalvageModel();model.LoadAll(KindleAlmanac.Ordered(current));
            if(CruciblePlugin.Session.Role==SessionRole.RemoteClient)model.Errors.Insert(0,"Salvage files are read-only on remote clients.");
            return model.Errors.AsReadOnly();
        }
        static bool ApplyDocument(string name,string original,string text)
        {
            DocumentOwner();KindleAlmanac.Authority();long generation=CruciblePlugin.Session.Generation;
            var current=DiskDocuments();
            if(!current.TryGetValue(name,out var before)||!string.Equals(before,original,StringComparison.Ordinal))
                throw new InvalidOperationException("Salvage content changed; reload the document first.");
            if(ValidateDocument(name,text).Count!=0)return false;
            string identity=KindleAlmanac.SetHash(current),path=Path.Combine(Paths.ConfigPath,name);
            string temporary=KindleAlmanac.Temporary(path,text),backup=KindleAlmanac.Backup(path);bool published=false;
            try
            {
                // Read/Validate is advisory. Recheck the whole merge and native authority at publication.
                DocumentOwner();KindleAlmanac.Authority();
                if(CruciblePlugin.Session.Generation!=generation||KindleAlmanac.SetHash(DiskDocuments())!=identity)
                    throw new InvalidOperationException("Salvage file-set or session changed during this edit.");
                File.Replace(temporary,path,backup);published=true;
                if(KindleAlmanac.ReadText(path)!=text)throw new IOException("Salvage publication did not retain the requested text.");
                Reload();
                if(!Ready||!Files.TryGetValue(name,out var retained)||retained!=text)
                    throw new InvalidOperationException("Salvage reload did not accept the requested file-set.");
                return true;
            }
            catch(Exception error)
            {
                if(published)
                {
                    try
                    {
                        DocumentOwner();KindleAlmanac.Authority();
                        if(CruciblePlugin.Session.Generation!=generation||KindleAlmanac.ReadText(path)!=text)
                            throw new InvalidOperationException("Current authority/content changed; rollback refused.");
                        // Preserve original bytes, including BOM/line endings, without consuming the backup.
                        string restore=path+".import-"+System.Guid.NewGuid().ToString("N")+".tmp";
                        try
                        {
                            File.Copy(backup,restore,false);
                            using(var output=new FileStream(restore,FileMode.Open,FileAccess.Write,FileShare.None))output.Flush(true);
                            File.Replace(restore,path,null);Reload();
                        }
                        finally{if(File.Exists(restore))File.Delete(restore);}
                    }
                    catch(Exception rollback){throw new IOException("Salvage edit failed; rollback failed: "+rollback.GetBaseException().Message+". Backup: "+backup,error);}
                }
                throw new IOException("Salvage edit refused; backup: "+(published?backup:"not written")+". "+error.GetBaseException().Message,error);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static void Shutdown()
        {
            foreach(var lease in documents.Values)lease.Dispose();documents.Clear();
            SynchronizationManager.OnConfigurationSynchronized -= Synchronized;
            initialized = Ready = false; applied = null; carrier = null;
            synchronizedGeneration = observedGeneration = -1;
            syncReceipt = new KindleSyncReceipt();
            Files = new Dictionary<string,string>(); SalvageRules.Apply(null);
        }
    }
}
