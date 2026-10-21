using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    // Cheat is appended so existing numeric values stay stable; see ConfigEditPolicy for its rules.
    public enum ConfigScope { Local, ServerAuthoritative, Cheat }
    public static class ConfigAuthority {
        public static bool CanEdit(ConfigScope scope,SessionSnapshot session)=>session!=null&&CanEdit(scope,session.AuthorityReady,session.IsAdmin);
        public static bool CanEdit(ConfigScope scope,bool authorityAvailable,bool isAdmin)=>scope==ConfigScope.Local || (scope==ConfigScope.ServerAuthoritative&&authorityAvailable&&isAdmin);
    }
    public sealed class MigrationStep {
        public int From { get; } public int To { get; } public Action Apply { get; }
        public MigrationStep(int from,int to,Action apply){if(from<0||to!=from+1)throw new ArgumentException("Migration must advance one schema.");From=from;To=to;Apply=apply??throw new ArgumentNullException(nameof(apply));}
    }
    public static class ConfigMigration {
        public static void Run(int current,int target,IReadOnlyList<MigrationStep> steps,Action backup,Action<int> persist) {
            if(current<0||target<0)throw new ArgumentOutOfRangeException(nameof(current));
            if(current>target)throw new NotSupportedException("Future configuration schema; file left unchanged.");
            var path=new List<MigrationStep>();
            for(var version=current;version<target;version++){var candidates=steps.Where(s=>s.From==version&&s.To==version+1).ToArray();if(candidates.Length!=1)throw new InvalidOperationException("Missing or ambiguous migration step.");path.Add(candidates[0]);}
            if(path.Count==0)return;
            backup();foreach(var step in path)step.Apply();persist(target);
        }
    }
}
