// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using RootPlugin = ExtraPotions.Wildblood.Runtime.Plugin;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Wildblood.Adopted.Rules {
    /// <summary>Existing C01 transport and AS05 editing around the pinned ECR parser; no source server binder.</summary>
    internal sealed class NativeRuleAuthority : IDisposable {
        const int MaximumBytes = 1024 * 1024;
        readonly ConfigEntry<string> carrier;
        readonly IDisposable fileLease;
        readonly MeadworksLog log;
        readonly string path;
        readonly ConfigFile config;
        sealed class Section {
            internal string Name = "", Default = "";
            internal Func<string, object> Prepare = null!;
            internal Action<object> Commit = null!;
        }
        sealed class SectionLease : IDisposable {
            Action? release;
            internal SectionLease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        readonly List<Section> sections = new List<Section>();
        string attemptedLocal = "", attemptedRemote = "", applied = "", errorSignature = "";
        long generation = -1;
        string worldStamp = "", projectionStamp = "";
        float nextPoll;
        bool disposed;
        internal bool IsReady => !disposed && applied.Length != 0 &&
            generation == CruciblePlugin.Session.Generation &&
            CruciblePlugin.Session.Role != SessionRole.Menu &&
            MeadworksAuthority.ProductActive(RootPlugin.Guid, out _);
        internal NativeRuleAuthority(ConfigFile config, MeadworksLog log) {
            this.log = log;
            this.config = config;
            path = Path.Combine(Paths.ConfigPath, "Meadworks", "wildblood", RuleText.FileName);
            var save = config.SaveOnConfigSet; config.SaveOnConfigSet = false;
            try { carrier = MeadworksConfig.BindHidden(config, "Meadworks", "NativeRulesSnapshot", "",
                "Wildblood rule snapshot, managed by the authority.", ConfigScope.ServerAuthoritative); }
            finally { config.SaveOnConfigSet = save; }
            fileLease = MeadworksDataFiles.Register(new DataFileRegistration(RootPlugin.Guid, "Creature rules", path, ReadForEditor, Validate, Apply));
            ServerLock.PublishReload = Invalidate;
            config.SettingChanged += SettingChanged;
        }
        void SettingChanged(object sender, SettingChangedEventArgs args) { if (!ReferenceEquals(args.ChangedSetting, carrier)) Invalidate(); }
        internal IDisposable RegisterSection<T>(string name, Func<string,T> prepare, Action<T> commit, string defaultText = "") {
            if (disposed) throw new ObjectDisposedException(nameof(NativeRuleAuthority));
            if (sections.Any(row => row.Name == name)) throw new InvalidOperationException("Duplicate Wildblood rule section: " + name);
            var section = new Section { Name = name, Prepare = text => prepare(text)!, Commit = state => commit((T)state), Default = defaultText };
            sections.Add(section); Invalidate();
            return new SectionLease(() => { if (sections.Remove(section) && !disposed) Invalidate(); });
        }
        IReadOnlyList<string> Validate(string text) {
            if (text == null || Encoding.UTF8.GetByteCount(text) > MaximumBytes) return new[] { "Wildblood rules exceed the 1 MiB limit." };
            var result = RuleParser.Parse(text);
            if (result.Rules == null && result.Errors.Count == 0) result.Errors.Add("No valid creature rules.");
            foreach (var section in sections)
                try { section.Prepare(text); }
                catch (Exception error) { result.Errors.Add(section.Name + ": " + error.Message); }
            return result.Errors;
        }
        bool OwnsAuthority() {
            if (disposed || !RootPlugin.Ready || CruciblePlugin.Session.Role == SessionRole.Menu) return false;
            var decision = MeadworksAuthority.Decide(RootPlugin.Guid, ConfigScope.ServerAuthoritative);
            return decision.ProductActive && decision.Allowed && !decision.RequiresServerAmendment;
        }
        string ReadForEditor() => OwnsAuthority() && File.Exists(path) ? ReadDisk() : carrier.Value;
        string ReadDisk() {
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Wildblood rules exceed 1 MiB.");
            return File.ReadAllText(path);
        }
        // The two-second poll reads the file only when its size or write time changed; editor and Apply read it fresh.
        readonly ExtraPotions.Wildblood.Integration.FileTextCache polled = new ExtraPotions.Wildblood.Integration.FileTextCache();
        string ReadDiskPolled() {
            var info = new FileInfo(path);
            if (info.Length > MaximumBytes) throw new InvalidDataException("Wildblood rules exceed 1 MiB.");
            return polled.Get(info.Length, info.LastWriteTimeUtc, () => File.ReadAllText(path));
        }
        bool Apply(string expected, string candidate) {
            if (!OwnsAuthority() || Validate(candidate).Count != 0) return false;
            var current = File.Exists(path) ? ReadDisk() : "";
            if (!string.Equals(current, expected, StringComparison.Ordinal)) return false;
            var temporary = path + ".write-" + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temporary, candidate, new UTF8Encoding(false));
                if (!OwnsAuthority() || (File.Exists(path) ? ReadDisk() : "") != expected) return false;
                if (File.Exists(path)) File.Replace(temporary, path, path + "." + DateTime.UtcNow.Ticks + ".bak"); else File.Move(temporary, path);
                Invalidate(); Tick(); return applied == candidate;
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal void Invalidate() { attemptedLocal = attemptedRemote = ""; nextPoll = 0; polled.Clear(); }
        internal void Tick() {
            if (disposed || !RootPlugin.Ready || RootPlugin.Settings == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 2;
            var receivedWorld = RootPlugin.Settings.WorldValuesStamp;
            if (receivedWorld != worldStamp) { worldStamp = receivedWorld; attemptedRemote = ""; }
            var receivedInputs = NativeRuleProjection.InputsStamp(RootPlugin.Settings);
            if (receivedInputs != projectionStamp) { projectionStamp = receivedInputs; attemptedRemote = ""; }
            if (CruciblePlugin.Session.Role == SessionRole.Menu) return;
            if (generation != CruciblePlugin.Session.Generation) {
                generation = CruciblePlugin.Session.Generation; attemptedLocal = attemptedRemote = applied = "";
            }
            var decision = MeadworksAuthority.Decide(RootPlugin.Guid, ConfigScope.ServerAuthoritative);
            if (!decision.ProductActive) return;
            try {
                if (OwnsAuthority()) {
                    if (!File.Exists(path)) {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllText(path, RuleText.Default + string.Concat(sections.Select(row => "\n" + row.Default)), new UTF8Encoding(false));
                    }
                    var text = ReadDiskPolled();
                    if (text != attemptedLocal) {
                        attemptedLocal = text; var errors = Validate(text);
                        if (errors.Count == 0) {
                            var result = MeadworksConfig.Amend(carrier, RootPlugin.Guid, ConfigScope.ServerAuthoritative, text);
                            if (!result.Applied) attemptedLocal = "";
                        } else Report(string.Join("; ", errors));
                    }
                }
                var snapshot = MeadworksConfig.Effective(carrier, RootPlugin.Guid, ConfigScope.ServerAuthoritative, "");
                if (snapshot.Length == 0 || snapshot == attemptedRemote) return;
                attemptedRemote = snapshot;
                if (Encoding.UTF8.GetByteCount(snapshot) > MaximumBytes) { Report("Authority rules exceed 1 MiB; last good rules retained."); return; }
                var parsed = RuleParser.Parse(snapshot);
                if (parsed.Rules == null || parsed.Errors.Count != 0) { Report("Invalid authority rules; last good rules retained."); return; }
                // Prepare every source section before any engine changes its last-good state.
                var prepared = sections.Select(section => Tuple.Create(section, section.Prepare(snapshot))).ToArray();
                foreach (var warning in parsed.Warnings) log.Warning("Wildblood rules: " + warning);
                NativeRuleProjection.Apply(parsed.Rules, RootPlugin.Settings);
                foreach (var row in prepared) row.Item1.Commit(row.Item2);
                RuleState.Adopt(parsed.Rules); applied = snapshot; errorSignature = "";
            } catch (Exception error) { attemptedLocal = ""; Report(error.GetType().Name + ": " + error.Message); }
        }
        void Report(string message) {
            if (errorSignature == message) return;
            errorSignature = message; log.Warning("Wildblood rules: " + message);
        }
        public void Dispose() {
            if (disposed) return; disposed = true;
            if (ServerLock.PublishReload == (Action)Invalidate) ServerLock.PublishReload = null;
            config.SettingChanged -= SettingChanged;
            sections.Clear();
            fileLease.Dispose();
        }
    }
    internal static class ServerLock {
        internal static Action? PublishReload;
        public static void OnLocalReloaded() => PublishReload?.Invoke();
    }
}
