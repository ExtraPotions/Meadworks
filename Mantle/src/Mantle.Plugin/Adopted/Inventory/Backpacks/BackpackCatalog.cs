using System;
using System.Collections.Generic;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Mantle's backpacks, one per biome (the user's plan, 2026-09-28): slots and carry weight take turns, 4 more slots,
    /// then 50 more carry weight, then 4 more slots, and so on; each recipe takes the pack before it, so a pack is upgraded
    /// rather than piled up. The Deep North pack may also carry what portals refuse (only with Backpack Portal Pass on).
    /// These are the built-in defaults; <c>Mantle.Backpacks.yml</c> overrides any number of them.
    /// </summary>
    public static class BackpackCatalog
    {
        public const string TrollhideId = "Mantle_TrollhideBackpack";

        private static readonly Dictionary<string, BackpackKind> byName = new Dictionary<string, BackpackKind>();
        private static readonly Dictionary<string, BackpackKind> byId = new Dictionary<string, BackpackKind>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, BackpackKind> byHash = new Dictionary<int, BackpackKind>();

        public static IReadOnlyList<BackpackKind> All { get; } = new List<BackpackKind>
        {
            new BackpackKind("Mantle_DeerhideSatchel", "deerhide", "Deerhide Satchel", "A small satchel of deer hide, patched with leather scraps.", 2f,
                new BackpackStats("piece_workbench", 2, "DeerHide:10, LeatherScraps:8", 4, 0f, false)),
            new BackpackKind(TrollhideId, "trollhide", "Trollhide Backpack", "A roomy pack of troll leather under a ragged hide flap.", 4f,
                new BackpackStats("forge", 1, "Mantle_DeerhideSatchel:1, TrollHide:20, Bronze:2", 4, 50f, false)),
            new BackpackKind("Mantle_RootboundPack", "rootbound", "Rootbound Pack", "Bog leather in a frame of woven roots, its seams sealed with guck.", 5f,
                new BackpackStats("forge", 2, "Mantle_TrollhideBackpack:1, Iron:5, Root:10, Guck:4", 8, 50f, false)),
            new BackpackKind("Mantle_WolfpeltPack", "wolfpelt", "Wolfpelt Pack", "Dark leather under a shaggy wolf pelt, fastened with silver.", 5f,
                new BackpackStats("forge", 3, "Mantle_RootboundPack:1, WolfPelt:12, Silver:6", 8, 100f, false)),
            new BackpackKind("Mantle_LoxHauler", "lox", "Lox Hauler", "A black metal frame hauling a load wrapped in lox fur.", 6f,
                new BackpackStats("forge", 4, "Mantle_WolfpeltPack:1, LoxPelt:8, BlackMetal:8, LinenThread:12", 12, 100f, false)),
            new BackpackKind("Mantle_CarapacePack", "carapace", "Carapace Pack", "Scale hide armored with carapace plates, stitched with blue jute.", 6f,
                new BackpackStats("blackforge", 1, "Mantle_LoxHauler:1, Carapace:12, ScaleHide:8, JuteBlue:6", 12, 150f, false)),
            new BackpackKind("Mantle_AsksvinPack", "asksvin", "Asksvin Pack", "Scaled asksvin hide with glowing flametal fittings.", 6f,
                new BackpackStats("blackforge", 2, "Mantle_CarapacePack:1, AskHide:10, FlametalNew:6, MorgenSinew:4", 16, 150f, false)),
            new BackpackKind("Mantle_MoosehidePack", "moosehide", "Moosehide Pack", "Heavy moose hide with a winter fur ruff, gold fittings and antler toggles.", 7f,
                new BackpackStats("blackforge", 4, "Mantle_AsksvinPack:1, MooseHide:10, MooseSinew:4, Gold:5", 16, 200f, true)),
        };

        /// <summary>The kind of an item by its shared name ($mantle_backpack_...), or null for any other item.</summary>
        public static BackpackKind Of(ItemDrop.ItemData item) => item != null && Tables().TryGetValue(item.m_shared.m_name, out BackpackKind kind) ? kind : null;

        public static BackpackKind ById(string id)
        {
            Tables();
            return id != null && byId.TryGetValue(id.Trim(), out BackpackKind kind) ? kind : null;
        }

        public static BackpackKind ByHash(int hash)
        {
            Tables();
            return byHash.TryGetValue(hash, out BackpackKind kind) ? kind : null;
        }

        private static Dictionary<string, BackpackKind> Tables()
        {
            if (byName.Count == 0)
            {
                foreach (BackpackKind kind in All)
                {
                    byName[kind.Token] = kind;
                    byId[kind.Id] = kind;
                    byHash[kind.Hash] = kind;
                }
            }
            return byName;
        }
    }
}
