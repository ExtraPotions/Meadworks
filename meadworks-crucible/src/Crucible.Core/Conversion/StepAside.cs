using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>A mod a Meadworks product replaces, by BepInEx plugin GUID and display name.</summary>
    public sealed class SourceMod {
        public string Guid { get; }
        public string Name { get; }
        public SourceMod(string guid,string name){
            if(string.IsNullOrWhiteSpace(guid)||guid.Length>256)throw new ArgumentException("Source mod needs a plugin GUID.",nameof(guid));
            if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Source mod needs a name.",nameof(name));
            Guid=guid;Name=name;
        }
    }

    /// <summary>One line of the compatibility list: a whole product (empty <see cref="ModuleId"/>) or one module stays off because <see cref="Source"/> is installed.</summary>
    public sealed class StepAsideReport {
        public string ProductId { get; }
        public string ProductName { get; }
        public string ModuleId { get; }
        public SourceMod Source { get; }
        public bool WholeProduct=>ModuleId.Length==0;
        /// <summary>The module id as words ("auto_doors" reads "auto doors").</summary>
        public string ModuleName=>ModuleId.Replace('_',' ');
        public StepAsideReport(string productId,string productName,string moduleId,SourceMod source){ProductId=productId??"";ProductName=productName??"";ModuleId=moduleId??"";Source=source??throw new ArgumentNullException(nameof(source));}
    }

    public sealed class StepAsideDecision {
        public string ProductId { get; }
        /// <summary>True when a source mod is installed: the product stays off and says why.</summary>
        public bool Yield { get; }
        public IReadOnlyList<SourceMod> Present { get; }
        /// <summary>English notice (C03 localizes); empty when not yielding.</summary>
        public string Notice { get; }
        /// <summary>Identifies this notice for the once-only rule: product plus the installed source GUIDs.</summary>
        public string NoticeKey=>Yield?ProductId+"|"+string.Join(",",Present.Select(p=>p.Guid).OrderBy(g=>g,StringComparer.Ordinal)):"";
        internal StepAsideDecision(string product,IReadOnlyList<SourceMod> present,string notice){ProductId=product;Present=present;Yield=present.Count>0;Notice=notice;}
    }

    /// <summary>
    /// Roster decision 3: when a source mod is installed, its Meadworks product steps aside and says so, instead of
    /// refusing to load (BepInIncompatibility). Products declare their source mods once.
    /// </summary>
    public sealed class StepAsideRegistry {
        sealed class Entry {internal string Name="";internal IReadOnlyList<SourceMod> Sources=Array.Empty<SourceMod>();}
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        public void Declare(string productId,string productName,IEnumerable<SourceMod> sources){
            Validation.Id(productId);
            if(string.IsNullOrWhiteSpace(productName)||productName.Length>128)throw new ArgumentException("Expected a product name.",nameof(productName));
            var list=(sources??throw new ArgumentNullException(nameof(sources))).ToList();
            if(list.Any(s=>s==null))throw new ArgumentException("Null source mod.",nameof(sources));
            if(list.Select(s=>s.Guid).Distinct(StringComparer.Ordinal).Count()!=list.Count)throw new ArgumentException("Duplicate source mod.",nameof(sources));
            if(entries.ContainsKey(productId))throw new InvalidOperationException("Source mods are already declared for "+productId+".");
            entries.Add(productId,new Entry{Name=productName,Sources=list.AsReadOnly()});
        }
        public IReadOnlyList<SourceMod> Declared(string productId)=>entries.TryGetValue(productId??"",out var e)?e.Sources:Array.Empty<SourceMod>();
        readonly Dictionary<string,ModuleStepAside> modules=new Dictionary<string,ModuleStepAside>(StringComparer.Ordinal);
        /// <summary>Records that one module of a product yields; a later report for the same module replaces it.</summary>
        public void ReportModule(ModuleStepAside aside){
            if(aside==null)throw new ArgumentNullException(nameof(aside));
            modules[aside.ProductId+"#"+aside.ModuleId]=aside;
        }
        /// <summary>Clears a module report (its source was removed or switched off); false when none was recorded.</summary>
        public bool WithdrawModule(string productId,string moduleId)=>modules.Remove((productId??"")+"#"+(moduleId??""));
        /// <summary>The product's module reports, ordered by module id. Whole-product decisions do not read these.</summary>
        public IReadOnlyList<ModuleStepAside> Modules(string productId)=>modules.Values.Where(m=>m.ProductId==productId).OrderBy(m=>m.ModuleId,StringComparer.Ordinal).ToList().AsReadOnly();
        /// <summary>
        /// Every step-aside for a read-only compatibility list: whole products that yield to an installed source mod and
        /// reported modules, ordered by product name then module. <paramref name="productName"/> names a product id.
        /// </summary>
        public IReadOnlyList<StepAsideReport> Reports(IEnumerable<string> installedGuids,Func<string,string?>? productName=null){
            if(installedGuids==null)throw new ArgumentNullException(nameof(installedGuids));
            var installed=new HashSet<string>(installedGuids,StringComparer.Ordinal);
            string Name(string id){string? named=null;try{named=productName?.Invoke(id);}catch{ }
                return !string.IsNullOrWhiteSpace(named)?named!:entries.TryGetValue(id,out var e)?e.Name:id;}
            var list=new List<StepAsideReport>();
            foreach(var pair in entries)foreach(var source in pair.Value.Sources.Where(s=>installed.Contains(s.Guid)))
                list.Add(new StepAsideReport(pair.Key,Name(pair.Key),"",source));
            foreach(var module in modules.Values)list.Add(new StepAsideReport(module.ProductId,Name(module.ProductId),module.ModuleId,module.Source));
            return list.OrderBy(r=>r.ProductName,StringComparer.OrdinalIgnoreCase).ThenBy(r=>r.ModuleId,StringComparer.Ordinal)
                .ThenBy(r=>r.Source.Name,StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }
        public StepAsideDecision Decide(string productId,IEnumerable<string> installedGuids){
            if(installedGuids==null)throw new ArgumentNullException(nameof(installedGuids));
            if(!entries.TryGetValue(productId??"",out var entry))return new StepAsideDecision(productId??"",Array.Empty<SourceMod>(),"");
            var installed=new HashSet<string>(installedGuids,StringComparer.Ordinal);
            var present=entry.Sources.Where(s=>installed.Contains(s.Guid)).ToList().AsReadOnly();
            return new StepAsideDecision(productId!,present,present.Count==0?"":Notice(entry.Name,present));
        }
        static string Notice(string product,IReadOnlyList<SourceMod> present){
            var names=present.Select(p=>p.Name).ToList();
            var list=names.Count==1?names[0]:string.Join(", ",names.Take(names.Count-1))+" and "+names[names.Count-1];
            return names.Count==1
                ?product+" steps aside because "+list+" is installed. Remove "+list+" to use "+product+"; Meadworks can import its settings."
                :product+" steps aside because "+list+" are installed. Remove them to use "+product+"; Meadworks can import their settings.";
        }
    }

    /// <summary>Shows each step-aside notice once per product and installed source set (or per product, module and source); persisted by the plugin.</summary>
    public sealed class StepAsideNotices {
        readonly HashSet<string> shown=new HashSet<string>(StringComparer.Ordinal);
        public bool ShouldShow(StepAsideDecision decision){
            if(decision==null)throw new ArgumentNullException(nameof(decision));
            return decision.Yield&&shown.Add(decision.NoticeKey);
        }
        /// <summary>True the first time this product, module and source are reported; shares the persisted set.</summary>
        public bool ShouldShow(ModuleStepAside aside){
            if(aside==null)throw new ArgumentNullException(nameof(aside));
            return shown.Add(aside.NoticeKey);
        }
        public string Format()=>string.Join(";",shown.OrderBy(s=>s,StringComparer.Ordinal));
        public void Load(string? text){foreach(var key in (text??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))if(key.Length<=1024)shown.Add(key.Trim());}
    }
}
