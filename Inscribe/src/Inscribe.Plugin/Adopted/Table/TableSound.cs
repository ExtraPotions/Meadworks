// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Bundles;
using ExtraPotions.Inscribe.Adopted.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// The sound of a use at the Rune Table (user 2026-10-08: "some like upgrade noise, instead of that vanilla build
    /// noise"): the game's skill level-up chime, <c>sfx_levelup</c> from the player's level-up effects. The game plays it
    /// flat (2D) for the levelling player alone; this copy is placed in the world like the stations' craft sounds (full to
    /// 4 m, gone at 20 m), so only players near the table hear it. Local only, never networked.
    /// </summary>
    internal static class TableSound
    {
        private const string Sound = "sfx_levelup";
        private const float Life = 6f;

        private static GameObject? _template;
        private static bool _tried;

        public static void Play(Vector3 at)
        {
            GameObject? template = Template();
            if (template != null)
            {
                Object.Destroy(Object.Instantiate(template, at, Quaternion.identity), Life);
            }
        }

        private static GameObject? Template()
        {
            if (_tried || ZNetScene.instance == null)
            {
                return _template;
            }
            _tried = true;
            GameObject? sound = Find(ZNetScene.instance.GetPrefab("Player")?.GetComponent<Player>());
            if (sound == null)
            {
                Log.Warn($"the game's {Sound} was not found; Rune Table uses play no sound");
                return null;
            }
            _template = PrefabBench.Copy(sound, "ecf_table_levelup");
            Object.DestroyImmediate(_template.GetComponent<ZNetView>());
            Place(_template.GetComponent<AudioSource>());
            return _template;
        }

        private static GameObject? Find(Player? player)
        {
            foreach (EffectList.EffectData effect in player != null ? player.m_skillLevelupEffects.m_effectPrefabs : new EffectList.EffectData[0])
            {
                if (effect.m_prefab != null && effect.m_prefab.name == Sound)
                {
                    return effect.m_prefab;
                }
            }
            return null;
        }

        private static void Place(AudioSource? source)
        {
            if (source == null)
            {
                return;
            }
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 20f;
        }
    }
}
