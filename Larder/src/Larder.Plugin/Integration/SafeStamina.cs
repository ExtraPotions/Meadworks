// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Independent native-seam implementation of the user-approved Safe Stamina behavior.
// Behavior reference only: https://github.com/jekkle/cartur-safe-stamina/tree/2960c141b64e55852c0d1b423c8d82e64e48555c
// No code copied; inspected source/tree contains no licence grant. Existing adopted FeastMaster owns the cost/regen hooks.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Larder.Exertion;
using ExtraPotions.Larder.Runtime.Adopted.FeastMaster;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Larder.Runtime
{
    public enum SafeStaminaAction { Sprint, Jump, Build, Swim, Attacks, Sneak, Encumbered }

    /// <summary>Optional Exertion policy over the existing native final cost and captured regeneration paths.</summary>
    public static class SafeStamina
    {
        const string Section = "5 - Exertion";
        const float ScanInterval = .25f;
        static ConfigFile config;
        static MeadworksLog log;
        static readonly List<ConfigEntryBase> entries = new List<ConfigEntryBase>();
        static readonly Dictionary<SafeStaminaAction, ConfigEntry<bool>> actions = new Dictionary<SafeStaminaAction, ConfigEntry<bool>>();
        static ConfigEntry<bool> enabled;
        static ConfigEntry<float> radius;
        static ConfigEntry<string> ignoredCreatures;
        static string ignoredSpecification;
        static IgnoredCreatures ignoredNames = new IgnoredCreatures("");
        static WeakReference<Player> scannedPlayer;
        static Vector3 scannedPosition;
        static float scanExpires;
        static bool scannedSafe, wasEnabled, scanWarning;
        static long generation;

        public static bool Enabled => enabled != null && NativeFeastMaster.Active("Exertion") &&
            MeadworksConfig.Effective(enabled, Plugin.Guid, ConfigScope.ServerAuthoritative, false);

        /// <summary>Initialize after NativeFeastMaster; root ticks this before the existing patch switch.</summary>
        public static void Initialize(ConfigFile file, MeadworksLog logger)
        {
            Shutdown();
            config = file ?? throw new ArgumentNullException(nameof(file));
            log = logger;
            enabled = Bind("Safe Area Stamina", false, "Actions switched on below cost no stamina while no hostile creature is within Safe Area Radius. Off keeps the ordinary Exertion rules.");
            radius = Bind("Safe Area Radius", 25f, "Radius in meters checked for hostile creatures. 0 counts only creatures touching you. Only creatures in loaded areas are checked.", new AcceptableValueRange<float>(0f, float.MaxValue));
            ignoredCreatures = Bind("Safe Area Ignored Creatures", "", "Comma-separated creature prefab or displayed names that do not count as nearby enemies. Empty ignores nothing. Matching is case-insensitive.");
            foreach (SafeStaminaAction action in Enum.GetValues(typeof(SafeStaminaAction)))
                actions.Add(action, Bind("Safe Area " + action, action != SafeStaminaAction.Encumbered, action == SafeStaminaAction.Encumbered
                    ? "While safe, encumbered walking costs no stamina and blocked stamina regeneration is allowed. Off keeps the ordinary encumbrance penalty. Other safe-action switches still apply."
                    : action == SafeStaminaAction.Swim
                    ? "Free safe swimming; this also prevents stamina-driven drowning while safe."
                    : action == SafeStaminaAction.Sprint ? "Sprinting costs no stamina while safe."
                    : action == SafeStaminaAction.Jump ? "Jumping costs no stamina while safe."
                    : action == SafeStaminaAction.Build ? "Placing with the hammer, hoe and cultivator costs no stamina while safe."
                    : action == SafeStaminaAction.Sneak ? "Sneaking costs no stamina while safe."
                    : "Weapon and tool swings cost no stamina while safe. Holding a drawn bow still costs stamina."));
            config.SettingChanged += Changed;
            config.ConfigReloaded += Reloaded;
            PatchSwitch.MarkDirty();
        }

        static ConfigEntry<T> Bind<T>(string key, T value, string description, AcceptableValueBase range = null)
        {
            var entry = MeadworksConfig.Bind(config, Section, key, value, description, ConfigScope.ServerAuthoritative, range);
            MeadworksBlockedOptions.Track(Plugin.Guid, entry, ConfigScope.ServerAuthoritative);
            entries.Add(entry);
            return entry;
        }
        static bool ActionEnabled(SafeStaminaAction action) => actions.TryGetValue(action, out var entry) &&
            MeadworksConfig.Effective(entry, Plugin.Guid, ConfigScope.ServerAuthoritative, true);
        public static bool Applies(SafeStaminaAction action) => Enabled && ActionEnabled(action);

        public static void Tick()
        {
            if (config == null) return;
            bool current = Enabled;
            long session = CruciblePlugin.IsReady ? CruciblePlugin.Session.Generation : 0;
            if (current != wasEnabled || session != generation)
            {
                wasEnabled = current; generation = session; Invalidate();
            }
        }
        static void Changed(object sender, SettingChangedEventArgs args) => Invalidate();
        static void Reloaded(object sender, EventArgs args) => Invalidate();
        static void Invalidate() { scannedPlayer = null; scanExpires = 0f; PatchSwitch.MarkDirty(); }

        static bool LocalReady(Player player) => Enabled && player != null && player == Player.m_localPlayer &&
            !player.IsDead() && !player.InIntro() && !player.IsTeleporting() && player.m_nview != null &&
            player.m_nview.IsValid() && player.m_nview.IsOwner();

        static bool Safe(Player player)
        {
            if (!LocalReady(player)) return false;
            Vector3 position = player.transform.position;
            float now = Time.time;
            if (scannedPlayer != null && scannedPlayer.TryGetTarget(out var prior) && ReferenceEquals(prior, player) &&
                now < scanExpires && (position - scannedPosition).sqrMagnitude < .25f)
                return scannedSafe;
            bool safe = false;
            try
            {
                float metres = MeadworksConfig.Effective(radius, Plugin.Guid, ConfigScope.ServerAuthoritative, 25f);
                if (float.IsNaN(metres) || float.IsInfinity(metres) || metres < 0f) return false;
                string specification = MeadworksConfig.Effective(ignoredCreatures, Plugin.Guid, ConfigScope.ServerAuthoritative, "");
                if (!string.Equals(specification, ignoredSpecification, StringComparison.Ordinal))
                {
                    ignoredNames = new IgnoredCreatures(specification);
                    ignoredSpecification = specification;
                }
                safe = true;
                foreach (Character character in Character.GetAllCharacters())
                    if (character != null && character != player && !character.IsDead() &&
                        Vector3.Distance(position, character.transform.position) <= metres && BaseAI.IsEnemy(player, character) &&
                        !ignoredNames.Contains(Utils.GetPrefabName(character.gameObject), character.GetHoverName()))
                    { safe = false; break; }
            }
            catch (Exception error)
            {
                safe = false;
                if (!scanWarning) { scanWarning = true; log?.Warning("Safe-area scan unavailable; ordinary stamina costs retained: " + error.GetType().Name); }
            }
            scannedPlayer = new WeakReference<Player>(player); scannedPosition = position;
            scannedSafe = safe; scanExpires = now + ScanInterval;
            return safe;
        }

        /// <summary>One final factor in the existing field-scaling scope; original finalizers still restore fields.</summary>
        public static float Multiplier(Player player, SafeStaminaAction action, float value) =>
            value > 0f && Applies(action) && Safe(player) ? 0f : value;

        /// <summary>One final returned cost, preserving a weapon's negative-cost refund.</summary>
        public static float Cost(Player player, SafeStaminaAction action, float value) =>
            value > 0f && Applies(action) && Safe(player) ? 0f : value;

        /// <summary>Native swimming can still emit drowning damage at zero stamina after its drain becomes free.</summary>
        public static bool ProtectsFromDrowning(Player player) => Applies(SafeStaminaAction.Swim) && Safe(player);

        /// <summary>Normal frames receive no extra contribution; encumbrance requires its own opt-in.</summary>
        public static bool AllowsBlockedRegeneration(Player player)
        {
            if (!LocalReady(player)) return false;
            StaminaBlock state = StaminaBlock.None;
            if (player.IsEncumbered()) state |= StaminaBlock.Encumbered;
            if (player.IsSwimming() && !player.IsOnGround()) state |= StaminaBlock.Swimming;
            if (player.InAttack()) state |= StaminaBlock.Attacking;
            if (player.InDodge()) state |= StaminaBlock.Dodging;
            if (player.m_wallRunning) state |= StaminaBlock.WallRunning;
            return SafeStaminaRules.AllowsBlockedRegeneration(state, ActionEnabled(SafeStaminaAction.Swim),
                ActionEnabled(SafeStaminaAction.Attacks), ActionEnabled(SafeStaminaAction.Encumbered)) && Safe(player);
        }

        public static void Shutdown()
        {
            if (config != null) { config.SettingChanged -= Changed; config.ConfigReloaded -= Reloaded; }
            foreach (var entry in entries) MeadworksBlockedOptions.Untrack(entry);
            entries.Clear(); actions.Clear(); enabled = null; radius = null; config = null; log = null;
            ignoredCreatures = null; ignoredSpecification = null; ignoredNames = new IgnoredCreatures("");
            scannedPlayer = null; scanExpires = 0f; wasEnabled = scanWarning = false; generation = 0;
            PatchSwitch.MarkDirty();
        }
    }
}
