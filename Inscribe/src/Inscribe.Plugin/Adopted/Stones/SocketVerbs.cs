// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; gem recovery.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Display;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// <c>socket</c> (the Dvergr Chisel, sockets.md section 5): 1-3 sockets, by the dropped gear's odds for the item's
    /// rarity (<see cref="SocketDrops.Cut"/>), into a weapon, staff, armour piece, shield or (PackPanel) backpack that
    /// has none, any rarity. An item with sockets already (one or more, dropped or cut) refuses it.
    /// </summary>
    internal sealed class SocketVerb : IStoneVerb
    {
        public StoneResult Run(StoneJob job)
        {
            if (!SocketSwitch.On)
            {
                return StoneResult.Refuse("gems_off");
            }
            if (GemCatalog.BaseOf(job.Class) == SocketBase.None)
            {
                return StoneResult.Refuse("no_socket_here");
            }
            if (job.State.Sockets > 0)
            {
                return StoneResult.Refuse("has_sockets");
            }
            int count = SocketDrops.Cut(job.State.RarityId, RollRandom.Create());
            return StoneResult.Success(job.State.ToBuilder().SetSockets(count).Build(), count == 1 ? "socket_cut" : "sockets_cut",
                job.ItemName, count.ToString());
        }
    }

    /// <summary>
    /// <c>gem</c> (the gems, sockets.md sections 3-4): the gem's stat for the item's base, its tier rolled now on the
    /// item's own ladder (<see cref="GemRolls"/>), into the next empty socket. With every socket full the player picks the
    /// socket whose gem it replaces (<see cref="StoneResult.ChooseSocket"/>, <see cref="GemChooser"/>); the old gem comes back
    /// by the host's Gem Recovery chance for the item's tier, else it is lost.
    /// </summary>
    internal sealed class GemVerb : IStoneVerb
    {
        public StoneResult Run(StoneJob job)
        {
            if (!SocketSwitch.On)
            {
                return StoneResult.Refuse("gems_off");
            }
            if (job.State.Sockets == 0)
            {
                return StoneResult.Refuse("gem_no_socket");
            }
            AffixDef? stat = StatOf(job);
            if (stat == null)
            {
                return StoneResult.Refuse("gem_wrong_item", job.StoneName);
            }
            int socket = job.Socket >= 0 ? job.Socket : job.State.FreeSockets > 0 ? job.State.FilledSockets : -1;
            return socket < 0 ? StoneResult.ChooseSocket() : Place(job, stat, socket);
        }

        /// <summary>The live, enabled inscription this gem gives on the item's base; null when it does not fit.</summary>
        private static AffixDef? StatOf(StoneJob job)
        {
            string? id = GemCatalog.StatFor(job.Def!.Id, GemCatalog.BaseOf(job.Class));
            AffixDef? def = id != null ? job.Rules.Affixes.Get(id) : null;
            return def != null && def.Enabled ? def : null;
        }

        private static StoneResult Place(StoneJob job, AffixDef stat, int socket)
        {
            AffixRoll? roll = GemRolls.Roll(stat, job.RollContext());
            if (roll == null)
            {
                return StoneResult.Refuse("gem_wrong_item", job.StoneName);
            }
            ItemStateBuilder builder = job.State.ToBuilder();
            bool replaces = socket < job.State.FilledSockets;
            string? oldGem = replaces ? OldGem(job.State, socket) : null;
            if (!builder.SetGem(socket, new GemRoll(job.Def!.Id, roll.Value)))
            {
                return StoneResult.Refuse("sockets_full");
            }
            string line = AffixLines.Sentence(stat.Id, roll.Value.Value, stat) + "  "
                + Words.Localize("$ecf_ui_tier", stat.ShownTier(roll.Value.Tier).ToString());
            if (!replaces)
            {
                return StoneResult.Success(builder.Build(), "gem_socketed", job.StoneName, job.ItemName, line);
            }
            // Gem recovery: rolled once here, in the dry run, by the host's chance for the item's tier.
            bool kept = oldGem != null && ExtraPotions.Inscribe.Rarity.GemRecovery.Returns(
                Runtime.GemRecoveryRuntime.Percent(job.State.RarityId), RollRandom.Create().NextDouble());
            string oldName = oldGem != null ? Words.Localize(job.Rules.Economy.Stone(oldGem)?.Name ?? oldGem) : "?";
            return StoneResult.Success(builder.Build(), kept ? "gem_swap_kept" : "gem_swap_lost", job.StoneName, (socket + 1).ToString(), line, oldName)
                .Returning(kept ? oldGem : null);
        }

        /// <summary>The gem id set in <paramref name="socket"/>, or null when none or unreadable.</summary>
        private static string? OldGem(ItemState state, int socket)
        {
            for (int index = 0; index < state.Gems.Count; index++)
            {
                if (state.GemSocketAt(index) == socket)
                {
                    return state.Gems[index].GemId;
                }
            }
            return null;
        }
    }
}
