// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/MineBreak.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A rock chunk or a single-piece rock broken by a miner, on the rock's ZDO owner: the machine that applied the
    /// damage and spawned the game's own drops. <see cref="ChunkBreaks"/> (MineRock5, MineRock) and
    /// <see cref="PieceBreaks"/> (single-piece Destructibles) build the <see cref="RockBreak"/>; <see cref="Dispatch"/>
    /// hands it to every break feature, each guarded, in a fixed order, then spawns the extra rolls they asked for:
    /// <list type="number">
    /// <item><see cref="Veins.OnBreak"/>: rich vein stars, extra rolls for everyone who mines the deposit.</item>
    /// <item><see cref="PickaxePerks.OnBreak"/>: extra ore and the clean strike roll (<see cref="CleanStrikeMarks.Take"/>).</item>
    /// <item><see cref="MineFinds.OnBreak"/>: finds, which spawn their own items and callout.</item>
    /// <item><see cref="ExtraDrops.Spawn"/>: the extra rolls, once, as the game spawns the chunk's drops.</item>
    /// </list>
    /// Every chunk counts, however it broke (<see cref="BreakCause"/>): a hit, a splash, or a collapse after the hit
    /// took its support. The game's own drops have already spawned when the features run.
    /// </summary>
    public static class MineBreak
    {
        /// <summary>The break being dispatched right now; null outside <see cref="Dispatch"/>.</summary>
        public static RockBreak Open { get; private set; }

        public static void Dispatch(RockBreak broken)
        {
            if (broken == null || broken.Miner == null || broken.Rock == null || !PickSkill.Active)
                return;
            RockBreak outer = Open;
            Open = broken;
            try
            {
                HookGuard.Run("veins", () => Veins.OnBreak(broken));
                HookGuard.Run("pickaxe yield", () => PickaxePerks.OnBreak(broken));
                HookGuard.Run("mine finds", () => MineFinds.OnBreak(broken));
                HookGuard.Run("extra drops", () => ExtraDrops.Spawn(broken));
            }
            finally
            {
                Open = outer;
            }
        }
    }
}
