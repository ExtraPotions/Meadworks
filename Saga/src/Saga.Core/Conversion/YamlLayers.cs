// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace ExtraPotions.Saga.Conversion {
    /// <summary>Saga's six hidden C01 carriers preserve empty YAML files and independent layer boundaries.</summary>
    public static class YamlLayers {
        public const int MaximumPacketLength=1500000;
        public static string Encode(IEnumerable<string> layers){var result="v1\n"+string.Join("\n",layers.Select(t=>Convert.ToBase64String(Encoding.UTF8.GetBytes(t))));if(result.Length>MaximumPacketLength)throw new InvalidDataException("YAML packet too large");return result;}
        public static IReadOnlyList<string> Decode(string packet){if(packet==null||packet.Length>MaximumPacketLength)throw new InvalidDataException("YAML packet too large");if(!packet.StartsWith("v1\n",StringComparison.Ordinal))throw new InvalidDataException("Unknown YAML packet version");return Array.AsReadOnly(packet.Substring(3).Split('\n').Select(t=>Encoding.UTF8.GetString(Convert.FromBase64String(t))).ToArray());}
    }
}
