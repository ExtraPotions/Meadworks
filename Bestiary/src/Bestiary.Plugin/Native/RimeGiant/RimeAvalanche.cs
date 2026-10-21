// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections;
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Adapters;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// The avalanche the giant's slam sends rolling towards its target (straight ahead when it has none). The owner
    /// traces the wave's path over the ground (<see cref="RimeWavePath"/>), sends it to every machine to draw, and
    /// rolls it: each step hits everyone hostile to the giant within <see cref="Reach"/> metres once per wave, for
    /// `Avalanche Damage` blunt and half as much frost, knocking them on in the direction it rolls - downhill, since
    /// that is where it runs. It can be dodged and blocked like any hit.
    /// </summary>
    public class RimeAvalanche : MonoBehaviour
    {
        public const string WaveRpc = "ecp_rime_wave";
        private const float StepTime = 0.09f;   // seconds per 2 m step: a wave about as fast as a sprint
        private const float Reach = 2.6f;
        private const float Push = 140f;
        private const float StartAhead = 3f;    // metres in front of its feet, grown with its size

        private Character _character = null!;
        private MonsterAI _ai = null!;
        private ZNetView _nview = null!;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _ai = GetComponent<MonsterAI>();
            _nview = GetComponent<ZNetView>();
            if (_nview != null && _nview.IsValid())
            {
                _nview.Register<ZPackage>(WaveRpc, (sender, package) => Guard.Run("RimeAvalanche.Draw", () => Draw(package)));
            }
        }

        /// <summary>OWNER, as the slam lands.</summary>
        public void Roll()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || RimeGiantSettings.AvalancheLength <= 0f)
            {
                return;
            }
            Vector3 direction = Heading();
            Vector3 start = transform.position + direction * (StartAhead * transform.localScale.x);
            List<Vector3> path = RimeWavePath.Trace(start, direction, RimeGiantSettings.AvalancheLength);
            if (path.Count == 0)
            {
                return;
            }
            _nview.InvokeRPC(ZNetView.Everybody, WaveRpc, Pack(path));
            StartCoroutine(Crush(path, direction));
        }

        private Vector3 Heading()
        {
            Character? target = _ai != null ? _ai.GetTargetCreature() : null;
            Vector3 heading = target != null ? target.transform.position - transform.position : transform.forward;
            heading.y = 0f;
            return heading.sqrMagnitude > 0.01f ? heading.normalized : transform.forward;
        }

        // OWNER: each step hits whoever it rolls over, once each.
        private IEnumerator Crush(List<Vector3> path, Vector3 direction)
        {
            var struck = new HashSet<Character>();
            foreach (Vector3 point in path)
            {
                if (_character == null || _character.IsDead())
                {
                    yield break;
                }
                foreach (Character victim in Caught(point, struck))
                {
                    victim.Damage(Hit(victim, direction));
                }
                yield return new WaitForSeconds(StepTime);
            }
        }

        private List<Character> Caught(Vector3 point, HashSet<Character> struck)
        {
            var caught = new List<Character>();
            foreach (Character other in Character.GetAllCharacters())
            {
                Vector3 offset = other.transform.position - point;
                bool near = new Vector2(offset.x, offset.z).magnitude <= Reach && Mathf.Abs(offset.y) <= Reach * 1.5f;
                if (near && other != _character && !other.IsDead() && BaseAI.IsEnemy(_character, other) && struck.Add(other))
                {
                    caught.Add(other);
                }
            }
            return caught;
        }

        private HitData Hit(Character victim, Vector3 direction)
        {
            float damage = RimeGiantSettings.AvalancheDamage;
            var hit = new HitData
            {
                m_point = victim.GetCenterPoint(),
                m_dir = direction,
                m_pushForce = Push,
                m_dodgeable = true,
                m_blockable = true,
                m_hitType = HitData.HitType.EnemyHit,
            };
            hit.m_damage.m_blunt = damage;
            hit.m_damage.m_frost = damage * 0.5f;
            hit.SetAttacker(_character);
            return hit;
        }

        private static ZPackage Pack(List<Vector3> path)
        {
            var package = new ZPackage();
            package.Write(path.Count);
            path.ForEach(package.Write);
            return package;
        }

        // EVERY MACHINE: the wave drawn step by step along the owner's path.
        private void Draw(ZPackage package)
        {
            var path = new List<Vector3>();
            for (int count = package.ReadInt(), i = 0; i < count; i++)
            {
                path.Add(package.ReadVector3());
            }
            StartCoroutine(Steps(path));
        }

        private static IEnumerator Steps(List<Vector3> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                RimeEffects.Wave(path[i], i);
                yield return new WaitForSeconds(StepTime);
            }
        }
    }
}
