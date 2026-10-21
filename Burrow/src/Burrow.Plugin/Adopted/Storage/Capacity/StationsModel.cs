using System;
using System.Collections.Generic;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Capacity
{
    /// <summary>
    /// The parsed Burrow.Stations*.yml files: station prefab name (trimmed, any case) to its caps. items: and
    /// fuel: are whole numbers from 1 to 1000; anything else is an error, so the file set is rejected and the
    /// previous caps stay. Whether a station uses a cap at all is only known from its prefab, so that is checked
    /// when the caps are applied (<see cref="StationCapacities"/>).
    /// </summary>
    public sealed class StationsModel : YamlModel
    {
        public const int MaxCap = 1000;

        private const string NothingSet = "no items: or fuel:, the entry does nothing";

        public Dictionary<string, StationCaps> Caps { get; } = new Dictionary<string, StationCaps>(StringComparer.OrdinalIgnoreCase);

        protected override void Read(YamlNode root)
        {
            YamlNode stations = root.Get("stations");
            if (stations.Kind == YamlNodeKind.Map)
            {
                foreach (KeyValuePair<string, YamlNode> entry in stations.Entries)
                    ReadCaps(entry.Key, entry.Value);
            }
            else if (stations.Kind == YamlNodeKind.Scalar || stations.Kind == YamlNodeKind.List)
            {
                stations.Error("expected one entry per station prefab, as in 'smelter: { items: 20, fuel: 40 }'");
            }
            ReadMisplaced(root);
        }

        /// <summary>An entry uncommented without its indentation lands at the top of the file; read it and say so.</summary>
        private void ReadMisplaced(YamlNode root)
        {
            foreach (KeyValuePair<string, YamlNode> entry in root.Entries)
            {
                if (string.Equals(entry.Key, "stations", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (entry.Value.Kind != YamlNodeKind.Map)
                {
                    entry.Value.Warn("unknown key");
                    continue;
                }
                entry.Value.Warn("this entry sits at the top of the file; indent it two spaces so it is inside 'stations:' (applied anyway)");
                ReadCaps(entry.Key, entry.Value);
            }
        }

        private void ReadCaps(string prefab, YamlNode node)
        {
            if (node.Kind != YamlNodeKind.Map)
            {
                if (node.Kind == YamlNodeKind.Null)
                    node.Warn(NothingSet);
                else
                    node.Error("a station needs items:, fuel: or both, as in { items: 20, fuel: 40 }");
                return;
            }
            bool itemsRead = ReadCap(node.Get("items"), out int items);
            bool fuelRead = ReadCap(node.Get("fuel"), out int fuel);
            if (!itemsRead || !fuelRead)
                return;
            if (items == 0 && fuel == 0)
            {
                node.Warn(NothingSet);
                return;
            }
            Caps[prefab.Trim()] = new StationCaps(items, fuel);
        }

        /// <summary>A missing key is 0 (not set); a present one must be a whole number from 1 to <see cref="MaxCap"/>.</summary>
        private static bool ReadCap(YamlNode node, out int cap)
        {
            cap = 0;
            if (node.Kind == YamlNodeKind.Missing)
                return true;
            if (!node.TryInt(out cap))
                return false;
            if (cap >= 1 && cap <= MaxCap)
                return true;
            node.Error($"must be a whole number from 1 to {MaxCap}, found {cap}");
            cap = 0;
            return false;
        }
    }
}
