// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The one target a Relentless creature has picked, held on the creature's owner. It is mirrored into the creature's
    /// ZDO as a ZDOID because ownership moves between machines mid-chase (the game hands a creature to whichever peer is
    /// nearest), and the next owner's AI starts with no target at all: it reads the quarry back and the hunt goes on
    /// without the creature having to notice the player again. A quarry is held only while it is alive, still an enemy,
    /// visible to AI at all (no ghost or debug-fly player, nothing the game marks as never to be targeted) and within
    /// the chase distance; losing any of those forgets it, and the next target the game picks becomes the quarry.
    /// </summary>
    public sealed class RelentlessQuarry
    {
        /// <summary>The quarry's ZDOID on the hunter's ZDO; owner-written, and cleared the moment the quarry is lost.</summary>
        public const string Key = Traits.TraitKeys.Quarry;

        private static readonly KeyValuePair<int, int> KeyHash = ZDO.GetHashZDOID(Key);

        private readonly ZNetView _view;
        private readonly MonsterAI _ai;
        private readonly float _rangeSqr;
        private Character? _current;
        private bool _loaded;

        public RelentlessQuarry(ZNetView view, MonsterAI ai, float chaseDistance)
        {
            _view = view;
            _ai = ai;
            _rangeSqr = chaseDistance * chaseDistance;
        }

        /// <summary>The quarry as last held; read <see cref="Hold"/> for one that is checked this update.</summary>
        public Character? Current => _current;

        /// <summary>
        /// Owner only. Checks the quarry it holds (reading it from the ZDO first on a new owner) and forgets one it has
        /// lost; holding none, it adopts <paramref name="candidate"/> - the target the game has just picked - when that
        /// one qualifies. Returns the quarry it now holds, or null.
        /// </summary>
        public Character? Hold(Character? candidate)
        {
            if (!_loaded)
            {
                Load();
            }
            if (_current != null && !Qualifies(_current))
            {
                Set(null);
            }
            if (_current == null && candidate != null && Qualifies(candidate))
            {
                Set(candidate);
            }
            return _current;
        }

        /// <summary>This machine no longer owns the creature: hold nothing, and read the ZDO again if it ever does.</summary>
        public void Release()
        {
            _current = null;
            _loaded = false;
        }

        private bool Qualifies(Character target)
        {
            if (target.IsDead() || target.m_aiSkipTarget || target.InGhostMode() || !_ai.IsEnemy(target))
            {
                return false; // dead, never a target, hidden from AI, or no longer an enemy (tamed, befriended)
            }
            if (target is Player player && player.InDebugFlyMode())
            {
                return false;
            }
            return (target.transform.position - _ai.transform.position).sqrMagnitude <= _rangeSqr;
        }

        private void Load()
        {
            _loaded = true;
            ZDOID id = ReadId();
            GameObject? found = !id.IsNone() && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            _current = found != null ? found.GetComponent<Character>() : null;
            if (_current == null && !id.IsNone())
            {
                WriteId(ZDOID.None); // the quarry is not here any more (logged out, died, unloaded): forget it
            }
        }

        private void Set(Character? quarry)
        {
            _current = quarry;
            WriteId(quarry != null ? quarry.GetZDOID() : ZDOID.None);
        }

        private ZDOID ReadId()
        {
            ZDO? zdo = _view != null && _view.IsValid() ? _view.GetZDO() : null;
            return zdo != null ? zdo.GetZDOID(KeyHash) : ZDOID.None;
        }

        // Written only on a change, so holding the same quarry costs no network traffic.
        private void WriteId(ZDOID id)
        {
            ZDO? zdo = _view != null && _view.IsValid() ? _view.GetZDO() : null;
            if (zdo != null && zdo.GetZDOID(KeyHash) != id)
            {
                zdo.Set(KeyHash, id);
            }
        }
    }
}
