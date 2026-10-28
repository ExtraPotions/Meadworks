// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;

namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class OwnValuePublicationException : IOException {
        internal OwnValuePublicationException(Exception error):base("Configuration was published, but final readback failed.",error){}
    }
    public static partial class MeadworksConfig {
        enum EditStep { Authority, Assignment, Serialize, ReadOriginal, StageCopy, Parse, Bind, Save, Flush, Reload, ReadStaged, ConflictRead, Publish, Readback, Cleanup, Count }
        [ThreadStatic] static EditTimingData? activeEditTiming;
        static partial void EnableEditTiming(ref bool enabled);
        static partial void CaptureEditFrame(ref int frame);
        static partial void WriteEditTiming(string line);

        sealed class EditTimingData {
            internal readonly long Start=Stopwatch.GetTimestamp();
            internal readonly long[] Steps=new long[(int)EditStep.Count];
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly int Gc0=GC.CollectionCount(0),Gc1=GC.CollectionCount(1),Gc2=GC.CollectionCount(2);
            internal int Frame=-1,Bytes,Persists,Amendments;
            internal string Operation,Product="unknown",Route="unresolved";
            internal bool Complete;
            internal EditTimingData(string operation){Operation=operation;CaptureEditFrame(ref Frame);}
            internal string Line(long end,int gc0,int gc1,int gc2){
                var text=new StringBuilder("stall-timing config-edit operation=").Append(Operation).Append(" product=").Append(Product).Append(" route=").Append(Route)
                    .Append(" status=").Append(Complete?"completed":"exception").Append(" frame=").Append(Frame).Append(" thread=").Append(Thread)
                    .Append(" start_ticks=").Append(Start).Append(" end_ticks=").Append(end).Append(" frequency=").Append(Stopwatch.Frequency)
                    .Append(" total_ms=").Append(((end-Start)*1000d/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture)).Append(" bytes=").Append(Bytes).Append(" persists=").Append(Persists).Append(" amendments=").Append(Amendments)
                    .Append(" gc=").Append(gc0-Gc0).Append('/').Append(gc1-Gc1).Append('/').Append(gc2-Gc2);
                for(int i=0;i<Steps.Length;i++)text.Append(' ').Append((EditStep)i).Append("_ticks=").Append(Steps[i]);
                return text.ToString();
            }
        }
        readonly struct EditSpan:IDisposable {
            readonly EditTimingData? data;readonly EditStep step;readonly long start;
            internal EditSpan(EditTimingData? data,EditStep step){this.data=data;this.step=step;start=data==null?0:Stopwatch.GetTimestamp();}
            public void Dispose(){if(data!=null)data.Steps[(int)step]+=Stopwatch.GetTimestamp()-start;}
        }
        readonly struct EditTiming:IDisposable {
            readonly EditTimingData? data;readonly bool owner;
            EditTiming(EditTimingData? data,bool owner){this.data=data;this.owner=owner;}
            internal static EditTiming Begin(string operation){
                if(activeEditTiming!=null)return new EditTiming(activeEditTiming,false);
                bool enabled=false;EnableEditTiming(ref enabled);if(!enabled)return default;
                var data=new EditTimingData(operation);activeEditTiming=data;return new EditTiming(data,true);
            }
            internal EditSpan Step(EditStep step)=>new EditSpan(data,step);
            internal void Describe(string product,AmendRoute route){if(data!=null){if(data.Amendments++==0){data.Product=product;data.Route=route.ToString();}}}
            internal void FileBytes(int bytes){if(data!=null){data.Bytes+=bytes;data.Persists++;}}
            internal void Success(){if(owner&&data!=null)data.Complete=true;}
            public void Dispose(){
                if(!owner||data==null)return;
                long end=Stopwatch.GetTimestamp();int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);activeEditTiming=null;
                // A diagnostic listener must not change the edit's success or exception.
                try{WriteEditTiming(data.Line(end,gc0,gc1,gc2));}catch{}
            }
        }
        /// <summary>Persists one admitted own-value edit. Other settings come from disk, never live server overlays.</summary>
        public static void PersistOwnValue(ConfigEntryBase entry){
            using var timing=EditTiming.Begin("PersistOwnValue");
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            var file=entry.ConfigFile;
            if(!file.ContainsKey(entry.Definition)||!ReferenceEquals(file[entry.Definition],entry))throw new InvalidOperationException("The configuration entry is no longer bound to its file.");
            string value;using(timing.Step(EditStep.Serialize))value=TomlTypeConverter.ConvertToString(entry.BoxedValue,entry.SettingType);
            string path=Path.GetFullPath(file.ConfigFilePath),temporary=path+".edit-"+Guid.NewGuid().ToString("N")+".tmp";
            byte[]? before;using(timing.Step(EditStep.ReadOriginal))before=File.Exists(path)?File.ReadAllBytes(path):null;
            timing.FileBytes(before?.Length??0);
            bool reserved=false,published=false;
            try{
                using(timing.Step(EditStep.StageCopy)){
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    reserved=true;if(before!=null)output.Write(before,0,before.Length);
                }
                }
                ConfigFile candidate;using(timing.Step(EditStep.Parse))candidate=CreateOwnValueStaging(temporary);
                ConfigEntryBase copy;
                using(timing.Step(EditStep.Bind)){
                var bind=typeof(ConfigFile).GetMethods(BindingFlags.Instance|BindingFlags.Public).Single(method=>method.Name=="Bind"&&method.IsGenericMethodDefinition&&method.GetParameters().Length==3&&method.GetParameters()[0].ParameterType==typeof(ConfigDefinition));
                copy=(ConfigEntryBase)bind.MakeGenericMethod(entry.SettingType).Invoke(candidate,new object[]{entry.Definition,entry.DefaultValue,entry.Description})!;
                copy.BoxedValue=entry.BoxedValue;
                if(TomlTypeConverter.ConvertToString(copy.BoxedValue,copy.SettingType)!=value)throw new IOException("The configuration serializer changed the requested value.");
                }
                using(timing.Step(EditStep.Save))candidate.Save();
                using(timing.Step(EditStep.Flush))using(var written=new FileStream(temporary,FileMode.Open,FileAccess.Write,FileShare.None))written.Flush(true);
                using(timing.Step(EditStep.Reload)){
                if(!ConfigValueReadback.TryReload(copy,temporary))candidate.Reload();
                if(TomlTypeConverter.ConvertToString(copy.BoxedValue,copy.SettingType)!=value)throw new IOException("The saved configuration value failed readback.");
                }
                byte[] staged;using(timing.Step(EditStep.ReadStaged))staged=File.ReadAllBytes(temporary);
                using(timing.Step(EditStep.ConflictRead))if(before==null?File.Exists(path):!File.Exists(path)||!before.SequenceEqual(File.ReadAllBytes(path)))throw new IOException("The configuration changed on disk during the edit.");
                using(timing.Step(EditStep.Publish)){
                try{
                    if(before==null)File.Move(temporary,path);else File.Replace(temporary,path,null);
                }catch(IOException error){throw new IOException("Configuration atomic "+(before==null?"creation":"replacement")+" failed (HRESULT 0x"+error.HResult.ToString("X8")+"): "+error.Message,error);}
                }
                reserved=false;published=true;
                using(timing.Step(EditStep.Readback))if(!staged.SequenceEqual(File.ReadAllBytes(path)))throw new IOException("The published configuration failed readback.");
            }catch(Exception error) when(published){throw new OwnValuePublicationException(error);}
            finally{using(timing.Step(EditStep.Cleanup))if(reserved&&File.Exists(temporary))File.Delete(temporary);}
            timing.Success();
        }

        static void AssignOwn<T>(ConfigEntry<T> entry,T value){
            using var timing=EditTiming.Begin("AssignOwn");
            var file=entry.ConfigFile;bool save=file.SaveOnConfigSet;T previous=entry.Value;
            file.SaveOnConfigSet=false;
            try{
                using(timing.Step(EditStep.Assignment))entry.Value=value;
                if(TomlTypeConverter.ConvertToString(entry.Value,typeof(T))!=TomlTypeConverter.ConvertToString(value,typeof(T)))throw new InvalidOperationException("The mod did not retain the requested setting.");
                PersistOwnValue(entry);
            }catch(OwnValuePublicationException){throw;}
            catch{entry.Value=previous;throw;}
            finally{file.SaveOnConfigSet=save;}
            timing.Success();
        }
    }
}
