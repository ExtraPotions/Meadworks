// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtraPotions.Kindle.Config;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Kindle.Runtime
{
    /// <summary>Product mapping over Crucible's existing preview/backup/atomic converter. Source files are read only.</summary>
    public sealed class KindleReferenceImport : IDisposable
    {
        readonly ConfigFile config;
        readonly ConfigEntry<string> marker;
        readonly ConfigEntry<bool> apply;
        readonly ConfigEntry<bool> preview;
        readonly ConfigEntry<string> choices;
        readonly Dictionary<string, KindleSetting> mappings = new Dictionary<string, KindleSetting>(StringComparer.Ordinal);
        readonly ConfigConverter converter;
        readonly MeadworksLog log;
        readonly KindleConfig settings;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        IDisposable registration;
        bool disposed;
        Player lastPlayer;
        bool offered;
        public string ReportPath => Path.Combine(Paths.ConfigPath, "Meadworks", "kindle", "import-report.txt");

        public KindleReferenceImport(ConfigFile config, KindleConfig settings, MeadworksLog log)
        {
            this.config = config; this.log = log; this.settings=settings;
            marker = MeadworksConfig.BindHidden(config, "Import", "Imported Sources", "", "Managed by Kindle.");
            preview = MeadworksConfig.Bind(config, "Import", "Preview OpenKeep", false, "Write a preview report without changing any source or target settings. Resets after running.");
            apply = MeadworksConfig.Bind(config, "Import", "Apply OpenKeep", false, "Apply the previewed reference settings with a target backup. Source files remain intact. Resets after running.");
            choices = MeadworksConfig.Bind(config, "Import", "Binding Overrides", "", "Replacement shortcuts as Section.Key=value, separated by semicolons, for collisions shown by Preview.");
            foreach (var setting in KindleSettings.All)
            {
                string section = setting.Section == "Recipe List" ? "12. Recipe List" : setting.Section == "Batch Crafting" ? "10. Batch Crafting"
                    : setting.Section == "Recipe Tracker" ? "13. Recipe Tracker" : setting.Section == "Salvage" ? "3. Salvage"
                    : setting.Section == "Cart Workbench" ? "6. Carts" : "";
                if (section.Length == 0 || setting == KindleSettings.RecipeList.FavouriteRecipeKey ||
                    setting == KindleSettings.RecipeList.IgnoreAccents || setting == KindleSettings.RecipeList.SortButton ||
                    setting == KindleSettings.RecipeList.GamepadSortButton) continue;
                mappings.Add("OpenKeep." + section + "." + setting.Key, setting);
            }
            mappings.Add("OpenKeep.2. Stow.Favourite Item Key", KindleSettings.RecipeList.FavouriteRecipeKey);
            var rules = mappings.Select(pair => new ConversionRule(pair.Key, pair.Value.Id,
                IsShortcut(pair.Value) ? "KeyboardShortcut" : pair.Value.ValueType.Name,
                string.Join(",", pair.Value.Choices), actionBinding: IsShortcut(pair.Value)));
            converter = new ConfigConverter(new ConversionMapping(1, "OpenKeep", 0, rules));
            registration=MeadworksConversion.Register(new ConversionRegistration("kindle.openkeep",Plugin.Guid,"milkyteam.openkeep","OpenKeep",
                ()=>!disposed&&(File.Exists(SourcePath)||Directory.GetFiles(Paths.ConfigPath,"OpenKeep.Salvage*.yml").Length!=0),
                ()=>Preview(),(document,resolutions,backup)=>
                {
                    if(!backup)throw new InvalidOperationException("Kindle import requires a backup.");
                    var sourceChoices=new Dictionary<string,string>(StringComparer.Ordinal);var bindings=new Dictionary<string,string>(StringComparer.Ordinal);
                    foreach(var resolution in resolutions)
                        if(resolution.Key.StartsWith("binding:",StringComparison.Ordinal))bindings.Add(resolution.Key.Substring(8),resolution.Value);
                        else if(KindleSettings.All.Any(s=>s.Id==resolution.Key&&IsShortcut(s))&&!mappings.ContainsKey(resolution.Value))bindings.Add(resolution.Key,resolution.Value);
                        else sourceChoices.Add(resolution.Key,resolution.Value);
                    return Apply(document as KindleImportPreview??throw new ArgumentException("Wrong Kindle import preview."),sourceChoices,bindings);
                }));
        }
        public string SourcePath=>Path.Combine(Paths.ConfigPath,"milkyteam.openkeep.cfg");
        void Check(){if(disposed||Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Kindle imports require their live owning thread.");}
        public void Dispose(){if(disposed)return;disposed=true;registration?.Dispose();registration=null;}

        static bool IsShortcut(KindleSetting setting) => setting == KindleSettings.RecipeList.SearchKey ||
            setting == KindleSettings.RecipeList.FavouriteRecipeKey || setting == KindleSettings.Salvage.SalvageKey;

        /// <summary>After all plugins load: GUIDs are authoritative; legacy CraftingSearch names identify its older GUID.</summary>
        public static bool DeclareAndYield()
        {
            if (!ExtraPotions.Meadworks.Crucible.Plugin.Services.StepAside.Declared(Plugin.Guid).Any())
            {
                var sources = new List<SourceMod>();
                if (Chainloader.PluginInfos.TryGetValue("milkyteam.openkeep", out var openKeep) && openKeep.Metadata.Version >= new System.Version(2, 0))
                    sources.Add(new SourceMod(openKeep.Metadata.GUID, "OpenKeep"));
                foreach (var info in Chainloader.PluginInfos.Values)
                    if (info.Metadata.GUID == "com.ab5olutezer0.valheim.craftingsearch" ||
                        string.Equals(info.Metadata.Name, "Crafting Search", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(info.Instance?.GetType().Assembly.GetName().Name, "CraftingSearch", StringComparison.OrdinalIgnoreCase))
                        if (!sources.Any(s => s.Guid == info.Metadata.GUID)) sources.Add(new SourceMod(info.Metadata.GUID, "Crafting Search"));
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, sources);
            }
            return MeadworksStepAside.ShouldYield(Plugin.Guid);
        }

        public void Tick(bool active)
        {
            if (!offered)
            {
                offered = true;
                if (marker.Value.Length == 0 && File.Exists(Path.Combine(Paths.ConfigPath, "milkyteam.openkeep.cfg"))) Run(false);
            }
            if (preview.Value || apply.Value) Run(apply.Value);
            if (!active) return;
            var player = Player.m_localPlayer;
            if (player == null || player == lastPlayer) return;
            lastPlayer = player;
            foreach (var (from, to) in new[] { ("favouriteRecipes", "favoriteRecipes"), ("favouriteRecipesOnly", "favoriteRecipesOnly"), ("trackedRecipes", "trackedRecipes") })
                if (!player.m_customData.ContainsKey("Kindle." + to) && player.m_customData.TryGetValue("OpenKeep." + from, out var value))
                    player.m_customData["Kindle." + to] = value;
            if (!player.TryGetUniqueKeyValue("kindle_sort_desc", out _) && !player.m_customData.ContainsKey("Kindle.importedCraftingSearch") &&
                player.TryGetUniqueKeyValue("craftingsearch_desc", out _))
            {
                player.AddUniqueKeyValue("kindle_sort_desc", "1");
                player.m_customData["Kindle.importedCraftingSearch"] = "1";
            }
        }

        public KindleImportPreview Preview()
        {
            Check();var sources=ReadSources(out string sourceHash);
            if(sources.Count==0)throw new InvalidOperationException("No OpenKeep settings or salvage files were found to import.");
            var raw=sources.TryGetValue(SourcePath,out var cfg)?CfgText.Read(cfg,"OpenKeep"):Array.Empty<KeyValuePair<string,string>>();
            var values=new List<KeyValuePair<string,string>>();var errors=new List<string>();var skipped=new List<string>();var normalizations=new List<string>();
            foreach(var pair in raw)
            {
                if(!mappings.TryGetValue(pair.Key,out var setting)){skipped.Add(pair.Key+": no Kindle mapping");continue;}
                if(setting==KindleSettings.RecipeList.FavouriteRecipeKey&&pair.Value=="F"){skipped.Add(pair.Key+": default reference F binding retained as Kindle's configured binding");continue;}
                var authority=MeadworksAuthority.Decide(Plugin.Guid,KindleConfig.Map(setting.Scope));
                if(settings.IsLocked(setting)||authority.Source==ConfigValueSource.Server||authority.Source==ConfigValueSource.Locked)
                {skipped.Add(pair.Key+": target is locked or server-provided");continue;}
                try
                {
                    var type=setting.ValueType;object parsed;
                    if(IsShortcut(setting))parsed=KeyboardShortcut.Deserialize(pair.Value);
                    else if(type.IsEnum)parsed=Enum.TryParse(type,pair.Value,true,out var chosen)?chosen:setting.DefaultValue;
                    else parsed=TomlTypeConverter.ConvertToValue(pair.Value,type);
                    var normalized=IsShortcut(setting)?parsed:setting.NormalizeBoxed(parsed);
                    string text=IsShortcut(setting)?((KeyboardShortcut)normalized).Serialize():Convert.ToString(normalized,CultureInfo.InvariantCulture)??"";
                    if(text!=pair.Value)normalizations.Add(pair.Key+": "+pair.Value+" -> "+text+" (normalized)");
                    values.Add(new KeyValuePair<string,string>(pair.Key,text));
                }
                catch(Exception error){errors.Add(pair.Key+": "+error.GetBaseException().Message);}
            }
            var plan=converter.Preview(values);errors.AddRange(plan.Errors);
            var merged=ReadTargetYaml();
            foreach(var source in sources.Where(p=>p.Key!=SourcePath))merged[Destination(source.Key)]=ImportedText(source.Key,source.Value);
            if(merged.Count!=0)
            {
                var model=new Salvage.SalvageModel();model.LoadAll(KindleAlmanac.Ordered(merged));errors.AddRange(model.Errors);
            }
            var blocking=errors.ToArray();
            try{ValidateFinalBindings(plan,ReadOverrides());}catch(Exception error){errors.Add(error.GetBaseException().Message);}
            var rows=plan.Values.Select(v=>new KindleImportValue(v.SourceId,v.TargetId,
                raw.First(p=>p.Key==v.SourceId).Value,Convert.ToString(v.Value,CultureInfo.InvariantCulture)??"",
                config[config.Keys.Single(k=>k.Section+"."+k.Key==v.TargetId)].GetSerializedValue()));
            return new KindleImportPreview(this,plan,rows,errors,blocking,skipped,normalizations,sources,sourceHash,TargetHash(),
                ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation);
        }
        Dictionary<string,string> ReadOverrides()
        {
            var overrides=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var part in choices.Value.Split(';')){int cut=part.IndexOf('=');if(cut>0)overrides[part.Substring(0,cut).Trim()]=part.Substring(cut+1).Trim();}
            return overrides;
        }
        Dictionary<string,string> ReadSources(out string hash)
        {
            var paths=Directory.GetFiles(Paths.ConfigPath,"OpenKeep.Salvage*.yml").OrderBy(p=>p,StringComparer.Ordinal).ToList();
            if(File.Exists(SourcePath))paths.Insert(0,SourcePath);
            var source=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string path in paths)
            {
                string before=KindleAlmanac.FileHash(path);string text=KindleAlmanac.ReadText(path);
                if(KindleAlmanac.FileHash(path)!=before)throw new InvalidOperationException("Source changed while preparing the import.");
                source.Add(path,text);hashes.Add(path,before);
            }
            hash=KindleAlmanac.SetHash(hashes);return source;
        }
        Dictionary<string,string> ReadTargetYaml()=>Directory.GetFiles(Paths.ConfigPath,"Kindle.Salvage*.yml")
            .ToDictionary(p=>p,p=>KindleAlmanac.ReadText(p),StringComparer.OrdinalIgnoreCase);
        static string Destination(string source)=>Path.Combine(Paths.ConfigPath,Path.GetFileName(source).Replace("OpenKeep.","Kindle."));
        static string ImportedText(string path,string text)=>"# Imported from "+Path.GetFileName(path)+"; source preserved.\n"+text;
        string TargetHash()
        {
            var image=config.Keys.OrderBy(k=>k.Section,StringComparer.Ordinal).ThenBy(k=>k.Key,StringComparer.Ordinal)
                .Select(k=>new KeyValuePair<string,string>(k.Section.Length+":"+k.Section+k.Key.Length+":"+k.Key,
                    TomlTypeConverter.ConvertToString(config[k].BoxedValue,config[k].SettingType))).ToList();
            image.Add(new KeyValuePair<string,string>("config-bytes",KindleAlmanac.FileHash(config.ConfigFilePath)));
            image.Add(new KeyValuePair<string,string>("yaml-set",KindleAlmanac.SetHash(ReadTargetYaml())));
            return KindleAlmanac.SetHash(image);
        }
        void Admission(KindleImportPreview document,bool target=true)
        {
            Check();if(document==null||!ReferenceEquals(document.Owner,this))throw new ArgumentException("Preview belongs to another Kindle importer.");
            KindleAlmanac.Authority();
            if(ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation!=document.Generation)throw new InvalidOperationException("Session changed; preview again.");
            ReadSources(out string current);
            if(current!=document.SourceSha256)throw new InvalidOperationException("Source files changed; preview again.");
            if(target&&TargetHash()!=document.TargetSha256)throw new InvalidOperationException("Target configuration or salvage files changed; preview again.");
            foreach(var value in document.Preview.Values)
            {
                var setting=KindleSettings.All.Single(s=>s.Id==value.TargetId);
                var authority=MeadworksAuthority.Decide(Plugin.Guid,KindleConfig.Map(setting.Scope));
                if(settings.IsLocked(setting)||!authority.Allowed||authority.RequiresServerAmendment)
                    throw new InvalidOperationException("An import target is now locked: "+value.TargetId);
            }
        }
        public KindleImportResult Apply(KindleImportPreview document,IReadOnlyDictionary<string,string> sourceChoices,
            IReadOnlyDictionary<string,string> bindingOverrides=null)
        {
            Admission(document);if(document.PrepareErrors.Count!=0)throw new ArgumentException(string.Join("; ",document.PrepareErrors));
            bindingOverrides??=new Dictionary<string,string>();ValidateFinalBindings(document.Preview,bindingOverrides);
            var target=new ImportTarget(this,document);var result=converter.Apply(document.Preview,target,sourceChoices,bindingOverrides);
            var messages=new List<string>();var backups=target.Backups.ToList();
            try{CopyYaml(document,messages,backups);Admission(document,false);}
            catch(Exception error)
            {throw new InvalidOperationException("Configuration committed "+result.Written+" settings before YAML/source verification stopped: "+error.GetBaseException().Message+". Backups: "+string.Join(", ",backups),error);}
            // Retain the qualified importer's ordinary YAML watcher/reload timing; its synchronized
            // carrier is not an additional config conversion target.
            marker.Value="OpenKeep";
            return new KindleImportResult(result,document.SourceSha256,TargetHash(),backups,messages);
        }
        sealed class ImportTarget:IValidatedConversionTarget
        {
            readonly KindleReferenceImport owner;readonly KindleImportPreview preview;readonly IConversionTarget target;
            internal readonly List<string> Backups=new List<string>();
            bool backupRequested,written;
            internal ImportTarget(KindleReferenceImport owner,KindleImportPreview preview)
            {this.owner=owner;this.preview=preview;target=MeadworksConversion.Target(owner.config);}
            public int SchemaVersion=>target.SchemaVersion;
            ConfigEntryBase[] Entries(IReadOnlyDictionary<string,object> values)
            {
                var entries=new List<ConfigEntryBase>();
                foreach(var value in values)
                {
                    var setting=KindleSettings.All.SingleOrDefault(s=>s.Id==value.Key)
                        ??throw new ArgumentException("Unknown Kindle import target: "+value.Key);
                    var entry=owner.settingsEntry(setting);
                    if(!MeadworksConfig.TryGetMetadata(entry,out var metadata)||metadata.ProductId!=Plugin.Guid
                        ||!metadata.Decision.Allowed||metadata.Decision.RequiresServerAmendment||owner.settings.IsLocked(setting))
                        throw new InvalidOperationException("An import target is now locked: "+value.Key);
                    var nativeValue=entry.SettingType.IsInstanceOfType(value.Value)?value.Value
                        :TomlTypeConverter.ConvertToValue(Convert.ToString(value.Value,CultureInfo.InvariantCulture),entry.SettingType);
                    if(entry.Description.AcceptableValues!=null&&!entry.Description.AcceptableValues.IsValid(nativeValue))
                        throw new ArgumentException("Invalid Kindle import target value: "+value.Key);
                    entries.Add(entry);
                }
                return entries.Distinct().ToArray();
            }
            public void Validate(IReadOnlyDictionary<string,object> values)
            {
                if(written)throw new InvalidOperationException("This Kindle configuration import has already been applied.");
                owner.Admission(preview);((IValidatedConversionTarget)target).Validate(values);Entries(values);
            }
            public void Backup()
            {
                owner.Admission(preview);string path=owner.config.ConfigFilePath;
                if(!File.Exists(path))throw new InvalidOperationException("Save Kindle configuration before importing; a target backup is required.");
                // Keep the verified backup inside the admitted publication callback, before its original atomic write.
                backupRequested=true;
            }
            void VerifiedBackup()
            {
                string path=owner.config.ConfigFilePath;
                string folder=Path.GetDirectoryName(path),pattern=Path.GetFileName(path)+".import-*.bak";
                var before=Directory.GetFiles(folder,pattern);string hash=KindleAlmanac.FileHash(path);target.Backup();
                var created=Directory.GetFiles(folder,pattern).Except(before,StringComparer.OrdinalIgnoreCase).ToArray();
                if(created.Length!=1||KindleAlmanac.FileHash(created[0])!=hash)throw new IOException("The exact target backup could not be verified.");
                Backups.Add(created[0]);
            }
            public void WriteAtomic(IReadOnlyDictionary<string,object> values)
            {
                Validate(values);
                if(!backupRequested)throw new InvalidOperationException("Kindle import requires a backup.");
                var entries=Entries(values);if(entries.Length==0)return;
                try
                {
                    var result=MeadworksConfig.AmendBatch(Plugin.Guid,entries,()=>
                    {
                        owner.Admission(preview);Entries(values);VerifiedBackup();target.WriteAtomic(values);written=true;
                    },"Kindle import");
                    if(!result.Applied||!written)throw new InvalidOperationException("Current authority or synchronization refused the Kindle import before writing.");
                }
                catch(Exception error)when(written)
                {
                    throw new IOException("Kindle settings were saved locally, but synchronization did not complete. Salvage files and the import marker were not updated; review synchronization before continuing.",error);
                }
            }
        }
        void Run(bool write)
        {
            var report=new List<string>{"Kindle OpenKeep import: "+(write?"apply":"preview")};
            try
            {
                var document=Preview();report.AddRange(document.Normalizations);
                report.AddRange(document.Values.Select(v=>v.SourceId+" -> "+v.TargetId+" = "+v.Value));
                report.AddRange(document.Errors);report.AddRange(document.Skipped);
                report.AddRange(document.BindingConflicts.Select(c=>"Shortcut collision: "+c.TargetId+" (set Binding Overrides)"));
                report.Add("Source SHA256: "+document.SourceSha256+"; target SHA256: "+document.TargetSha256);
                if(write)
                {
                    var result=Apply(document,new Dictionary<string,string>(),ReadOverrides());report.AddRange(result.Messages);
                    report.Add("Applied "+result.Written+" settings; target backup: "+result.BackedUp);
                }
                else report.Add("Preview only; no settings changed.");
            }
            catch(Exception error){report.Add("Import stopped: "+error.GetType().Name+": "+error.Message);}
            finally
            {
                preview.Value=apply.Value=false;Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllLines(ReportPath,report);log.Info(report[report.Count-1]+" Report: "+ReportPath);
            }
        }

        void ValidateFinalBindings(ConversionPreview plan, IReadOnlyDictionary<string, string> overrides)
        {
            var final = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var setting in KindleSettings.All.Where(IsShortcut))
                final[setting.Id] = settingsEntry(setting).GetSerializedValue();
            foreach (var imported in plan.Values)
                if (final.ContainsKey(imported.TargetId)) final[imported.TargetId] = Convert.ToString(imported.Value, CultureInfo.InvariantCulture) ?? "";
            foreach (var replacement in overrides)
                if (final.ContainsKey(replacement.Key)) final[replacement.Key] = replacement.Value;
            var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var binding in final)
            {
                if (!HotkeyChord.TryParse(binding.Value, out var chord)) throw new ArgumentException("Unknown shortcut: " + binding.Key);
                if (!chord.IsNone) normalized[binding.Key] = string.Join(" + ", new[] { chord.Main }.Concat(chord.Modifiers.OrderBy(m => m, StringComparer.Ordinal)));
            }
            var collisions = normalized.GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).ToArray();
            if (collisions.Length != 0) throw new ArgumentException("Final shortcut collision: " + string.Join("; ", collisions.Select(group => group.Key + " -> " + string.Join(", ", group.Select(pair => pair.Key)))));
        }

        ConfigEntryBase settingsEntry(KindleSetting setting) => config[new ConfigDefinition(setting.Section, setting.Key)];

        void CopyYaml(KindleImportPreview document,List<string> report,List<string> backups)
        {
            // Capture the post-config state: YAML admission must also refuse changes made since that commit.
            string expectedTarget=TargetHash();
            foreach(var source in document.Sources.Where(p=>p.Key!=SourcePath))
            {
                Admission(document,false);if(TargetHash()!=expectedTarget)throw new InvalidOperationException("Target changed during YAML import.");
                string destination=Destination(source.Key),text=ImportedText(source.Key,source.Value);
                string original=File.Exists(destination)?KindleAlmanac.ReadText(destination):null;
                string temporary=KindleAlmanac.Temporary(destination,text),backup=null;
                try
                {
                    Admission(document,false);if(TargetHash()!=expectedTarget)throw new InvalidOperationException("Target changed at YAML publication.");
                    if(original!=null)
                    {
                        backup=KindleAlmanac.Backup(destination);File.Replace(temporary,destination,backup);backups.Add(backup);
                    }
                    else File.Move(temporary,destination);
                    if(KindleAlmanac.ReadText(destination)!=text)throw new IOException("Imported YAML verification failed; backup: "+backup);
                    expectedTarget=TargetHash();report.Add("Copied "+Path.GetFileName(destination)+(backup!=null?" (existing target backed up)":" (new target; no previous bytes)"));
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
        }
    }
}
