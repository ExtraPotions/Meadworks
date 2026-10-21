// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The giant's frozen sleep. It uses the game's own sleep state (the troll's sleeping pose; the ZDO carries it to
    /// every client), so on every machine a sleeping giant wears its crust of snow and shows no name or health bar: a
    /// snowy outcrop. The owner wakes it at night or in a snowstorm (a hit wakes it any time, the game's way), and lets
    /// it settle back to sleep where it stands once it is day again, clear, and it has had nothing to fight for
    /// <see cref="Settle"/> seconds. Asleep, its body is fixed in place so nobody can shove it about; since a fixed body
    /// does not fall either, the owner sets a sleeping giant down on the ground itself.
    /// </summary>
    public class RimeSlumber : MonoBehaviour, IDisguise
    {
        public const string Blizzard = "SnowStorm";
        private const float Check = 1f;
        private const float Settle = 20f;

        private MonsterAI _ai = null!;
        private Character _character = null!;
        private ZNetView _nview = null!;
        private List<GameObject> _crust = new List<GameObject>();
        private bool? _crustShown;
        private float _checkTimer, _quiet;

        public bool Asleep => _ai != null && _ai.IsSleeping();

        /// <summary>A sleeping giant has no hover name or text (<see cref="DisguiseHoverPatch"/>) and no name plate.</summary>
        public bool Holds => Asleep;

        public string HoverText() => "";

        public string HoverName() => "";

        private void Awake()
        {
            _ai = GetComponent<MonsterAI>();
            _character = GetComponent<Character>();
            _nview = GetComponent<ZNetView>();
            _crust = RimeKit.CrustPieces(transform);
            Disguises.Register(_character, this);
        }

        private void OnDestroy() => Disguises.Unregister(_character);

        private void Update()
        {
            bool asleep = Asleep;
            _character.m_hideHud = asleep;
            ShowCrust(asleep);
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= Check && _nview != null && _nview.IsValid() && _nview.IsOwner() && !_character.IsDead())
            {
                _checkTimer = 0f;
                Decide(asleep);
            }
        }

        // OWNER: wake to the night or the storm; sleep again once the day is clear and quiet.
        private void Decide(bool asleep)
        {
            bool stirring = EnvMan.IsNight() || (EnvMan.instance != null && EnvMan.instance.IsEnvironment(Blizzard));
            if (asleep)
            {
                if (stirring)
                {
                    _ai.Wakeup();
                }
                else
                {
                    Ground();
                }
                return;
            }
            bool busy = stirring || _ai.IsAlerted() || _ai.GetTargetCreature() != null || _character.InAttack();
            _quiet = busy ? 0f : _quiet + Check;
            if (_quiet >= Settle)
            {
                _quiet = 0f;
                _ai.Sleep();
            }
        }

        /// <summary>
        /// OWNER: a sleeping giant left above the ground (spawned in the air, or by hand) set down on whatever solid is
        /// under its feet (a ledge or the terrain; looked for from 2 m up, so never an overhang above it).
        /// </summary>
        private void Ground()
        {
            Vector3 at = transform.position;
            if (!ZoneSystem.instance.GetSolidHeight(at, out float ground, 2))
            {
                ground = ZoneSystem.instance.GetGroundHeight(at);
            }
            if (at.y - ground > 0.05f)
            {
                at.y = ground;
                transform.position = at;
                _character.m_body.position = at;
            }
        }

        private void ShowCrust(bool asleep)
        {
            if (_crustShown == asleep)
            {
                return;
            }
            _crustShown = asleep;
            foreach (GameObject piece in _crust)
            {
                if (piece != null)
                {
                    piece.SetActive(asleep);
                }
            }
        }
    }
}
