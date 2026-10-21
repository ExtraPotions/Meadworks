using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>The seven languages every Meadworks product ships, in canonical order and spelling.</summary>
    public static class MeadworksLanguages {
        public const string English="English";
        public static IReadOnlyList<string> Supported { get; }=Array.AsReadOnly(new[]{"English","Korean","Spanish","Chinese","Russian","French","German"});
        /// <summary>Canonical spelling of a supported language (case-insensitive, Valheim Chinese variants accepted).</summary>
        public static bool TryCanonical(string? requested,out string language){
            language=English;if(string.IsNullOrWhiteSpace(requested))return false;
            var trimmed=requested!.Trim();
            if(trimmed.StartsWith("Chinese",StringComparison.OrdinalIgnoreCase)){language="Chinese";return true;}
            foreach(var supported in Supported)if(string.Equals(supported,trimmed,StringComparison.OrdinalIgnoreCase)){language=supported;return true;}
            return false;
        }
        /// <summary>Canonical language, or English when unsupported.</summary>
        public static string Normalize(string? requested)=>TryCanonical(requested,out var language)?language:English;
    }

    /// <summary>
    /// One product's translations: built-in tables, validated hot-reload overrides and English fallback.
    /// Consolidates Wildroot's TranslationStore with Crucible's <see cref="LocalizationCatalog"/> token namespace.
    /// </summary>
    public sealed class ProductTranslations {
        static readonly Regex ProductShape=new Regex("^[a-z][a-z0-9_]*$",RegexOptions.CultureInvariant);
        readonly object gate=new object();
        readonly IReadOnlyDictionary<string,string> english;
        readonly Dictionary<string,IReadOnlyDictionary<string,string>> builtins=new Dictionary<string,IReadOnlyDictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
        volatile Dictionary<string,IReadOnlyDictionary<string,string>> tables;
        long revision;
        public string Product { get; }
        public long Revision=>Interlocked.Read(ref revision);

        /// <summary>Built-in tables as translation JSON text keyed by language.</summary>
        public ProductTranslations(string product,IReadOnlyDictionary<string,string> jsonByLanguage)
            :this(product,ParseAll(jsonByLanguage)){}
        ProductTranslations(string product,Dictionary<string,IReadOnlyDictionary<string,string>> parsed){
            if(product==null||!ProductShape.IsMatch(product))throw new ArgumentException("Invalid product translation namespace.");
            Product=product;
            if(!parsed.TryGetValue(MeadworksLanguages.English,out var baseline))throw new ArgumentException("English translations are required.");
            english=baseline;
            foreach(var pair in parsed){Validate(pair.Value,"built-in "+pair.Key);builtins[pair.Key]=pair.Value;}
            tables=new Dictionary<string,IReadOnlyDictionary<string,string>>(builtins,StringComparer.OrdinalIgnoreCase);
        }
        /// <summary>Built-in tables as in-memory dictionaries (Wayfarer C# style).</summary>
        public static ProductTranslations FromTables(string product,IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> tables){
            if(tables==null)throw new ArgumentNullException(nameof(tables));
            var parsed=new Dictionary<string,IReadOnlyDictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
            foreach(var language in tables){
                var copy=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var pair in language.Value){
                    if(!TranslationJson.IsValidKey(pair.Key))throw new ArgumentException("Invalid translation key: "+pair.Key);
                    try{TranslationJson.Placeholders(pair.Value??throw new ArgumentException("Null translation: "+pair.Key));}
                    catch(FormatException exception){throw new ArgumentException(exception.Message,exception);}
                    copy.Add(pair.Key,pair.Value);
                }
                parsed.Add(Canonical(language.Key),new ReadOnlyDictionary<string,string>(copy));
            }
            return new ProductTranslations(product,parsed);
        }
        static Dictionary<string,IReadOnlyDictionary<string,string>> ParseAll(IReadOnlyDictionary<string,string> jsonByLanguage){
            if(jsonByLanguage==null)throw new ArgumentNullException(nameof(jsonByLanguage));
            var parsed=new Dictionary<string,IReadOnlyDictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
            foreach(var pair in jsonByLanguage){
                try{parsed.Add(Canonical(pair.Key),TranslationJson.Parse(pair.Value));}
                catch(FormatException exception){throw new ArgumentException("Built-in "+pair.Key+" translations: "+exception.Message,exception);}
            }
            return parsed;
        }
        static string Canonical(string language)=>MeadworksLanguages.TryCanonical(language,out var canonical)?canonical:throw new ArgumentException("Unsupported language: "+language);

        public IReadOnlyCollection<string> Languages=>tables.Keys.ToArray();
        public IReadOnlyCollection<string> Keys=>english.Keys.ToArray();
        public string Token(string key){if(!TranslationJson.IsValidKey(key))throw new ArgumentException("Invalid translation key.");return "meadworks_"+Product+"_"+key;}
        public string Text(string key,string? language){
            var snapshot=tables;
            if(snapshot.TryGetValue(MeadworksLanguages.Normalize(language),out var table)&&table.TryGetValue(key,out var value))return value;
            if(snapshot.TryGetValue(MeadworksLanguages.English,out var activeEnglish)&&activeEnglish.TryGetValue(key,out var currentFallback))return currentFallback;
            return english.TryGetValue(key,out var fallback)?fallback:key;
        }
        public string Format(string key,string? language,params object[] arguments)=>string.Format(CultureInfo.InvariantCulture,Text(key,language),arguments);
        /// <summary>Table actually in effect for a language (built-in merged with override), without English fill-in.</summary>
        public IReadOnlyDictionary<string,string> Table(string language)=>tables.TryGetValue(MeadworksLanguages.Normalize(language),out var table)?table:new Dictionary<string,string>();

        /// <summary>Merges an override file over the built-in table. Invalid input leaves the current table untouched.</summary>
        public bool TryOverride(string language,string json,out string error){
            try{
                var canonical=Canonical(language);var parsed=TranslationJson.Parse(json);Validate(parsed,"override "+canonical);
                lock(gate){
                    var merged=new Dictionary<string,string>(StringComparer.Ordinal);
                    if(builtins.TryGetValue(canonical,out var existing))foreach(var pair in existing)merged[pair.Key]=pair.Value;
                    foreach(var pair in parsed)merged[pair.Key]=pair.Value;
                    Publish(canonical,new ReadOnlyDictionary<string,string>(merged));
                }
                error="";return true;
            }catch(Exception exception)when(exception is ArgumentException||exception is FormatException){error=exception.Message;return false;}
        }
        /// <summary>Drops any override and restores the built-in table (or no table).</summary>
        public void ClearOverride(string language){
            var canonical=Canonical(language);
            lock(gate)Publish(canonical,builtins.TryGetValue(canonical,out var builtin)?builtin:null);
        }
        void Publish(string language,IReadOnlyDictionary<string,string>? table){
            var next=new Dictionary<string,IReadOnlyDictionary<string,string>>(tables,StringComparer.OrdinalIgnoreCase);
            if(table==null)next.Remove(language);else next[language]=table;
            tables=next;Interlocked.Increment(ref revision);
        }
        /// <summary>English reference file translators copy into the overrides folder.</summary>
        public string ExportEnglish()=>TranslationJson.Write(english);

        void Validate(IReadOnlyDictionary<string,string> table,string source){
            foreach(var pair in table){
                if(!english.TryGetValue(pair.Key,out var baseline))throw new ArgumentException("Unknown translation key in "+source+": "+pair.Key);
                if(!TranslationJson.Placeholders(pair.Value).SetEquals(TranslationJson.Placeholders(baseline)))throw new ArgumentException("Translation placeholders differ in "+source+": "+pair.Key);
            }
        }
    }

    public enum OverrideOutcome { Unchanged, Applied, Rejected, Cleared, TooLarge }
    public readonly struct OverrideObservation {
        public OverrideOutcome Outcome { get; }
        public string Error { get; }
        public OverrideObservation(OverrideOutcome outcome,string error=""){Outcome=outcome;Error=error??"";}
    }

    /// <summary>
    /// Hot-reload decisions for one product's overrides folder. The plugin polls file existence and length;
    /// content is read only when within bounds, and unchanged content (by SHA-256) is skipped, so each problem is reported once.
    /// </summary>
    public sealed class TranslationOverrideWatcher {
        const string TooLargeMarker="too-large";
        readonly ProductTranslations translations;
        readonly Dictionary<string,string> seen=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public TranslationOverrideWatcher(ProductTranslations translations){this.translations=translations??throw new ArgumentNullException(nameof(translations));}
        public OverrideObservation Observe(string language,bool exists,long length,Func<byte[]> read){
            if(!MeadworksLanguages.TryCanonical(language,out var canonical))throw new ArgumentException("Unsupported language: "+language);
            if(!exists){if(!seen.Remove(canonical))return new OverrideObservation(OverrideOutcome.Unchanged);translations.ClearOverride(canonical);return new OverrideObservation(OverrideOutcome.Cleared);}
            if(length>TranslationJson.MaximumBytes){
                if(seen.TryGetValue(canonical,out var previous)&&previous==TooLargeMarker)return new OverrideObservation(OverrideOutcome.Unchanged);
                seen[canonical]=TooLargeMarker;return new OverrideObservation(OverrideOutcome.TooLarge,"Translation override exceeds 256 KiB.");
            }
            var bytes=read()??Array.Empty<byte>();
            string hash;using(var digest=SHA256.Create())hash=Convert.ToBase64String(digest.ComputeHash(bytes));
            if(seen.TryGetValue(canonical,out var old)&&old==hash)return new OverrideObservation(OverrideOutcome.Unchanged);
            seen[canonical]=hash;
            var text=new UTF8Encoding(false,false).GetString(bytes).TrimStart('﻿');
            return translations.TryOverride(canonical,text,out var error)?new OverrideObservation(OverrideOutcome.Applied):new OverrideObservation(OverrideOutcome.Rejected,error);
        }
    }

    /// <summary>
    /// The override poll's file stamps (exists, length, last write time) per language. A file is read and hashed again
    /// only when its stamp changed, so the two-second poll does not read every override.
    /// shortcut: an edit that keeps both the length and the write time is missed; compare contents if an editor ever does that.
    /// </summary>
    public sealed class OverrideStamps {
        readonly (bool Known,bool Exists,long Length,long Ticks)[] last;
        public OverrideStamps(int count){if(count<0)throw new ArgumentOutOfRangeException(nameof(count));last=new (bool,bool,long,long)[count];}
        /// <summary>True (and remembered) when the file differs from the last stamp, or on the first look.</summary>
        public bool Changed(int index,bool exists,long length,long writeTicks){
            var now=(true,exists,exists?length:0,exists?writeTicks:0);
            if(last[index]==now)return false;
            last[index]=now;return true;
        }
        /// <summary>The next look reports a change (after a failed read, so it is retried).</summary>
        public void Forget(int index)=>last[index]=default;
    }

    /// <summary>Per-product registration of translation sets.</summary>
    public sealed class LocalizationRegistry {
        readonly object gate=new object();
        readonly Dictionary<string,ProductTranslations> products=new Dictionary<string,ProductTranslations>(StringComparer.Ordinal);
        public ProductTranslations Register(ProductTranslations translations){
            if(translations==null)throw new ArgumentNullException(nameof(translations));
            lock(gate){if(products.ContainsKey(translations.Product))throw new InvalidOperationException("Translations already registered: "+translations.Product);products.Add(translations.Product,translations);}
            return translations;
        }
        public bool TryGet(string product,out ProductTranslations translations){lock(gate)return products.TryGetValue(product,out translations!);}
        public bool Remove(string product){lock(gate)return products.Remove(product);}
        public IReadOnlyList<string> Products{get{lock(gate)return products.Keys.OrderBy(p=>p,StringComparer.Ordinal).ToArray();}}
    }
}
