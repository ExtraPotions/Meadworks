// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Cloaked: hides the creature beyond its reveal distance from the local player and phases it back in over a fade
    /// time as the player closes, rather than snapping, so a hard cut never looks like a rendering fault. A hysteresis
    /// band (fade in at reveal distance, out only at reveal distance + margin) stops it strobing for a player standing
    /// on the boundary. This is a client-side visual, so it runs on every client; the nameplate patch reads
    /// <see cref="Hidden"/> (through <see cref="PlateVeils"/>) and hides the nameplate in step. The renderers are touched
    /// only on a frame the fade moves, not every frame. See DECISIONS.md on why the body may snap on opaque shaders.
    /// </summary>
    public sealed class CloakBehaviour : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock _block = null!;
        private float _reveal = 6f;
        private float _margin = 1f;
        private float _fadeTime = 0.5f;
        private bool _revealed;
        private float _phase;
        private float _lastAlpha = float.NaN;
        private float _applied = float.NaN; // the phase the renderers last showed; NaN until first applied
        private Character? _character;
        private EliteController? _controller;
        private bool[] _originalEnabled = System.Array.Empty<bool>();
        private MaterialPropertyBlock[] _originalBlocks = System.Array.Empty<MaterialPropertyBlock>();

        /// <summary>True while the creature is fully cloaked, so the nameplate can hide in exact step with the body.</summary>
        public bool Hidden => isActiveAndEnabled && _phase <= 0f;

        // Joins at once, not at Start: it is hidden from the moment it is added, and its nameplate must hide with it.
        private void Awake()
        {
            _character = GetComponent<Character>();
            if (_character != null)
            {
                PlateVeils.Join(_character, this);
            }
        }

        private void OnDisable()
        {
            if (_block == null || float.IsNaN(_applied)) return;
            bool appliedVisible = _applied > 0.001f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;
                if (renderer.enabled == appliedVisible) renderer.enabled = _originalEnabled[i];
                Material shared = renderer.sharedMaterial;
                if (shared == null || !shared.HasProperty(ColorId)) continue;
                renderer.GetPropertyBlock(_block);
                Color owned = shared.GetColor(ColorId);
                owned.a = _lastAlpha;
                if (_block.GetColor(ColorId) == owned)
                {
                    Color original = _originalBlocks[i].HasColor(ColorId)
                        ? _originalBlocks[i].GetColor(ColorId) : shared.GetColor(ColorId);
                    _block.SetColor(ColorId, original); // retain unrelated property changes made by another owner
                    renderer.SetPropertyBlock(_block);
                }
            }
            _lastAlpha = float.NaN;
            _applied = float.NaN; // resume applies the same source fade from its existing phase
        }

        private void OnDestroy()
        {
            OnDisable();
            PlateVeils.Leave(_character, this);
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _originalEnabled = new bool[_renderers.Length];
            _originalBlocks = new MaterialPropertyBlock[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalEnabled[i] = _renderers[i].enabled;
                _originalBlocks[i] = new MaterialPropertyBlock();
                _renderers[i].GetPropertyBlock(_originalBlocks[i]);
            }
            EliteController controller = GetComponent<EliteController>();
            _controller = controller;
            if (controller != null)
            {
                _reveal = Scaling.Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Cloaked,
                    Fields.RevealDistance);
                _margin = controller.Rules.PowerOf(Mutation.Cloaked, Fields.FadeMargin);
                _fadeTime = controller.Rules.PowerOf(Mutation.Cloaked, Fields.FadeTime);
            }
            Apply();
        }

        private void Update() => Guard.Run("CloakBehaviour.Update", static self => self.Step(), this);

        private void Step()
        {
            EliteController? controller = _controller;
            if (controller != null)
            {
                _reveal = Scaling.Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Cloaked, Fields.RevealDistance);
                _margin = controller.Rules.PowerOf(Mutation.Cloaked, Fields.FadeMargin);
                _fadeTime = controller.Rules.PowerOf(Mutation.Cloaked, Fields.FadeTime);
            }
            Player local = Player.m_localPlayer;
            if (local == null)
            {
                return;
            }
            float distance = Vector3.Distance(local.transform.position, transform.position);
            UpdateReveal(distance);
            _phase = Advance(_phase, _revealed ? 1f : 0f);
            Apply();
        }

        /// <summary>Hysteresis: reveal at the reveal distance, re-hide only a margin further out, so the two never coincide.</summary>
        private void UpdateReveal(float distance)
        {
            if (_revealed && distance > _reveal + _margin)
            {
                _revealed = false;
            }
            else if (!_revealed && distance <= _reveal)
            {
                _revealed = true;
            }
        }

        private float Advance(float current, float target)
        {
            if (_fadeTime <= 0f)
            {
                return target;
            }
            return Mathf.MoveTowards(current, target, Time.deltaTime / _fadeTime);
        }

        private void Apply()
        {
            if (_phase == _applied)
            {
                return; // nothing moved since the renderers were last set: at rest, fully hidden or fully shown
            }
            _applied = _phase;
            bool visible = _phase > 0.001f;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                renderer.enabled = visible;
                if (visible)
                {
                    SetAlpha(renderer, _phase);
                }
            }
        }

        /// <summary>Best-effort alpha fade; a no-op on opaque creature shaders, which is why the body can snap.</summary>
        private void SetAlpha(Renderer renderer, float alpha)
        {
            Material shared = renderer.sharedMaterial;
            if (shared == null || !shared.HasProperty(ColorId))
            {
                return;
            }
            renderer.GetPropertyBlock(_block);
            Color color = shared.GetColor(ColorId);
            color.a = alpha;
            _lastAlpha = alpha;
            _block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_block);
        }
    }
}
