using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// C09 per-module step-aside: one module of a product stays off because one source mod covers it, while the rest of
    /// the product runs. The product decides when a module yields (installed GUID, a source setting, a source data file)
    /// and reports it; Crucible shows the notice once with the product's own C05 text.
    /// </summary>
    public sealed class ModuleStepAside {
        static readonly Regex ModuleShape=new Regex("^[a-z][a-z0-9_]{0,63}$",RegexOptions.CultureInvariant);
        public string ProductId { get; }
        /// <summary>Lowercase module id (letters, digits, underscores), for example <c>sleep</c> or <c>auto_doors</c>.</summary>
        public string ModuleId { get; }
        public SourceMod Source { get; }
        /// <summary>The product's C05 key for this notice; <c>{0}</c> is the source mod's name. Default <c>step_aside_&lt;module&gt;</c>.</summary>
        public string TextKey { get; }
        /// <summary>Identifies this notice for the once-only rule: product, module and source GUID.</summary>
        public string NoticeKey=>ProductId+"#"+ModuleId+"|"+Source.Guid;
        public ModuleStepAside(string productId,string moduleId,SourceMod source,string? textKey=null){
            Validation.Id(productId);
            if(moduleId==null||!ModuleShape.IsMatch(moduleId))throw new ArgumentException("Expected a lowercase module id.",nameof(moduleId));
            Source=source??throw new ArgumentNullException(nameof(source));
            var key=textKey??ModuleStepAsideText.DefaultKey(moduleId);
            if(!TranslationJson.IsValidKey(key))throw new ArgumentException("Expected a translation key.",nameof(textKey));
            ProductId=productId;ModuleId=moduleId;TextKey=key;
        }
    }

    /// <summary>Notice text for <see cref="ModuleStepAside"/>: the product's translation, else a Crucible English sentence.</summary>
    public static class ModuleStepAsideText {
        public static string DefaultKey(string moduleId)=>"step_aside_"+moduleId;

        public static string English(string productName,string moduleName,string sourceName)=>
            productName+" "+moduleName+" steps aside because "+sourceName+" is installed. Remove "+sourceName+" to use it; Meadworks can import its settings.";

        /// <summary>
        /// The product's text for <see cref="ModuleStepAside.TextKey"/> in <paramref name="language"/> (the product's English
        /// when that language lacks it), formatted with the source name as <c>{0}</c>. When the product has no such key or
        /// its text does not format, the Crucible English sentence with <paramref name="productName"/> (or the product id).
        /// </summary>
        public static string Notice(ModuleStepAside aside,string? productName,ProductTranslations? translations,string? language){
            if(aside==null)throw new ArgumentNullException(nameof(aside));
            if(translations!=null&&translations.Keys.Contains(aside.TextKey)){
                try{return translations.Format(aside.TextKey,language,aside.Source.Name);}
                catch(FormatException){ }
            }
            return English(string.IsNullOrWhiteSpace(productName)?aside.ProductId:productName!,aside.ModuleId.Replace('_',' '),aside.Source.Name);
        }
    }
}
