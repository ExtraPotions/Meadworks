// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The jump itself, on the owner: the transform and the creature's own rigidbody set together, so the next physics
    /// step starts from the new place without syncing every transform in the scene, and the ZDO's position and rotation
    /// written in the same moment, so the other machines get the new place with this frame's update rather than at the
    /// next ZSyncTransform tick. Its momentum is dropped, so it arrives standing rather than still running the way it was
    /// going; it faces its target; and its AI forgets the path it was following, which would otherwise walk it back round
    /// to where it came from. A client snaps a jump of 5 m or more and eases a shorter one, which
    /// <see cref="BlinkVeil"/> hides.
    /// </summary>
    internal static class BlinkMove
    {
        public static void Jump(Character me, Vector3 dest, Vector3 faceToward)
        {
            Transform body = me.transform;
            body.position = dest;
            Vector3 look = BlinkSpot.Flat(faceToward - dest);
            if (look != Vector3.zero)
            {
                body.rotation = Quaternion.LookRotation(look);
                me.SetLookDir(look);
            }
            Still(me);
            Place(me, body);
            Forget(me.GetBaseAI());
        }

        private static void Still(Character me)
        {
            me.m_currentVel = Vector3.zero; // the smoothed run velocity the next physics step would otherwise re-apply
            me.m_maxAirAltitude = me.transform.position.y; // no landing thud for a fall that never happened
            Rigidbody rigidbody = me.m_body;
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private static void Place(Character me, Transform body)
        {
            if (me.m_body != null)
            {
                me.m_body.position = body.position;
                me.m_body.rotation = body.rotation;
            }
            ZDO? zdo = me.m_nview != null && me.m_nview.IsValid() ? me.m_nview.GetZDO() : null;
            if (zdo != null && zdo.IsOwner())
            {
                zdo.SetPosition(body.position);
                zdo.SetRotation(body.rotation);
            }
        }

        // The AI caches its path for up to 5 s while the target stays put; drop it so the next step plans from here.
        private static void Forget(BaseAI ai)
        {
            if (ai != null)
            {
                ai.m_path.Clear();
                ai.m_lastFindPathTime = -1000f;
            }
        }
    }
}
