// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Timber/Timber.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Timber!: a felled tree falls where the woodcutter aims it, "Timber!" floats above it, and its logs go easy on
    /// the woodcutter who felled it.
    /// <list type="bullet">
    /// <item><b>Fall push</b>, on the tree's owner. TreeBase.SpawnLog instantiates the log at m_logSpawnPoint (halfway up
    /// the trunk, about where the log's centre of mass is), multiplies its Rigidbody mass by the tree's scale.x and
    /// adds the impulse hitDir × 0.2 × mass at the log's position + up × 4 × scale.y (ForceMode.Impulse): a nudge that
    /// tips the top over along the felling hit. <see cref="Felling"/> calls <see cref="OnFelled"/> right after, in the
    /// same frame, and the same impulse is added again times the woodcutter's share, the same way on the same body.
    /// Impulses on a Rigidbody add up until the next physics step, so the log starts with (1 + share) × the game's
    /// push. The log belongs to this machine: its physics run here and ZSyncTransform shows the fall to everybody.</item>
    /// <item><b>Aim.</b> The extra push follows only the horizontal part of the felling hit, at the hit's full length.
    /// A swing's direction runs from the attack's origin at chest height to the point hit on the bark
    /// (Attack.DoMeleeAttack), so it tilts with the slope and the chopping height, and a tilt would only lift the log
    /// or press it into the stump. Flattened, all of it tips the trunk away from the woodcutter. For a tree knocked over
    /// by another log the direction is the striking log's motion at the contact (ImpactEffect), mostly downward;
    /// flattened, the chain carries on the way the first tree fell.</item>
    /// <item><b>Size.</b> The impulse is proportional to the mass, so the speed it gives is the same for every log:
    /// 0.2 m/s along the ground per game's push, plus a turn. At the default (×10 at level 100) the top of a pine log
    /// starts at about 8 m/s, below the 20 m/s or so it reaches when a log lands on its own (the game's gravity is
    /// 20 m/s²). The largest setting (×31) gives about 6 m/s and 25 m/s: rough, not a fling, and the game bounds the
    /// rest: 7 rad/s maximum angular speed, TreeLog.Awake's maxDepenetrationVelocity of 1, and ImpactEffect's damage
    /// reaching its full value at m_maxVelocity (5 m/s), so a harder push never makes a log hit harder. The only
    /// clamps are the setting's range and the level's.</item>
    /// <item><b>Callout.</b> "Timber!" floats a few metres up the trunk (<see cref="WoodCallout"/>) for every tree that
    /// falls to a woodcutter, chain fells included.</item>
    /// <item><b>Log safety</b>, on the log's owner; see <see cref="Struck"/>.</item>
    /// </list>
    /// </summary>
    public static class Timber
    {
        /// <summary>What floats above a tree that falls to a woodcutter.</summary>
        public static string Callout => SagaText.Packet("gs_9365cf95e77bfbbe");

        /// <summary>How high above the foot of the tree the callout floats, in metres for a tree of scale 1.</summary>
        private const float CalloutHeight = 3f;

        /// <summary>The game's push in TreeBase.SpawnLog: hitDir × this × the log's mass, as an impulse.</summary>
        private const float GamePush = 0.2f;

        /// <summary>Where the game's push lands: this many metres (times the tree's scale.y) above the log's position.</summary>
        private const float GameLever = 4f;

        /// <summary>A felling hit whose horizontal part is shorter than this share of it points straight up or down: no aim.</summary>
        private const float MinHorizontal = 0.1f;

        /// <summary>Called by <see cref="Felling"/> on the tree's owner right after the log spawned.</summary>
        public static void OnFelled(FellContext fell)
        {
            if (fell == null || fell.Woodcutter == null)
                return;
            WoodCallout.Broadcast(fell.Position + Vector3.up * (CalloutHeight * fell.Scale.y), Callout);
            if (fell.Log != null)
                Push(fell);
        }

        /// <summary>Adds the game's own push again, times the woodcutter's share, along the flattened felling hit.</summary>
        private static void Push(FellContext fell)
        {
            Rigidbody body = fell.Log.GetComponent<Rigidbody>();
            Vector3 aim = Aim(fell.HitDir);
            float share = Share(TimberSettings.FallPushAt100, TimberSettings.MaxFallPush, fell.Woodcutter);
            if (body == null || share <= 0f || aim == Vector3.zero)
                return;
            Vector3 point = fell.Log.transform.position + Vector3.up * (GameLever * fell.Scale.y);
            body.AddForceAtPosition(aim * (GamePush * body.mass * share), point, ForceMode.Impulse);
        }

        /// <summary>The horizontal part of a felling direction at the direction's own length; zero when there is none.</summary>
        private static Vector3 Aim(Vector3 hitDir)
        {
            float length = hitDir.magnitude;
            Vector3 flat = new Vector3(hitDir.x, 0f, hitDir.z);
            return length > 0f && flat.magnitude >= MinHorizontal * length ? flat.normalized * length : Vector3.zero;
        }

        /// <summary>
        /// Log safety: logs of your own trees hurt you less. ImpactEffect.OnCollisionEnter runs on the log's owner
        /// inside an <see cref="ImpactScope"/> and hands a struck character a hit with no attacker through
        /// IDestructible.Damage, that is Character.Damage (Humanoid and Player do not override it), which sends the hit
        /// by RPC to the character's owner. This prefix scales the hit's damage down before it is sent, when the struck
        /// character is the player who felled the log (Player.GetPlayerID reads the player's ZDO, so it works for a
        /// player whose client is another machine). Stagger follows the damage. The hit keeps no attacker: a player
        /// attacker would make Character.RPC_Damage treat it as PvP (dropped with PvP off) and count it as a player
        /// hit. Log hits carry HitData.HitType.Tree, not Impact (the logs' ImpactEffect.m_hitType), so the scope and
        /// the missing attacker identify them. Other players, creatures and buildings take the game's own damage.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static class Struck
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, HitData hit)
            {
                if (ImpactScope.Current != null)
                    HookGuard.Run("log safety", static struck => Soften(struck.character, struck.hit), (character: __instance, hit));
            }
        }

        /// <summary>Scales a felled log's hit on its own woodcutter down by the woodcutter's share of Log Safety.</summary>
        private static void Soften(Character struck, HitData hit)
        {
            Woodcutter woodcutter = ImpactScope.Current;
            if (woodcutter == null || hit == null || hit.HaveAttacker() || !WoodSkill.Active || woodcutter.PlayerId == 0L)
                return;
            if (!(struck is Player player) || player.GetPlayerID() != woodcutter.PlayerId)
                return;
            float share = Share(TimberSettings.LogSafetyAt100, TimberSettings.MaxLogSafety, woodcutter);
            if (share > 0f)
                hit.m_damage.Modify(Mathf.Clamp01(1f - share));
        }

        /// <summary>A woodcutter's share of a setting given at level 100, the setting held to its own range.</summary>
        private static float Share(NativeSetting<float> atLevel100, float max, Woodcutter woodcutter) =>
            WoodSkill.Share(Mathf.Min(atLevel100.Value, max), woodcutter.Level);
    }
}
