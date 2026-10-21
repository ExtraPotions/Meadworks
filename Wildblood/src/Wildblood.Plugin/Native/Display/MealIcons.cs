// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display
{
    /// <summary>
    /// Draws what a Devouring creature has eaten on its nameplate, one icon per creature, the way
    /// <see cref="PouchIcons"/> draws what a thief carries: the eaten creature's trophy, or the game's horned monster head
    /// for one with no trophy (<see cref="MealSprites"/>). On StarRow's own line, right-justified - to the health bar's
    /// right edge, or just left of the pouch icons of a creature that is Thieving too - oldest leftmost, shrinking and
    /// then dropping the oldest when they do not all fit (<see cref="IconRow"/>). Read from the meal list on the
    /// creature's ZDO, so every client draws the same icons whoever owns it. Lives under the nameplate GameObject
    /// EnemyHudCloakPatch hides as a whole for a Cloaked creature, so it hides and fades with the rest of the plate.
    /// </summary>
    public sealed class MealIcons : MonoBehaviour, IPlateIcons
    {
        private const string Prefix = "ecr_meal_";
        private const float RefreshInterval = 0.5f; // a meal can come at any moment

        private Character _character = null!;
        private EliteController? _controller;
        private RectTransform _healthBar = null!;
        private IconRow? _row;
        private int _lastCount = -1;
        private float _lastStarEnd = -1f;
        private float _lastRight = -1f;
        private float _timer, _lastSize = -1f;

        public void Init(Character character, RectTransform healthBar)
        {
            _character = character;
            _healthBar = healthBar;
            _row = new IconRow(healthBar, Prefix);
        }

        private void Start() => Guard.Run("MealIcons.Start", static self => self.Refresh(), this);

        private void Update() => Guard.Run("MealIcons.Update", static self => self.Poll(), this);

        private void Poll()
        {
            _timer += Time.deltaTime;
            if (_timer < RefreshInterval)
            {
                return;
            }
            _timer = 0f;
            Refresh();
        }

        // Re-laid out only when something it depends on moved: a new meal, the star row, or the pouch icons beside it.
        private void Refresh()
        {
            if (_controller == null && _character != null)
            {
                _controller = _character.GetComponent<EliteController>();
            }
            ZDO? zdo = _controller != null && _controller.Ready ? _controller.View.GetZDO() : null;
            if (!Configuration.ShowDevouredCreatures.Value || zdo == null || _healthBar == null || _row == null)
            {
                _row?.Clear(); _lastCount = -1;
                return;
            }
            float starEnd = IconRow.StarRowEnd(_healthBar);
            float right = IconRow.LeftOf(_healthBar, PouchIcons.Prefix);
            float size = IconRow.VanillaSize * Configuration.DevouredIconSize.Value;
            if (!Moved(MealStore.Count(zdo), starEnd, right, size))
            {
                return;
            }
            List<Sprite?> sprites = MealSprites.For(MealStore.Load(zdo));
            _row.Layout(sprites, starEnd, right, size);
            if (sprites.Contains(null))
            {
                _lastCount = -1; // a picture not found yet (the map not built yet): look again next poll
            }
        }

        private bool Moved(int count, float starEnd, float right, float size)
        {
            if (count == _lastCount && Mathf.Approximately(starEnd, _lastStarEnd) && Mathf.Approximately(right, _lastRight) && size.Equals(_lastSize))
            {
                return false;
            }
            _lastCount = count;
            _lastSize = size;
            _lastStarEnd = starEnd;
            _lastRight = right;
            return true;
        }

        internal void ClearOwned() => _row?.Clear();

        private void OnDestroy() => _row?.Clear();
    }
}
