// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Almanac.Runtime.Utilities
{
    public sealed class ConversionOption
    {
        internal ConversionOption(ConversionRegistration registration,bool available,string notice){Registration=registration;Available=available;StepAsideNotice=notice;SourceInstalled=Chainloader.PluginInfos.ContainsKey(registration.SourceId);}
        public ConversionRegistration Registration { get; }
        public bool SourceInstalled { get; }
        public bool Available { get; }
        public string StepAsideNotice { get; }
    }
    public sealed class ConversionPlan
    {
        internal ConversionPlan(ConversionTools owner,ConversionRegistration registration,object preview){Owner=owner;Registration=registration;Preview=preview;}
        internal ConversionTools Owner { get; }
        public ConversionRegistration Registration { get; }
        public object Preview { get; }
    }
    /// <summary>AS03 consumer. Product callbacks retain the real plan, conflict rules, schema checks and atomic writer.</summary>
    public sealed class ConversionTools
    {
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        public IReadOnlyList<ConversionOption> List()
        {
            Check();return MeadworksConversion.Registered.Take(1024).Select(registration=>{
                bool available;try{available=registration.Available();}catch{available=false;}
                string notice="";try{notice=MeadworksStepAside.Decide(registration.ProductId).Notice;}catch{}
                return new ConversionOption(registration,available,notice);
            }).ToArray();
        }
        public ConversionPlan Preview(ConversionRegistration registration)
        {Check();Current(registration);if(!registration.Available())throw new InvalidOperationException("The registered conversion source is unavailable.");return new ConversionPlan(this,registration,registration.Preview());}
        public object Apply(ConversionPlan plan,IReadOnlyDictionary<string,string> choices)
        {
            Check();if(plan==null||!ReferenceEquals(plan.Owner,this))throw new ArgumentException("Preview belongs to another conversion controller.",nameof(plan));
            Current(plan.Registration);if(!plan.Registration.Available())throw new InvalidOperationException("The registered conversion source is unavailable.");
            // Backups are mandatory. Registered products recheck all current locks and skip remote synced targets.
            return plan.Registration.Apply(plan.Preview,choices??throw new ArgumentNullException(nameof(choices)),true);
        }
        static void Current(ConversionRegistration registration)
        {if(registration==null||!MeadworksConversion.Registered.Any(current=>ReferenceEquals(current,registration)))throw new InvalidOperationException("The conversion is no longer registered.");}
        void Check(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Conversions must run on their owning thread.");}
    }
    public sealed class DataFileDocument
    {
        internal DataFileDocument(DataFileTools owner,DataFileRegistration registration,string text){Owner=owner;Registration=registration;Original=text;}
        internal DataFileTools Owner { get; }
        public DataFileRegistration Registration { get; }
        public string Original { get; }
    }
    /// <summary>AS05 consumer. It writes only product-registered files through the product's validator and stale/authority checked callback.</summary>
    public sealed class DataFileTools
    {
        const int MaxBytes=1024*1024;
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        public IReadOnlyList<DataFileRegistration> List(){Check();return MeadworksDataFiles.Registered.Take(1024).ToArray();}
        public DataFileDocument Read(DataFileRegistration registration)
        {Check();Current(registration);string text=registration.Read();Bounded(text);return new DataFileDocument(this,registration,text);}
        public IReadOnlyList<string> Validate(DataFileDocument document,string text)
        {Check();Document(document);Bounded(text);return document.Registration.Validate(text)??throw new InvalidOperationException("The product returned no validation result.");}
        public bool Apply(DataFileDocument document,string text)
        {
            Check();Document(document);if(Validate(document,text).Count!=0)return false;
            if(!string.Equals(document.Registration.Read(),document.Original,StringComparison.Ordinal))throw new InvalidOperationException("The data file changed while it was being edited; reload it first.");
            return document.Registration.Apply(document.Original,text);
        }
        void Document(DataFileDocument document)
        {if(document==null||!ReferenceEquals(document.Owner,this))throw new ArgumentException("Document belongs to another data-file controller.",nameof(document));Current(document.Registration);}
        static void Current(DataFileRegistration registration)
        {if(registration==null||!MeadworksDataFiles.Registered.Any(current=>ReferenceEquals(current,registration)))throw new InvalidOperationException("The data file is no longer registered.");}
        static void Bounded(string text){if(text==null||Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new ArgumentException("Registered text is limited to 1 MiB.",nameof(text));}
        void Check(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Data-file tools must run on their owning thread.");}
    }
}
