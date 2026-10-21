// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Items;
using ExtraPotions.Inscribe.Runtime;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// One dropped item the glow manager knows (display.md section 5): whether it glows and in which color, and its
    /// light and loot beam, and the glint hook's small ground glint (<see cref="GlowBeams"/>; Legendary gear). The item's data is reloaded from its ZDO only when
    /// the saved item bytes really changed (<see cref="Reload"/>: a moving drop's ZDO changes revision with every
    /// position update, its item bytes do not), and the color is re-decided only when the item's data object, its
    /// custom-data dictionary (the game's <c>Load</c> replaces it), state, glint decision, rules generation or <c>Glow stones</c> switch
    /// changed, so a tick touches the parse cache only for new or changed items. Viewing client only.
    /// </summary>
    internal sealed class GlowItem
    {
        private const float LightHeight = 0.3f;

        private byte[]? _bytes;
        private ItemDrop.ItemData? _data;
        private Dictionary<string, string>? _source;
        private ItemState? _state;
        private bool _glintWanted;
        private int _rulesGeneration = -1;
        private bool _stonesSwitch;
        private Light? _light;
        private GameObject? _beam;
        private Color32 _beamColor;
        private GameObject? _glint;

        public GlowItem(ItemDrop drop)
        {
            Drop = drop;
        }

        public ItemDrop Drop { get; }
        public int Stamp { get; set; }
        public float SqrDistance { get; set; }
        public bool Glows { get; private set; }
        public Color32 Color { get; private set; }
        public bool EnhancementGlint { get; private set; }

        /// <summary>The rarity (or rune) light shows; false for an item that only glints.</summary>
        public bool LightOn { get; private set; }

        /// <summary>
        /// The game's <c>Load</c> (a full re-read of the item) only when the ZDO's item bytes are a new array with new
        /// content: the first sight, or a real change to the item on the ground. A moving drop's ZDO is re-sent with
        /// every position update, as a new array holding the same bytes; a byte compare skips those.
        /// </summary>
        public void Reload(ZDO zdo)
        {
            byte[]? bytes = zdo.GetByteArray(ZDOVars.s_itemData);
            if (ReferenceEquals(bytes, _bytes))
            {
                return;
            }
            bool same = bytes != null && _bytes != null && SameBytes(bytes, _bytes);
            _bytes = bytes;
            if (!same)
            {
                Drop.Load();
            }
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Re-decides glow and color if anything it depends on changed; removes the light if it stops glowing.</summary>
        public void Refresh()
        {
            ItemDrop.ItemData data = Drop.m_itemData;
            bool stones = ModSettings.GlowStones.Value;
            ItemState state = ItemState.Read(data);
            bool glintWanted = Runtime.ItemBadgeHooks.GlintFor(data);
            if (ReferenceEquals(data, _data) && ReferenceEquals(data?.m_customData, _source)
                && ReferenceEquals(state, _state) && glintWanted == _glintWanted
                && _rulesGeneration == ActiveRules.Generation && stones == _stonesSwitch)
            {
                return;
            }
            _data = data;
            _source = data?.m_customData;
            _state = state;
            _glintWanted = glintWanted;
            _rulesGeneration = ActiveRules.Generation;
            _stonesSwitch = stones;
            Glows = Decide(data, state, stones, glintWanted, out Color32 color, out bool glint, out bool light);
            LightOn = light;
            Color = color;
            if (EnhancementGlint != glint)
            {
                RemoveGlow();
            }
            EnhancementGlint = glint;
            if (!Glows)
            {
                RemoveGlow();
            }
        }

        /// <summary>The glint (<see cref="Runtime.ItemBadgeHooks.Glint"/>: Legendary gear) shows with the rarity light; rarity lights optionally add a loot beam.</summary>
        public void Shine(float intensity, float range, bool beam)
        {
            if (EnhancementGlint)
            {
                if (_glint == null)
                {
                    _glint = GlowBeams.CreateGlint(Drop.transform);
                }
                if (_glint != null)
                {
                    GlowBeams.Upright(_glint.transform);
                    if (!_glint.activeSelf)
                    {
                        _glint.SetActive(true);
                    }
                }
                if (!LightOn)
                {
                    return;
                }
            }
            ShineLight(intensity, range);
            if (beam)
            {
                ShowBeam();
            }
            else
            {
                RemoveBeam();
            }
        }

        // Each value is written only when it differs (rule 14): a resting drop's light is left alone.
        private void ShineLight(float intensity, float range)
        {
            if (_light == null)
            {
                _light = GlowLights.Create(Drop.transform);
            }
            Transform at = _light.transform;
            Vector3 position = Drop.transform.position + Vector3.up * LightHeight;
            if (at.position != position)
            {
                at.position = position;
            }
            Color color = Color;
            if (_light.color != color)
            {
                _light.color = color;
            }
            SetLevels(_light, intensity, range);
        }

        private static void SetLevels(Light light, float intensity, float range)
        {
            if (light.intensity != intensity)
            {
                light.intensity = intensity;
            }
            if (light.range != range)
            {
                light.range = range;
            }
            if (!light.enabled)
            {
                light.enabled = true;
            }
        }

        // The beam is made in the item's color the first time, made again when the color changed, and stood upright on
        // every tick (a dropped item rolls; the beam is its child).
        private void ShowBeam()
        {
            if (_beam != null && !_beamColor.Equals(Color))
            {
                RemoveBeam();
            }
            if (_beam == null)
            {
                _beam = GlowBeams.Create(Drop.transform, Color);
                _beamColor = Color;
            }
            if (_beam != null)
            {
                GlowBeams.Upright(_beam.transform);
                if (!_beam.activeSelf)
                {
                    _beam.SetActive(true);
                }
            }
        }

        /// <summary>Over the nearest-N cap: owned lights and particles are switched off, not destroyed.</summary>
        public void Dim()
        {
            if (_light != null)
            {
                _light.enabled = false;
            }
            if (_beam != null)
            {
                _beam.SetActive(false);
            }
            if (_glint != null)
            {
                _glint.SetActive(false);
            }
        }

        /// <summary>Removes owned effects when the item loses its cue or ground glow is switched off.</summary>
        public void RemoveGlow()
        {
            if (_light != null)
            {
                UnityEngine.Object.Destroy(_light.gameObject);
            }
            _light = null;
            RemoveBeam();
            if (_glint != null)
            {
                UnityEngine.Object.Destroy(_glint);
            }
            _glint = null;
        }

        private void RemoveBeam()
        {
            if (_beam != null)
            {
                UnityEngine.Object.Destroy(_beam);
            }
            _beam = null;
        }

        /// <summary>Magic items glow in their rarity color when the rarity says <c>glow</c> (never the base rarity);
        /// stones glow only with <c>Glow stones</c> on; the glint hook (Legendary gear) adds the small ground glint.</summary>
        private static bool Decide(ItemDrop.ItemData? data, ItemState state, bool stones, bool glintWanted,
            out Color32 color, out bool glint, out bool light)
        {
            color = default;
            glint = false;
            light = false;
            if (data == null)
            {
                return false;
            }
            if (InscribeModules.StepAside.RarityDisplay && ItemClasses.IsStone(data))
            {
                // Tint from the Items area; a rune without one glows white.
                color = stones ? (Color32)StoneVisuals.TintOfPrefab(ItemTier.PrefabName(data)) : default;
                light = stones;
                return stones;
            }
            RarityDef? rarity = state.Rarity;
            glint = glintWanted;
            if (rarity == null || rarity.IsBase || !InscribeModules.StepAside.RarityDisplay || !rarity.Glow)
            {
                return glint;
            }
            color = rarity.Color32;
            light = true;
            return true;
        }
    }
}
