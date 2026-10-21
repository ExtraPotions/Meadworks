using System.IO;
using UnityEditor;
using UnityEngine;
using Workshop.Slinger;

namespace Workshop.Crossbow
{
    /// <summary>
    /// The preview video's frames: the skeleton idles, gets the attack_bow trigger and runs through the game's own shot
    /// states (<see cref="XbowGameAnimator"/>), stepped at 30 frames a second, the string and bolts following the state
    /// as the mod's rig reads it; the bolt flies off the muzzle at the shot.
    /// </summary>
    public static class XbowFrames
    {
        private const float Fps = 30f;
        private const float Lead = 0.5f, Length = 6.4f;
        private static readonly int AttackState = Animator.StringToHash("attack_bow");

        public static void Render(string folder, GameObject skeleton, XbowRigPreview rig)
        {
            Directory.CreateDirectory(folder);
            Animator animator = XbowReference.Animator(skeleton);
            animator.runtimeAnimatorController = XbowGameAnimator.Build(Clip(XbowClips.AimName), Clip(XbowClips.FireName));
            animator.Rebind();
            animator.Update(0f);
            Camera camera = Camera.main;
            SlingerStage.Aim(camera, new Vector3(2.8f, 1.5f, 3.4f), new Vector3(0f, 1.1f, 0.6f));
            var flight = new XbowFlight(XbowReference.Bone(skeleton, "ecp_xbow_muzzle"));
            int frames = Mathf.RoundToInt(Length * Fps);
            for (int frame = 0; frame < frames; frame++)
            {
                if (Mathf.Approximately(frame, Lead * Fps))
                    animator.SetTrigger(XbowGameAnimator.Trigger);
                animator.Update(1f / Fps);
                float time = FireTime(animator);
                rig.Update(time);
                flight.Update(time);
                SlingerStage.Shoot(folder, $"frame_{frame:0000}", camera, 960, 540);
            }
            Log.Info($"video frames: {frames}");
        }

        /// <summary>Seconds into the fire clip, as the mod's rig reads the animator; below zero outside attack_bow.</summary>
        public static float FireTime(Animator animator)
        {
            AnimatorStateInfo state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == AttackState ? Mathf.Clamp01(state.normalizedTime) * XbowClips.FireLength : -1f;
        }

        private static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(XbowBuild.ClipPath(name));
    }
}
