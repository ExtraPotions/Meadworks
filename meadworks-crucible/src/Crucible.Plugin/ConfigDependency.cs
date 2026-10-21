// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Meadworks.Crucible
{
    /// <summary>Optional live configuration-UI dependency. Stored values and server authority are unchanged.</summary>
    public sealed class ConfigDependency
    {
        public Func<bool> Enabled { get; }
        public Func<bool>? Visible { get; }
        public Func<string> Reason { get; }
        public ConfigDependency(Func<bool> enabled,Func<string> reason,Func<bool>? visible=null)
        {Enabled=enabled??throw new ArgumentNullException(nameof(enabled));Reason=reason??throw new ArgumentNullException(nameof(reason));Visible=visible;}
    }
}
