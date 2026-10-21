// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// Builds the kraken's prefabs once and registers them whenever ZNetScene wakes, identically on the server and every
    /// client: the creature (a copy of the game's sea serpent wearing the kraken's model from the embedded bundle,
    /// AssetWorkshop assets/ecp_kraken) and its corpse. It also registers the ink's status effect with the game's item
    /// database and hands the screen its ink splats. Its loot items come first (<see cref="KrakenLoot"/>). Its spawn joins
    /// the game's own spawn lists (<see cref="KrakenSpawns"/>).
    /// </summary>
    public static class KrakenPrefabs
    {
        public const string Creature = "ECP_Kraken";
        public const string Corpse = "ECP_Kraken_corpse";
        private const string Serpent = "Serpent";

        /// <summary>The creature prefab, once built; null until the first ZNetScene wakes.</summary>
        public static GameObject? Prefab { get; private set; }

        private static GameObject? corpse;

        public static void Install(Harmony harmony)
        {
            NetPrefabs.OnSceneAwake(harmony, scene => Guard.Run("KrakenPrefabs.Build", () => Build(scene, harmony)));
            Settings.Changed += () => SafeCall.Run("kraken settings", Reapply);
        }

        private static void Build(ZNetScene scene, Harmony harmony)
        {
            if (scene.GetPrefab(Creature) != null)
            {
                return;
            }
            // First, so the kraken's drops can name its loot; a kraken without its own loot still comes.
            SafeCall.Run("KrakenLoot.Build", () => KrakenLoot.Build(scene, harmony));
            if (Prefab == null && !Create(scene, harmony))
            {
                return;
            }
            InkStatus.Register(harmony, KrakenLook.InkIcon());
            NetPrefabs.Register(scene, corpse!);
            NetPrefabs.Register(scene, Prefab!);
            KrakenSpawns.Refresh();
            KrakenWords.Add(Localization.instance);
            Log.Info($"Kraken ready: {Creature}, {Corpse}.");
        }

        /// <summary>Builds both prefabs, or logs what is missing and builds none (no kraken then, but nothing else is held up).</summary>
        private static bool Create(ZNetScene scene, Harmony harmony)
        {
            GameObject? serpent = scene.GetPrefab(Serpent);
            if (serpent == null || serpent.GetComponent<Character>() == null || serpent.GetComponent<MonsterAI>() == null)
            {
                Log.Error($"Kraken not built: the game's {Serpent} is missing or no longer a creature.");
                return false;
            }
            AssetBundle bundle = EmbeddedBundle.Load(typeof(KrakenPrefabs).Assembly, KrakenModel.Bundle);
            Material skin = KrakenLook.Skin(serpent);
            InkScreen.Use(KrakenLook.Splats(bundle));
            InkStatus.Hold = KrakenSettings.InkBlind;
            int layer = serpent.GetComponentInChildren<Renderer>(true)?.gameObject.layer ?? 0;
            corpse = KrakenCorpse.Build(bundle, skin, layer);
            Prefab = KrakenCreature.Build(serpent, bundle, skin, corpse, scene);
            return true;
        }

        /// <summary>After a settings change: the spawn, the ink's blindness, and the health of new krakens.</summary>
        private static void Reapply()
        {
            KrakenSpawns.Refresh();
            InkStatus.Hold = KrakenSettings.InkBlind;
            if (Prefab != null)
            {
                Prefab.GetComponent<Character>().m_health = KrakenSettings.Health;
            }
        }
    }
}
