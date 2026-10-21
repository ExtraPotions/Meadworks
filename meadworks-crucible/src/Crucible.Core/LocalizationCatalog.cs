using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    // Validated immutable input for Jötunn, also usable in dependency-free tests.
    public sealed class LocalizationCatalog {
        readonly string product;
        public IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> Tables { get; }
        public LocalizationCatalog(string product,IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> tables) {
            if(product==null||!Regex.IsMatch(product,@"^[a-z][a-z0-9_]*$"))throw new ArgumentException("Invalid product token namespace.");
            if(tables==null||!tables.ContainsKey("English"))throw new ArgumentException("English fallback is required.");
            this.product=product;var copy=new Dictionary<string,IReadOnlyDictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
            foreach(var language in tables){var translations=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var pair in language.Value){Token(pair.Key);translations.Add(pair.Key,pair.Value??throw new ArgumentException("Null translation."));}copy.Add(language.Key,new ReadOnlyDictionary<string,string>(translations));}
            Tables=new ReadOnlyDictionary<string,IReadOnlyDictionary<string,string>>(copy);
        }
        public string Token(string key){if(key==null||!Regex.IsMatch(key,@"^[a-z][a-z0-9_]*$"))throw new ArgumentException("Invalid translation key.");return "meadworks_"+product+"_"+key;}
        public string Text(string key,string language){if(Tables.TryGetValue(language,out var selected)&&selected.TryGetValue(key,out var translated))return translated;return Tables["English"].TryGetValue(key,out var fallback)?fallback:key;}
    }
}
