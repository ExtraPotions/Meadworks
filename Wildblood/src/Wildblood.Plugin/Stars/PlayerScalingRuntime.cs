// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0, adapted from CreatureHarmonyPatches.cs (CreatureGameSettings.ApplyDifficulty)
// at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Changed by expDARE, 2026: Wildblood namespace; values come from the Pressure settings, are re-read every tick because received server
// values raise no change event, apply only while the Pressure module is on, and the game's own values are put back when it is not.
// Meadworks addition: the write stands down for an installed mod that owns the fields (CreatureManager, or the Yield Scaling To list) and for
// any other writer seen by its effect (the fields no longer hold what was last written), leaving their values and saying so once.
// SPDX-License-Identifier: GPL-3.0-only
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Localization;
using ExtraPotions.Wildblood.Stars;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Health Per Extra Player, Damage Per Extra Player and Max Players For Scaling: the game's own per-player difficulty fields
    /// (Game.m_healthScalePerPlayer, m_damageScalePerPlayer, m_difficultyScaleMaxPlayers), which every machine reads for the creatures it owns.
    /// </summary>
    internal static class PlayerScalingRuntime
    {
        internal const string ModuleId = "pressure_scaling";
        static Game? owner;
        static PlayerScalingCoordinator coordinator = new PlayerScalingCoordinator();
        static readonly PlayerScalingOwnersCache ownersCache = new PlayerScalingOwnersCache();

        static PlayerScalingValues Read(Game game) => new PlayerScalingValues(game.m_healthScalePerPlayer, game.m_damageScalePerPlayer, game.m_difficultyScaleMaxPlayers);

        static void Write(Game game, PlayerScalingValues values)
        {
            game.m_healthScalePerPlayer = values.HealthPerPlayer;
            game.m_damageScalePerPlayer = values.DamagePerPlayer;
            game.m_difficultyScaleMaxPlayers = values.MaxPlayers;
        }

        internal static void Tick(MeadworksLog log)
        {
            Game? game = Game.instance;
            if (!ReferenceEquals(game, owner))
            {
                // A new Game object has its own defaults; the old one is gone with its world.
                owner = game;
                coordinator.Reset();
            }
            if (game == null) return;
            var settings = Plugin.Settings;
            bool on = settings != null && PlayerScaling.Acts(WildbloodModules.Active(WildbloodModule.Pressure), true);
            var owners = on ? ownersCache.Installed(Chainloader.PluginInfos.Keys, settings!.Get(WildbloodSettings.Pressure.YieldScalingTo)) : (System.Collections.Generic.IReadOnlyList<ModuleSource>)System.Array.Empty<ModuleSource>();
            var desired = on
                ? PlayerScaling.Resolve(settings!.Get(WildbloodSettings.Pressure.HealthPerExtraPlayer), settings.Get(WildbloodSettings.Pressure.DamagePerExtraPlayer),
                    settings.Get(WildbloodSettings.Pressure.MaxPlayersForScaling))
                : default;
            var step = coordinator.Step(on, owners.Count > 0, Read(game), desired);
            if (step.Write) Write(game, step.Values);
            if (step.StartedYield != PlayerScalingYieldReason.None) Notice(step.StartedYield, owners, log);
        }

        static void Notice(PlayerScalingYieldReason reason, System.Collections.Generic.IReadOnlyList<ModuleSource> owners, MeadworksLog log)
        {
            if (reason == PlayerScalingYieldReason.NamedMod)
            {
                var source = owners[0];
                MeadworksStepAside.ReportModule(Plugin.Guid, ModuleId, new SourceMod(source.Guid, source.Name), WildbloodCore.TokenNamespace, WildbloodTextKeys.StepAsidePressureScaling);
                return;
            }
            var text = WildbloodText.Text(WildbloodTextKeys.PressureScalingExternal);
            log.Warning(text);
            WildbloodText.Centre(text, log);
        }

        internal static void Restore()
        {
            var game = owner;
            if (game != null && game == Game.instance)
            {
                var step = coordinator.Restore(Read(game));
                if (step.Write) Write(game, step.Values);
            }
            coordinator.Reset();
        }

        internal static void Reset()
        {
            Restore();
            owner = null;
        }
    }
}
