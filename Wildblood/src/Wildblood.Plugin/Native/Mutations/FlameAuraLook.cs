// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Flame visuals adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: owned lifecycle, portable emission policy and live ember fallback.
using ExtraPotions.Wildblood.Visuals;
using System;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What Flamebound looks like on its own body, as Frostbound wears its frost: the game's own Burning look (the
    /// flames, cinders, glow and light a burning character wears) cloned onto the creature, sized to it as the game sizes
    /// a worn status effect, made endless and drawn by the game's own emission. Its glow is one particle, as on any
    /// burning creature, so fog shows one soft halo, never the wall that thousands of hand-fed glows once made. Its
    /// crackle is the game's sound for a player taking fire damage and stays silent. Purely cosmetic, on every client
    /// (never on a dedicated server, which draws nothing); it rides the creature, so it moves and is destroyed with it.
    /// While a Cloaked or Cloning creature hides, the fire hides with it, or it would give the creature away.
    /// </summary>
    public sealed class FlameAuraLook : MonoBehaviour
    {
        private const string Source = "vfx_Burning";
        private static readonly string[] Keywords = { "burning", "flame", "fire" };

        private GameObject? _clone;
        private ParticleSystem[] _systems = Array.Empty<ParticleSystem>();
        private EliteController? _controller;
        private CloakBehaviour? _cloak;
        private Action? _step;

        private void Start() => Guard.Run("FlameAuraLook.Start", Build);

        private void Build()
        {
            Character character = GetComponent<Character>();
            _controller = GetComponent<EliteController>();
            _cloak = GetComponent<CloakBehaviour>();
            if (character == null || _controller == null)
            {
                enabled = false;
                return;
            }
            GameObject? prefab = EffectResolver.Resolve(Source, Keywords, "Flamebound body fire");
            _clone = CosmeticClone.Spawn(prefab, transform, character.GetCenterPoint(), endless: true);
            if (_clone != null)
            {
                Dress(_clone, FlameVisualPolicy.BodyScale(character.GetRadius()));
                Step();
            }
            // only a hider with a fire to hide needs watching every frame
            enabled = _clone != null && (_cloak != null || GetComponent<CloneBehaviour>() != null);
        }

        /// <summary>Sized from the game's size for a worn status effect (twice the body's radius), silent and looped for good.</summary>
        private void Dress(GameObject clone, float scale)
        {
            clone.transform.localScale *= scale;
            foreach (Behaviour part in clone.GetComponentsInChildren<Behaviour>(true))
            {
                if (part is AudioSource || part is ZSFX)
                {
                    part.enabled = false;
                }
            }
            _systems = clone.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in _systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                system.Play(withChildren: false);
            }
        }

        private void Update() => Guard.Run("FlameAuraLook.Update", _step ??= Step);

        private void Step()
        {
            if (_clone == null || !_controller!.View.IsValid())
            {
                return;
            }
            bool show = _controller.Ready && FlameVisualPolicy.BodyVisible(_cloak != null && _cloak.Hidden, CloneStore.Hiding(_controller.View.GetZDO()));
            if (_clone.activeSelf == show)
            {
                return;
            }
            _clone.SetActive(show);
            if (show)
            {
                foreach (ParticleSystem system in _systems)
                {
                    system.Play(withChildren: false); // woken again, a part that does not play on awake stays still
                }
            }
        }
    }
}
