using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class LogCommandResult {
        public IReadOnlyList<string> Lines { get; }
        /// <summary>The policy or filter changed; the adapter persists it.</summary>
        public bool Changed { get; }
        internal LogCommandResult(bool changed,params string[] lines){Changed=changed;Lines=Array.AsReadOnly(lines);}
        internal LogCommandResult(bool changed,IEnumerable<string> lines):this(changed,lines.ToArray()){}
    }

    /// <summary>
    /// The <c>meadworks log</c> console verb: <c>meadworks log &lt;level&gt; [product]</c>, <c>meadworks log default
    /// [product]</c> and <c>meadworks log filter [on|off|&lt;source&gt; &lt;level&gt;]</c>. Arguments exclude "log".
    /// </summary>
    public static class LogCommand {
        public const string Usage="Usage: meadworks log <quiet|normal|verbose|debug|default> [product] | meadworks log filter [on|off|<source> <all|info|warning|error|none|default>]";

        public static LogCommandResult Run(LogPolicy policy,ConsoleFilter filter,IReadOnlyList<string> args){
            if(policy==null)throw new ArgumentNullException(nameof(policy));
            if(filter==null)throw new ArgumentNullException(nameof(filter));
            if(args==null)throw new ArgumentNullException(nameof(args));
            if(args.Count==0)return Status(policy,filter);
            if(string.Equals(args[0],"filter",StringComparison.OrdinalIgnoreCase))return Filter(filter,args.Skip(1).ToArray());
            if(args.Count>2)return new LogCommandResult(false,Usage);
            bool reset=string.Equals(args[0].Trim(),"default",StringComparison.OrdinalIgnoreCase);
            LogVerbosity level=LogVerbosity.Quiet;
            if(!reset&&!LogLevels.TryParse(args[0],out level))return new LogCommandResult(false,"Unknown log level '"+args[0]+"'. Use quiet, normal, verbose or debug.");
            if(args.Count==1){policy.Level=level;return new LogCommandResult(true,"Meadworks log level is now "+policy.Level+".");}
            var product=args[1].Trim();
            try{
                if(reset){policy.ClearOverride(product);return new LogCommandResult(true,product+" follows the Meadworks log level ("+policy.Level+").");}
                policy.SetOverride(product,level);return new LogCommandResult(true,product+" log level is now "+level+".");
            }catch(ArgumentException){return new LogCommandResult(false,"Invalid product name '"+product+"'.");}
        }

        static LogCommandResult Status(LogPolicy policy,ConsoleFilter filter){
            var lines=new List<string>{"Meadworks log level: "+policy.Level};
            lines.AddRange(policy.Overrides.OrderBy(o=>o.Key,StringComparer.OrdinalIgnoreCase).Select(o=>"  "+o.Key+"="+o.Value));
            lines.Add("Console filter: "+(filter.Enabled?"on":"off"));
            lines.Add(Usage);
            return new LogCommandResult(false,lines);
        }

        static LogCommandResult Filter(ConsoleFilter filter,string[] args){
            if(args.Length==0){
                var lines=new List<string>{"Console filter: "+(filter.Enabled?"on":"off")};
                lines.AddRange(filter.Rules.OrderBy(r=>r.Key==ConsoleFilter.Wildcard?0:1).ThenBy(r=>r.Key,StringComparer.OrdinalIgnoreCase).Select(r=>"  "+r.Key+"="+r.Value));
                lines.Add(Usage);return new LogCommandResult(false,lines);
            }
            if(args.Length==1){
                if(string.Equals(args[0],"on",StringComparison.OrdinalIgnoreCase)){filter.Enabled=true;return new LogCommandResult(true,"Console filter is now on.");}
                if(string.Equals(args[0],"off",StringComparison.OrdinalIgnoreCase)){filter.Enabled=false;return new LogCommandResult(true,"Console filter is now off.");}
                return new LogCommandResult(false,Usage);
            }
            var source=string.Join(" ",args.Take(args.Length-1)).Trim();var word=args[args.Length-1];
            if(string.Equals(word.Trim(),"default",StringComparison.OrdinalIgnoreCase)){
                return filter.Remove(source)?new LogCommandResult(true,"Console filter rule for "+source+" removed."):new LogCommandResult(false,"No console filter rule for "+source+".");
            }
            if(!ConsoleFilter.TryParseLevel(word,out var level))return new LogCommandResult(false,"Unknown filter level '"+word+"'. Use all, info, warning, error, none or default.");
            try{filter.Set(source,level);}catch(ArgumentException){return new LogCommandResult(false,"Invalid log source '"+source+"'.");}
            return new LogCommandResult(true,Describe(source,level));
        }

        static string Describe(string source,ConsoleFilterLevel level){
            switch(level){
                case ConsoleFilterLevel.All:return "Console shows everything from "+source+".";
                case ConsoleFilterLevel.Error:return "Console shows only errors from "+source+".";
                case ConsoleFilterLevel.None:return "Console hides "+source+".";
                default:return "Console shows "+level+" and worse from "+source+".";
            }
        }
    }
}
