// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The rime armour's state and rules. How many plates are on is kept in the creature's ZDO, so every machine reads
    /// the same number and draws it (<see cref="RimeArmourLook"/>); only the owner changes it. The plates cut the
    /// physical part of each hit, the more of them the more: all on, only `Armored Damage` gets through, and each one
    /// lost lets an equal part more through, down to the whole hit with none left. Fire breaks them: every
    /// `Fire Per Plate` of fire the giant takes (the game turns all fire into burning, so arrows, torches and burning
    /// all count) or of heat it stands in knocks one off. At `Shatter At` of its health whatever is left breaks off at
    /// once, and the last plate's fall always staggers it. They never grow back in a fight: only once it has no target,
    /// is no longer alerted and has not been hit for `Regrow Delay` seconds, and only while its health is above
    /// `Shatter At`, one every `Regrow Interval`.
    /// </summary>
    public class RimeArmour : MonoBehaviour
    {
        private static readonly int PlatesKey = "ecp_rime_plates".GetStableHashCode();
        private const float HeatCheck = 1f;          // seconds between looks for a fire underfoot
        private const float HeatPerCheck = 10f;      // fire counted for each second near one: a plate every 3 s
        private const float HeatReach = 1.5f;        // metres around its feet, grown with its size

        private ZNetView _nview = null!;
        private Character _character = null!;
        private MonsterAI _ai = null!;
        private float _fire;                         // OWNER: fire taken towards the next plate
        private float _lastHit = float.NegativeInfinity;
        private float _lastGrowth;
        private float _heatTimer;

        /// <summary>Plates the settings allow (0 to 8).</summary>
        public static int MaxPlates => Mathf.Clamp(RimeGiantSettings.Plates, 0, 8);

        /// <summary>Plates on now, on every machine.</summary>
        public int Plates
        {
            get
            {
                ZDO? zdo = _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
                return Mathf.Clamp(zdo?.GetInt(PlatesKey, MaxPlates) ?? MaxPlates, 0, MaxPlates);
            }
        }

        /// <summary>The share of a physical hit that gets through the plates on now.</summary>
        public float Through
        {
            get
            {
                int max = MaxPlates;
                float blocked = 1f - RimeGiantSettings.ArmouredDamage;
                return max <= 0 ? 1f : 1f - blocked * Plates / max;
            }
        }

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            _ai = GetComponent<MonsterAI>();
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character.IsDead())
            {
                return;
            }
            FeelHeat(Time.deltaTime);
            if (Plates > 0 && Wounded)
            {
                Shatter();
            }
            Regrow();
        }

        /// <summary>OWNER, as a hit lands (before resistances): the physical part cut by the plates on now.</summary>
        public void Soften(HitData hit)
        {
            _lastHit = Time.time;
            float through = Through;
            hit.m_damage.m_blunt *= through;
            hit.m_damage.m_slash *= through;
            hit.m_damage.m_pierce *= through;
            hit.m_damage.m_chop *= through;
            hit.m_damage.m_pickaxe *= through;
        }

        /// <summary>OWNER: fire taken, towards breaking the next plate.</summary>
        public void Melt(float fire)
        {
            if (fire <= 0f || !_nview.IsOwner())
            {
                return;
            }
            _lastHit = Time.time;
            _fire += fire;
            float perPlate = RimeGiantSettings.FirePerPlate;
            while (_fire >= perPlate && Plates > 0)
            {
                _fire -= perPlate;
                Set(Plates - 1);
                if (Plates == 0)
                {
                    Shatter();
                }
            }
        }

        /// <summary>Whatever plates are left break off, and the last one's fall rocks it back.</summary>
        private void Shatter()
        {
            Set(0);
            _fire = 0f;
            _character.Stagger(-transform.forward);
        }

        private bool Wounded => _character.GetHealthPercentage() <= RimeGiantSettings.ShatterAt;

        /// <summary>A target, an alert, or a hit within `Regrow Delay` seconds: the fight is on.</summary>
        private bool Fighting =>
            (_ai != null && (_ai.IsAlerted() || _ai.GetTargetCreature() != null))
            || Time.time - _lastHit < RimeGiantSettings.RegrowDelay;

        private void FeelHeat(float dt)
        {
            _heatTimer += dt;
            if (_heatTimer < HeatCheck)
            {
                return;
            }
            _heatTimer = 0f;
            if (EffectArea.IsPointInsideArea(transform.position, EffectArea.Type.Heat, HeatReach * transform.localScale.x) != null)
            {
                Melt(HeatPerCheck);
            }
        }

        private void Regrow()
        {
            if (Plates >= MaxPlates || Fighting || Wounded || Time.time - _lastGrowth < RimeGiantSettings.RegrowInterval)
            {
                return;
            }
            _lastGrowth = Time.time;
            _fire = 0f;
            Set(Plates + 1);
        }

        private void Set(int plates) => _nview.GetZDO().Set(PlatesKey, Mathf.Clamp(plates, 0, MaxPlates));
    }
}
