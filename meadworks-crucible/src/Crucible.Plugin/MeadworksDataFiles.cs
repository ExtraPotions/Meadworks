// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>A product's registered text file. All validation, stale-content checks, authority and publication remain owned by the product.</summary>
    public sealed class DataFileRegistration {
        public string ProductId { get; }
        public string Name { get; }
        public string Path { get; }
        public Func<string> Read { get; }
        public Func<string,IReadOnlyList<string>> Validate { get; }
        public Func<string,string,bool> Apply { get; }
        public DataFileRegistration(string productId,string name,string path,Func<string> read,Func<string,IReadOnlyList<string>> validate,Func<string,string,bool> apply){
            if(string.IsNullOrWhiteSpace(productId)||string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(path))throw new ArgumentException("Registered file identity required.");
            ProductId=productId;Name=name;Path=System.IO.Path.GetFullPath(path);Read=read??throw new ArgumentNullException(nameof(read));Validate=validate??throw new ArgumentNullException(nameof(validate));Apply=apply??throw new ArgumentNullException(nameof(apply));
        }
    }
    public static class MeadworksDataFiles {
        static readonly object gate=new object();static readonly Dictionary<string,DataFileRegistration> files=new Dictionary<string,DataFileRegistration>(StringComparer.Ordinal);
        public static IReadOnlyList<DataFileRegistration> Registered {get{lock(gate)return files.Values.OrderBy(f=>f.ProductId,StringComparer.Ordinal).ThenBy(f=>f.Name,StringComparer.Ordinal).ToArray();}}
        public static IDisposable Register(DataFileRegistration file){if(file==null)throw new ArgumentNullException(nameof(file));string id=file.ProductId+"\n"+file.Name;lock(gate){if(files.ContainsKey(id)||files.Values.Any(f=>PortablePaths.Same(f.Path,file.Path)))throw new InvalidOperationException("Data file already registered.");files.Add(id,file);}return new Lease(id,file);}
        sealed class Lease:IDisposable {readonly string id;DataFileRegistration? file;internal Lease(string id,DataFileRegistration file){this.id=id;this.file=file;}public void Dispose(){lock(gate){if(file!=null&&files.TryGetValue(id,out var current)&&ReferenceEquals(current,file))files.Remove(id);file=null;}}}
    }
}
