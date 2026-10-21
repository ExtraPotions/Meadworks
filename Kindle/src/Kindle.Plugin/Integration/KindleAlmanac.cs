// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product-owned AS03/AS05 documents over the existing C09 converter and salvage model.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Kindle.Runtime
{
    public sealed class KindleImportValue
    {
        internal KindleImportValue(string source,string target,string original,string value,string current)
        {SourceId=source;TargetId=target;SourceValue=original;Value=value;CurrentValue=current;}
        public string SourceId { get; }
        public string TargetId { get; }
        public string SourceValue { get; }
        public string Value { get; }
        public string CurrentValue { get; }
    }
    public sealed class KindleImportPreview
    {
        internal KindleImportPreview(KindleReferenceImport owner,ConversionPreview plan,IEnumerable<KindleImportValue> values,
            IEnumerable<string> errors,IEnumerable<string> prepareErrors,IEnumerable<string> skipped,IEnumerable<string> normalizations,
            IReadOnlyDictionary<string,string> sources,string sourceHash,string targetHash,long generation)
        {
            Owner=owner;Preview=plan;Values=Array.AsReadOnly(values.ToArray());Errors=Array.AsReadOnly(errors.ToArray());
            PrepareErrors=Array.AsReadOnly(prepareErrors.ToArray());Skipped=Array.AsReadOnly(skipped.ToArray());Normalizations=Array.AsReadOnly(normalizations.ToArray());
            Sources=new ReadOnlyDictionary<string,string>(sources.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase));
            SourceSha256=sourceHash;TargetSha256=targetHash;Generation=generation;
        }
        internal KindleReferenceImport Owner { get; }
        internal IReadOnlyDictionary<string,string> Sources { get; }
        internal IReadOnlyList<string> PrepareErrors { get; }
        internal long Generation { get; }
        public ConversionPreview Preview { get; }
        public IReadOnlyList<KindleImportValue> Values { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Skipped { get; }
        public IReadOnlyList<string> Normalizations { get; }
        public IReadOnlyList<ConversionConflict> Conflicts=>Preview.Conflicts;
        public IReadOnlyList<ConversionConflict> BindingConflicts=>Preview.BindingConflicts;
        public string SourceSha256 { get; }
        public string TargetSha256 { get; }
        public override string ToString()
        {
            var lines=new List<string>{KindleText.Text("import_review")};
            lines.AddRange(Values.Select(v=>v.SourceId+" → "+v.TargetId+": "+v.CurrentValue+" → "+v.Value+" (source: "+v.SourceValue+")"));
            lines.AddRange(Normalizations.Select(v=>"Normalized: "+v));
            lines.AddRange(Skipped.Select(v=>"Skipped: "+v));
            lines.AddRange(Errors.Select(v=>"Invalid: "+v));
            lines.AddRange(Conflicts.Select(c=>"Choice required: "+c.TargetId+" = "+string.Join(" or ",c.Choices.Select(v=>v.SourceId))));
            lines.AddRange(BindingConflicts.Select(c=>"Shortcut collision: "+c.TargetId+"; replace binding:<target>=<shortcut>"));
            lines.AddRange(Sources.Where(p=>!p.Key.EndsWith(".cfg",StringComparison.OrdinalIgnoreCase)).Select(p=>"Salvage file: "+Path.GetFileName(p.Key)+" → "+Path.GetFileName(p.Key).Replace("OpenKeep.","Kindle.")));
            lines.Add("Overrides: binding:<Section.Key>=<shortcut>; conflicts: <Section.Key>=<source id>.");
            lines.Add("Source SHA256: "+SourceSha256);lines.Add("Target SHA256: "+TargetSha256);
            return string.Join("\n",lines);
        }
    }
    public sealed class KindleImportResult
    {
        internal KindleImportResult(ConversionResult result,string sourceHash,string targetHash,IEnumerable<string> backups,IEnumerable<string> messages)
        {Written=result.Written;BackedUp=result.BackedUp;SourceSha256=sourceHash;TargetSha256=targetHash;Backups=Array.AsReadOnly(backups.ToArray());Messages=Array.AsReadOnly(messages.ToArray());}
        public int Written { get; }
        public bool BackedUp { get; }
        public string SourceSha256 { get; }
        public string TargetSha256 { get; }
        public IReadOnlyList<string> Backups { get; }
        public IReadOnlyList<string> Messages { get; }
        public override string ToString()=>string.Join("\n",new[]{"Applied "+Written+" settings; config backup "+(BackedUp?"created":"no config changes")}
            .Concat(Messages).Concat(Backups.Select(p=>"Backup: "+p)).Concat(new[]{"Source SHA256: "+SourceSha256,"Target SHA256: "+TargetSha256}));
    }
    internal static class KindleAlmanac
    {
        internal const int MaxBytes=1024*1024;
        internal static string ReadText(string path)
        {
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length>MaxBytes)throw new InvalidDataException("Registered text exceeds 1 MiB.");
                using(var reader=new StreamReader(input,new UTF8Encoding(false,true),true))return reader.ReadToEnd();
            }
        }
        internal static string Hash(string text)
        {using(var sha=SHA256.Create())return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        internal static string FileHash(string path)
        {if(!File.Exists(path))return "missing";using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())return Hex(sha.ComputeHash(input));}
        static string Hex(byte[] bytes)=>string.Concat(bytes.Select(b=>b.ToString("X2",CultureInfo.InvariantCulture)));
        internal static string SetHash(IEnumerable<KeyValuePair<string,string>> files)
        {
            var text=new StringBuilder();
            foreach(var file in files.OrderBy(p=>p.Key,StringComparer.Ordinal))
                text.Append(file.Key.Length).Append(':').Append(file.Key).Append(file.Value.Length).Append(':').Append(file.Value);
            return Hash(text.ToString());
        }
        internal static IEnumerable<KeyValuePair<string,string>> Ordered(IEnumerable<KeyValuePair<string,string>> files)=>files
            .OrderBy(p=>Path.GetFileName(p.Key).Equals("Kindle.Salvage.yml",StringComparison.OrdinalIgnoreCase)?0:1)
            .ThenBy(p=>Path.GetFileName(p.Key),StringComparer.OrdinalIgnoreCase);
        internal static void Authority()
        {
            if(!ExtraPotions.Meadworks.Crucible.Plugin.IsReady||!Plugin.Ready)
                throw new InvalidOperationException("Kindle configuration owner is unavailable.");
            var decision=MeadworksAuthority.Decide(Plugin.Guid,ConfigScope.ServerAuthoritative);
            if(ExtraPotions.Meadworks.Crucible.Plugin.Session.Role==SessionRole.RemoteClient||!decision.Allowed||decision.RequiresServerAmendment)
                throw new InvalidOperationException("Apply this import on the authority machine; this client cannot replace server-controlled configuration.");
        }
        // Same sibling-temp/flush/replace native seam already used by Kindle's CopyYaml.
        internal static string Temporary(string path,string text)
        {
            if(Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new InvalidDataException("Registered text exceeds 1 MiB.");
            string temporary=path+".import-"+System.Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {using(var writer=new StreamWriter(output,new UTF8Encoding(false),1024,true)){writer.Write(text);writer.Flush();}output.Flush(true);}
                return temporary;
            }
            catch{if(File.Exists(temporary))File.Delete(temporary);throw;}
        }
        internal static string Backup(string path)=>path+".import-"+DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)+"-"+System.Guid.NewGuid().ToString("N")+".bak";
    }
}
