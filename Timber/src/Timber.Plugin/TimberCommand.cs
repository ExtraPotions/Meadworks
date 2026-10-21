// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using Jotunn.Entities;

namespace ExtraPotions.Timber.Runtime
{
    /// <summary>The `timber` console command: `timber bindings` and `timber import ...`.</summary>
    public sealed class TimberCommand : ConsoleCommand
    {
        public override string Name => "timber";

        public override string Help => TimberText.Text("command_help");

        public override void Run(string[] args)
        {
            string verb = args != null && args.Length > 0 ? args[0] : "";
            if (verb == "bindings")
            {
                foreach (string line in BindingReport.Lines()) global::Console.instance?.Print(line);
                return;
            }
            if (verb == "import")
            {
                foreach (string line in Plugin.Importer?.Command(args!) ?? new[] { TimberText.Text("import_unavailable") }) global::Console.instance?.Print(line);
                return;
            }
            global::Console.instance?.Print(Help);
        }
    }
}
