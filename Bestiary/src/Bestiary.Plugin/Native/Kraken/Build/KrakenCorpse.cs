// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// What a dead kraken leaves: a network object of its own (so every machine sees it, made where the kraken died by
    /// its owner as the game makes every corpse) carrying the kraken's model, without hit boxes, that floats up, rolls
    /// onto its side with its tentacles spread limp on the water, then sinks out of sight and is removed
    /// (<see cref="KrakenCorpseMotion"/>). Its loot is not in it: that drops on the deck as it dies.
    /// </summary>
    public static class KrakenCorpse
    {
        public const float Life = 16f;

        public static GameObject Build(AssetBundle bundle, Material skin, int layer)
        {
            GameObject corpse = new GameObject(KrakenPrefabs.Corpse);
            corpse.transform.SetParent(PrefabBench.Root, false);
            var nview = corpse.AddComponent<ZNetView>();
            nview.m_persistent = false;
            nview.m_type = ZDO.ObjectType.Default;
            KrakenModel.Build(corpse.transform, bundle, skin, layer, hitboxes: false);
            corpse.AddComponent<KrakenCorpseMotion>();
            var timer = corpse.AddComponent<TimedDestruction>();
            timer.m_timeout = Life;
            timer.m_triggerOnAwake = true;
            return corpse;
        }
    }

    /// <summary>The corpse on every machine: up to the surface, over on its side, limbs limp on the water, then down and gone.</summary>
    public class KrakenCorpseMotion : MonoBehaviour
    {
        private const float Float = 5f;          // seconds at the surface before it sinks
        private const float Roll = 70f;          // degrees it rolls onto its side
        private const float SinkRate = 0.9f;

        private HeadRig? _head;
        private readonly TentacleMotion?[] _tentacles = new TentacleMotion?[KrakenBody.Tentacles];
        private readonly Vector3[] _pose = TentacleChain.New();
        private Vector3 _start;
        private float _age;

        private void Start()
        {
            _start = transform.position;
            Transform? model = transform.Find(KrakenBody.ModelName);
            Transform? head = model != null ? model.Find(KrakenBody.HeadName) : null;
            _head = head != null ? new HeadRig(head) : null;
            for (int i = 0; i < _tentacles.Length && model != null; i++)
            {
                Transform? arm = model.Find(KrakenBody.TentaclePrefix + i);
                TentacleRig? rig = arm != null ? TentacleRig.Find(arm) : null;
                _tentacles[i] = rig != null ? new TentacleMotion(i, rig) : null;
            }
        }

        private void LateUpdate()
        {
            if (_head == null)
            {
                return;
            }
            _age += Time.deltaTime;
            float water = KrakenShips.Water(_start);
            float y = Mathf.Lerp(_start.y, water - 1.2f, Ease.Out(Ease.Span(_age, 0f, 2f))) - Mathf.Max(0f, _age - Float) * SinkRate;
            Quaternion rolled = transform.rotation * Quaternion.Euler(0f, 0f, Roll * Ease.InOut(Ease.Span(_age, 0.5f, 3f)));
            Vector3 at = new Vector3(_start.x, y, _start.z);
            _head.Apply(new HeadPose { Position = at, Rotation = rolled, Beak = 0.6f, Lean = -10f, Time = _age, Life = 0f });
            Limbs(water - Mathf.Max(0f, _age - Float) * SinkRate);
        }

        private void Limbs(float water)
        {
            for (int i = 0; i < _tentacles.Length; i++)
            {
                Transform joint = _head!.Joint(i);
                Vector3 outward = Vector3.ProjectOnPlane(joint.position - _head.Root.position, Vector3.up);
                var frame = new TentacleFrame(joint.position, outward.sqrMagnitude > 1e-4f ? outward : Vector3.forward, Vector3.up, transform.lossyScale.x);
                TentacleSwim.Limp(frame, (water - joint.position.y) / Mathf.Max(frame.Scale, 1e-4f), _age, i, _pose);
                TentacleChain.Straighten(_pose, TentacleSpec.Segment * frame.Scale);
                _tentacles[i]?.Hold(_pose, frame.Side);
            }
        }
    }
}
