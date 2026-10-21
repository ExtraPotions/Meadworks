// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Thing.RemoteConfig;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    internal static class ThingPanelYaml
    {
        static Edit? active;
        sealed class Edit
        {
            internal KitAlmanacDialog Owner = null!;
            internal Action<string> Message = null!;
            internal string File = "", Caption = "", Token = "", Version = "";
            internal long Generation;
            internal int Total, At;
            internal readonly List<byte> Read = new List<byte>();
            internal byte[] Proposed = Array.Empty<byte>();
            internal bool Current => ReferenceEquals(active, this) && Owner.Panel != null && Owner.Window.IsOpen && ThingPanel.CanConfigure && Generation == CruciblePlugin.Session.Generation;
            internal bool Request(string[] args, Action<IReadOnlyList<string>> receive) => Current && ThingPanelJobs.Start(args, lines => { if (Current) receive(lines); }, failure: args.Length > 1 && args[1] == "yaml-commit" ? () => { if (Current) { Message(ThingText.Text("config_unknown")); active = null; } } : null);
            internal void Failed(IReadOnlyList<string> lines) { Message(string.Join("\n", lines)); active = null; }
            internal void Open(bool writing)
            {
                Request(new[] { "config", "yaml-open", File }, lines =>
                {
                    if (!YamlTransfer.TryHeader(lines, out var token, out var version, out var total)) { Failed(lines); return; }
                    Token = token;
                    if (writing)
                    {
                        if (version != Version) { Cancel(() => Failed(new[] { ThingText.Text("config_stale") })); return; }
                        Request(new[] { "config", "yaml-begin", Token, Proposed.Length.ToString(CultureInfo.InvariantCulture), FileVersion.Hash(Proposed) }, ready =>
                        { if (ready.Count != 1 || ready[0] != "THING_YAML_READY") { Failed(ready); return; } At = 0; Put(); });
                    }
                    else { Version = version; Total = total; At = 0; Read.Clear(); Get(); }
                });
            }
            internal void Get()
            {
                if (At == Total) { Cancel(Editor); return; }
                var offset = At;
                Request(new[] { "config", "yaml-read", Token, offset.ToString(CultureInfo.InvariantCulture) }, lines =>
                {
                    if (!YamlTransfer.TryChunk(lines, Token, offset, out var bytes) || bytes.Length != Math.Min(YamlTransfer.ChunkBytes, Total - offset)) { Failed(lines); return; }
                    Read.AddRange(bytes); At += bytes.Length; Get();
                });
            }
            internal void Cancel(Action next)
            {
                Request(new[] { "config", "yaml-cancel", Token }, lines =>
                { Token = ""; if (lines.Count != 1 || lines[0] != "THING_YAML_CANCELED") { Failed(lines); return; } next(); });
            }
            internal void Editor()
            {
                string text;
                try { text = YamlTransfer.Text(Read.ToArray()); } catch { Failed(new[] { ThingPanel.Ui("invalid") }); return; }
                Message(ThingPanel.Ui("config_yaml_hint"));
                KitAlmanacTextEditor.Open(Owner.Window, Caption, text,
                    value => { try { return YamlTransfer.Bytes(value).Length > YamlTransfer.MaxBytes ? ThingPanel.Ui("invalid") : null!; } catch { return ThingPanel.Ui("invalid"); } },
                    value =>
                    {
                        if (!Current || ThingPanelJobs.Busy) return;
                        Proposed = YamlTransfer.Bytes(value);
                        ThingPanelForms.Confirm(Owner.Window, Caption + "\n" + ThingPanel.Ui("config_write_warning"), () => { if (Current) Open(true); }, settingsOnly: true);
                    });
            }
            internal void Put()
            {
                if (At == Proposed.Length)
                {
                    Request(new[] { "config", "yaml-commit", Token }, lines => { Token = ""; Message(string.Join("\n", lines)); active = null; }); return;
                }
                var end = At + Math.Min(YamlTransfer.ChunkBytes, Proposed.Length - At);
                var frame = new[] { "config", "yaml-put", Token, At.ToString(CultureInfo.InvariantCulture) }.Concat(YamlTransfer.Pack(Proposed.Skip(At).Take(end - At).ToArray())).ToArray();
                Request(frame, lines =>
                {
                    if (lines.Count != 1 || lines[0] != "THING_YAML_RECEIVED\t" + end.ToString(CultureInfo.InvariantCulture)) { Failed(lines); return; }
                    At = end; Put();
                });
            }
        }
        internal static void Open(KitAlmanacDialog owner, string file, string caption, Action<string> message)
        {
            active = new Edit { Owner = owner, File = file, Caption = caption, Message = message, Generation = CruciblePlugin.Session.Generation }; active.Open(false);
        }
        internal static void Reset() => active = null;
    }
}
