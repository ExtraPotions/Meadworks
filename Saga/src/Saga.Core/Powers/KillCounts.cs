// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace ExtraPotions.Saga.Powers {
    public sealed class KillCounts {
        public const string Key = SagaCore.DataPrefix + "powers.kills";
        public const string SeededKey = SagaCore.DataPrefix + "powers.seeded";
        /// <summary>Kall's own seeding mark: characters seeded before Kall existed get Kall from world progress once, without reseeding the others.</summary>
        public const string SeededKallKey = SagaCore.DataPrefix + "powers.seeded.kall";
        readonly Dictionary<Boss,int> counts;
        KillCounts(Dictionary<Boss,int> counts) { this.counts=counts; }
        public static KillCounts Empty => new KillCounts(new Dictionary<Boss,int>());
        public IReadOnlyDictionary<Boss,int> Values => counts;
        public int Of(Boss boss) => counts.TryGetValue(boss,out var n)?n:0;
        public string Text => string.Join(",",counts.Where(p=>p.Value>0).OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value.ToString(CultureInfo.InvariantCulture)));
        public KillCounts Add(Boss boss,int amount) {
            if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));
            var copy=new Dictionary<Boss,int>(counts); copy[boss]=(int)Math.Min(int.MaxValue,(long)Of(boss)+amount); return new KillCounts(copy);
        }
        public KillCounts Reset(Boss boss) { var copy=new Dictionary<Boss,int>(counts); copy.Remove(boss); return new KillCounts(copy); }
        public KillCounts ResetAll()=>Empty;
        public static KillCounts Read(string? text) {
            var copy=new Dictionary<Boss,int>();
            foreach(var part in (text??"").Split(',')) {
                var p=part.Trim().Split(':');
                if(p.Length==2&&PowerCatalog.TryParse(p[0],out var boss)&&int.TryParse(p[1],NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>=0)copy[boss]=n;
            }
            return new KillCounts(copy);
        }
    }
    public readonly struct CreditCandidate {
        public CreditCandidate(long id,float x,float y,float z,bool aliveAndLoaded) { Id=id;X=x;Y=y;Z=z;AliveAndLoaded=aliveAndLoaded; }
        public long Id {get;} public float X {get;} public float Y {get;} public float Z {get;} public bool AliveAndLoaded {get;}
    }
    public static class KillCredit {
        public static IReadOnlyList<long> Eligible(float x,float y,float z,float range,IEnumerable<CreditCandidate> players) => players
            .Where(p=>p.AliveAndLoaded&&(range==0f||range>0f&&((double)p.X-x)*(p.X-x)+((double)p.Y-y)*(p.Y-y)+((double)p.Z-z)*(p.Z-z)<=range*range)).Select(p=>p.Id).Distinct().ToArray();
        public static KillCounts Seed(KillCounts existing,IEnumerable<Boss> defeated) {
            var result=existing;
            foreach(var boss in defeated.Distinct())if(result.Of(boss)==0)result=result.Add(boss,1);
            return result;
        }
    }

    /// <summary>Who may grant a boss kill. Health in the packet is not a fact: the server reads the ZDO, and a client trusts only the server.</summary>
    public static class BossKillRules {
        public static bool ServerAccepts(bool senderOwnsBoss, bool knownPrefab, bool dead, bool first) =>
            senderOwnsBoss && knownPrefab && dead && first;

        public static bool ClientAccepts(bool senderIsServer, bool knownBoss, bool inRange, bool first) =>
            senderIsServer && knownBoss && inRange && first;
    }
}
