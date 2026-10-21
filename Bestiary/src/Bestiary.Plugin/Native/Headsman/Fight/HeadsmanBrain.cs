// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The Executioner's owner's part in its attacks, by the clip's own time: in the rear strike it turns the creature
    /// itself half round as the legs come round under it (0.95 to 1.35 s), the game's AI held off meanwhile so it does
    /// not turn it back towards its target (<see cref="Turning"/>, <see cref="HeadsmanAiPatch"/>); in the slam, the low
    /// sweep and the spin the axe head hits (<see cref="HeadsmanCut"/>); and it plays the slam's wind-up faster
    /// (<see cref="Pace"/>). The turn reaches the other peers as any turn does, through the creature's synced transform,
    /// and the speed through the game's own animation sync (the owner's animator speed is kept in the ZDO and every peer
    /// plays at it).
    /// </summary>
    public sealed class HeadsmanBrain : MonoBehaviour
    {
        private ZNetView nview = null!;
        private Character character = null!;
        private Animator animator = null!;
        private HeadsmanRig rig = null!;
        private HeadsmanCut cut = null!;
        private HeadsmanMove? current;
        private float turned;
        private float? paced;

        /// <summary>Whether the rear strike is playing: the AI waits it out.</summary>
        public bool Turning => current == HeadsmanMoves.Rear;

        private void Awake()
        {
            (nview, character, rig) = (GetComponent<ZNetView>(), GetComponent<Character>(), GetComponent<HeadsmanRig>());
            animator = GetComponentInChildren<Animator>();
            cut = new HeadsmanCut(GetComponent<Humanoid>());
            HeadsmanAiPatch.Watch(GetComponent<MonsterAI>(), () => Turning);
        }

        private void OnDestroy() => HeadsmanAiPatch.Forget(GetComponent<MonsterAI>());

        private void Update()
        {
            if (nview == null || !nview.IsValid() || !nview.IsOwner() || animator == null || !rig.enabled)
            {
                return;
            }
            var (move, time) = HeadsmanClock.Read(animator);
            if (move != current)
            {
                (current, turned) = (move, 0f);
            }
            if (move == HeadsmanMoves.Rear)
            {
                Turn(time);
            }
            cut.Step(move, time, rig.Edge);
            Pace(move, time);
        }

        /// <summary>
        /// The wind-up at its own speed, on top of the speed the creature had as it began (star scaling's, given back
        /// after it). Set every frame while it lasts: the game puts a creature's speed back to 1 each fixed step while it
        /// is not yet in its attack state, which the blend into the attack is.
        /// </summary>
        private void Pace(HeadsmanMove? move, float time)
        {
            bool winding = move != null && time < move.WindUp;
            if (winding)
            {
                paced ??= animator.speed;
                animator.speed = paced.Value * HeadsmanSettings.SlamWindup;
            }
            else if (paced != null)
            {
                animator.speed = paced.Value;
                paced = null;
            }
        }

        /// <summary>By what the rear strike's own turn has grown since the last frame, and the creature looks that way.</summary>
        private void Turn(float time)
        {
            float want = HeadsmanMoves.RootYaw(time);
            transform.rotation = Quaternion.AngleAxis(want - turned, Vector3.up) * transform.rotation;
            turned = want;
            character.SetLookDir(transform.forward);
        }
    }
}
