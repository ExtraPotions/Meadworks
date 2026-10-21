// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Where a thrown axe broke: the game spawns it from the projectile on the projectile's owner, turned as the axe
    /// flew; networked, so every peer has it, keeping the moment it broke in its ZDO. Every peer flies the axe's own
    /// pieces from there (<see cref="HeadsmanShards"/>). Its owner raises a skeleton on the spot
    /// <see cref="FormAfter"/> seconds on (<see cref="HeadsmanSummon"/>), unless enough are already up, forming over
    /// the same two seconds as the Executioner's new axe, and removes the shatter once it stands.
    /// </summary>
    public sealed class HeadsmanShatter : MonoBehaviour
    {
        public const string Name = "ECP_Headsman_shatter";
        public const float FormAfter = 0.6f;
        private const string HitKey = "ecp_hs_hit", SummonKey = "ecp_hs_summon", RaisedKey = "ecp_hs_raised";

        private ZNetView nview = null!;
        private HeadsmanShards? shards;
        private HeadsmanRising? rising;
        private double hit;

        /// <summary>A networked, unsaved object carrying this component, on the prefab bench.</summary>
        public static GameObject Build()
        {
            var shatter = new GameObject(Name);
            shatter.transform.SetParent(PrefabBench.Root, false);
            ZNetView view = shatter.AddComponent<ZNetView>();
            (view.m_persistent, view.m_distant, view.m_type) = (false, false, ZDO.ObjectType.Default);
            shatter.AddComponent<HeadsmanShatter>();
            return shatter;
        }

        private void Awake()
        {
            nview = GetComponent<ZNetView>();
            if (nview.GetZDO() == null)
            {
                return;
            }
            if (nview.IsOwner() && HeadsmanTime.Read(nview.GetZDO(), HitKey) == 0d)
            {
                HeadsmanTime.Keep(nview.GetZDO(), HitKey, HeadsmanTime.Now);
            }
            hit = HeadsmanTime.Read(nview.GetZDO(), HitKey);
            hit = hit == 0d ? HeadsmanTime.Now : hit;
            shards = new HeadsmanShards(transform, HeadsmanGround.Floor(transform.position), nview.GetZDO().m_uid.GetHashCode());
            if (HeadsmanTime.Now - hit < 0.5d)
            {
                HeadsmanSounds.Play("shatter", transform.position);
            }
        }

        private void Update()
        {
            if (shards == null || !nview.IsValid())
            {
                return;
            }
            float since = (float)(HeadsmanTime.Now - hit);
            shards.Step(since, Rising());
            if (nview.IsOwner())
            {
                Own(since);
            }
        }

        private void OnDestroy() => shards?.Clear();

        /// <summary>OWNER: the skeleton raised at its time, the shatter gone once it stands.</summary>
        private void Own(float since)
        {
            if (since >= FormAfter && !nview.GetZDO().GetBool(RaisedKey))
            {
                nview.GetZDO().Set(RaisedKey, true);
                ZDOID raised = HeadsmanSummon.Raise(HeadsmanGround.Floor(transform.position), transform.rotation, hit + FormAfter);
                nview.GetZDO().Set(SummonKey, raised);
            }
            if (since >= FormAfter + HeadsmanMoves.Forming + 1.5f)
            {
                ZNetScene.instance.Destroy(gameObject);
            }
        }

        /// <summary>The skeleton raised here, once it exists on this peer.</summary>
        private HeadsmanRising? Rising()
        {
            if (rising == null)
            {
                ZDOID id = nview.GetZDO().GetZDOID(SummonKey);
                GameObject? raised = id.IsNone() ? null : ZNetScene.instance.FindInstance(id);
                rising = raised != null ? raised.GetComponent<HeadsmanRising>() : null;
            }
            return rising;
        }
    }
}
