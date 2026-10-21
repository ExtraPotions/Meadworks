// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Crucible hidden string carriers, authority-only disk access and owned reload lifecycle.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Core;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// One YAML family end to end: this machine's files, the build (merge, parse, validate), and the server binding
    /// through a Charter article carrying the author's file texts (never the parsed model). The author (server, host,
    /// single player, or a player the server does not bind) adopts its own files and publishes them; a bound player
    /// adopts what the server pushed. A family that fails validation keeps the previous rules; at startup, with no
    /// previous rules, the author falls back to the built-in defaults alone and publishes an empty file list, so every
    /// peer plays the same rules and nothing is written to disk.
    /// </summary>
    internal sealed class RuleFamily<T> where T : class
    {
        private readonly FamilySpec _spec;
        private readonly Func<YamlMappingNode, RuleIssues, T?> _parse;
        private readonly RuleFiles _files;
        private List<SourceText> _local = new List<SourceText>();
        private ConfigFile _config = null!;
        private long _session = -1;
        private SessionRole _role;
        private bool IsAuthor => CruciblePlugin.Session.IsAuthority;
        private ConfigEntry<string> _article = null!;

        public RuleFamily(FamilySpec spec, Func<YamlMappingNode, RuleIssues, T?> parse)
        {
            _spec = spec;
            _parse = parse;
            _files = new RuleFiles(spec);
        }

        public T? Active { get; private set; }

        /// <summary>The texts <see cref="Active"/> was built from.</summary>
        public RuleSources InForce { get; private set; } = RuleSources.None;

        /// <summary>Raised after <see cref="Active"/> was replaced.</summary>
        public event Action? Adopted;

        public void Setup(ConfigFile config)
        {
            _config = config;
            _article = MeadworksConfig.BindHidden(config, "0 - Synced rules", _spec.SyncKey, "", "Validated authority YAML texts, managed by Inscribe.", ConfigScope.ServerAuthoritative);
            _article.SettingChanged += OnCarrierChanged;
            RuleReload.SessionChecks.Add(CheckSession);
            RuleReload.Stops.Add(() => _article.SettingChanged -= OnCarrierChanged);
            Recompute();
        }

        /// <summary>Hot reload tick: re-read the files when anything in the family changed on disk.</summary>
        public void PollDisk()
        {
            if (!IsAuthor) return;
            if (_files.Changed())
            {
                ReloadLocal();
            }
        }

        /// <summary>
        /// Builds the family again from the texts it was last given (no disk read): this machine's files on the author,
        /// the server's on a bound player. For API registrations made after the rules loaded (<see cref="CodeLayer"/>,
        /// external effects), which may also make a file valid that was rejected before them.
        /// </summary>
        public void Rebuild() => Guarded("API registrations", Recompute);

        /// <summary>Re-reads this machine's files; the author adopts them when they build, a bound player only keeps them.</summary>
        public FamilyReload ReloadLocal()
        {
            if (!IsAuthor) return FamilyReload.Bound;
            List<SourceText> files = _files.Read();
            if (files.Count == 0 && _local.Count > 0)
            {
                Log.Warn($"every {_spec.Prefix}*.yml file is gone; keeping the loaded {_spec.Prefix} rules");
                return FamilyReload.NoFiles;
            }
            _local = files;
            if (!IsAuthor)
            {
                return FamilyReload.Bound;
            }
            return AdoptLocal() ? FamilyReload.Applied : FamilyReload.Rejected;
        }

        private void Recompute()
        {
            if (IsAuthor)
            {
                AdoptLocal();
            }
            else
            {
                AdoptPushed();
            }
        }

        private void OnPushed()
        {
            if (!IsAuthor)
            {
                AdoptPushed();
            }
        }

        private void OnBindingChanged()
        {
            if (IsAuthor)
            {
                _files.EnsureMainFile();
                _local = _files.Read();
            }
            if (CruciblePlugin.Session.Role == SessionRole.Menu) { _local.Clear(); Active = null; InForce = RuleSources.None; AdoptLocal(); }
            else Recompute();
        }

        private void OnCarrierChanged(object? sender, EventArgs e) => Guarded("server push", OnPushed);
        private void CheckSession()
        {
            var session = CruciblePlugin.Session;
            if (_session == session.Generation && _role == session.Role) return;
            _session = session.Generation; _role = session.Role;
            Guarded("binding change", OnBindingChanged);
        }

        // True when this machine's files built and were adopted.
        private bool AdoptLocal()
        {
            T? model = Build(_local, "this machine's files", out bool defaults);
            if (model != null)
            {
                Adopt(model, new RuleSources(_local, defaults, fromServer: false));
                Publish(_local);
                return true;
            }
            // No previous rules of our own: at startup, or back from a server whose rules must not outlive the session.
            if (Active == null || InForce.FromServer)
            {
                Log.Error($"{_spec.Prefix}: using the built-in defaults alone until the files are fixed");
                List<SourceText> none = new List<SourceText>();
                T? fallback = Build(none, "built-in defaults", out _)
                    ?? Build(none, "built-in defaults without what mods registered", out _, withCode: false);
                Adopt(fallback ?? throw new InvalidOperationException($"the built-in {_spec.MainFile} does not validate"),
                    new RuleSources(none, true, fromServer: false));
                Publish(none);
            }
            return false;
        }

        private void AdoptPushed()
        {
            List<SourceText> files = RuleFormat.KeepCurrent(Unpack(_article.Value), "the server", _spec.Format);
            T? model = Build(files, "the server's files", out bool defaults);
            if (model != null)
            {
                Adopt(model, new RuleSources(files, defaults, fromServer: true));
                return;
            }
            Log.Error($"{_spec.Prefix}: the server's files do not build here; keeping the previous rules. Received:");
            foreach (SourceText file in files)
            {
                Log.Error($"--- {file.Name}\n{file.Text}");
            }
        }

        private T? Build(List<SourceText> files, string what, out bool usedDefaults, bool withCode = true)
        {
            RuleIssues issues = new RuleIssues();
            YamlMappingNode merged = FamilyBuilder.Merge(_spec, files, issues, out usedDefaults, withCode);
            T? model = issues.HasErrors ? null : SafeParse(merged, issues);
            Report(issues, what);
            return issues.HasErrors ? null : model;
        }

        private T? SafeParse(YamlMappingNode merged, RuleIssues issues)
        {
            try
            {
                return _parse(merged, issues);
            }
            catch (Exception e)
            {
                issues.Error("", null, $"internal error while reading the rules: {e}");
                return null;
            }
        }

        private void Report(RuleIssues issues, string what)
        {
            foreach (string note in issues.Notes)
            {
                Log.Info($"{_spec.Prefix}: {note}");
            }
            foreach (string warning in issues.Warnings)
            {
                Log.Warn($"{_spec.Prefix}: {warning}");
            }
            if (issues.HasErrors)
            {
                Log.Error($"{_spec.Prefix} ({what}) has {issues.Errors.Count} error(s); the family was not applied:");
                issues.Errors.ForEach(e => Log.Error("  " + e));
            }
        }

        private void Adopt(T model, RuleSources sources)
        {
            Active = model;
            InForce = sources;
            Adopted?.Invoke();
        }

        private void Publish(List<SourceText> files)
        {
            if (IsAuthor)
            {
                string packed = Pack(files);
                if (_article.Value != packed) MeadworksConfig.Amend(_article, global::ExtraPotions.Inscribe.Runtime.Plugin.Guid, ConfigScope.ServerAuthoritative, packed);
            }
        }

        private static string Pack(List<SourceText> files)
        {
            List<string> packed = new List<string>(files.Count * 2);
            foreach (SourceText file in files)
            {
                packed.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(file.Name)));
                packed.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(file.Text)));
            }
            return string.Join(".", packed);
        }

        private static List<SourceText> Unpack(string? text)
        {
            List<SourceText> files = new List<SourceText>();
            string[] packed = (text ?? "").Split('.');
            if (!string.IsNullOrEmpty(text) && (packed.Length % 2 != 0)) throw new FormatException("The synced YAML carrier is incomplete.");
            for (int i = 0; i + 1 < packed.Length; i += 2)
            {
                files.Add(new SourceText(Encoding.UTF8.GetString(Convert.FromBase64String(packed[i])), Encoding.UTF8.GetString(Convert.FromBase64String(packed[i + 1]))));
            }
            return files;
        }

        private void Guarded(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Error($"{_spec.Prefix} ({what}) threw: {e}");
            }
        }
    }
}
