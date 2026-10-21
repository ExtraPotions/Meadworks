// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Windstep (<c>air_jump</c>): one more jump while in the air, given back when the player touches the ground. Runs
    /// on the local player's own client, which moves its player: the game sends the position to everyone and the jump
    /// animation trigger to everyone (ZSyncAnimation), so other players see the jump with nothing of ours sent.
    /// <para>
    /// The air jump is the game's ground jump done in the air: the same upward speed (jump skill, then the status
    /// effects' jump modifiers such as Spring-Heeled), the forward push, the stamina cost and effects of ForceJump. It is
    /// refused when the game would refuse a jump (dead, encumbered, dodging, knocked back, staggered, attacking), while
    /// swimming, attached, debug flying or on a grappling hook, and without the stamina for it. It arrests the fall:
    /// fall damage counts from the height of the air jump (judgement call; the fall it stops is not a fall any more).
    /// </para>
    /// </summary>
    internal static class AirJump
    {
        /// <summary>The air jump of this flight is spent; cleared on ground contact.</summary>
        public static bool Used;

        /// <summary>Character.Jump prefix. False: the air jump was made and the game's ground-only jump is skipped.</summary>
        public static bool TryJump(Character character)
        {
            if (!(character is Player player) || !ReferenceEquals(player, Player.m_localPlayer))
            {
                return true;
            }
            if (player.IsOnGround())
            {
                Used = false;
                return true;
            }
            if (Used || AggregateHost.Current[EffectKind.AirJump] <= 0f || !Airborne(player) || !Free(player)
                || !HasJumpStamina(player))
            {
                return true;
            }
            Used = true;
            Launch(player);
            return false;
        }

        /// <summary>Character.UpdateGroundContact postfix: a touch of the ground gives the air jump back.</summary>
        public static void OnGroundContact(Character character)
        {
            if (Used && character.m_lastGroundTouch <= 0f && ReferenceEquals(character, Player.m_localPlayer))
            {
                Used = false;
            }
        }

        private static bool Airborne(Player player) =>
            !player.InLiquidSwimDepth() && !player.IsAttached() && !player.IsDebugFlying() && GrapplingPoint.m_localGrappler == null;

        // The game's own refusals at the top of Character.Jump, plus "not attacking" (a forced jump is never ours).
        private static bool Free(Player player) =>
            !player.IsDead() && !player.IsEncumbered() && !player.InDodge() && !player.IsKnockedBack()
            && !player.IsStaggering() && !player.InAttack();

        // Enough stamina for a jump; otherwise the bar flashes as for a tired ground jump, and nothing happens.
        private static bool HasJumpStamina(Player player)
        {
            if (player.HaveStamina(player.m_jumpStaminaUsage))
            {
                return true;
            }
            if (Hud.instance != null)
            {
                Hud.instance.StaminaBarEmptyFlash();
            }
            return false;
        }

        // Character.Jump on flat ground: the up speed is at least the jump force, the move direction adds the forward
        // force, both scaled by the jump skill; the status effects modify the result; ForceJump plays it (stamina in OnJump).
        private static void Launch(Player player)
        {
            float lift = 1f + player.GetSkillFactor(Skills.SkillType.Jump) * 0.4f;
            Vector3 velocity = player.m_body.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, player.m_jumpForce * lift);
            velocity += player.m_moveDir * (player.m_jumpForceForward * lift);
            player.GetSEMan().ApplyStatusEffectJumpMods(ref velocity);
            player.RaiseSkill(Skills.SkillType.Jump);
            player.m_maxAirAltitude = player.transform.position.y;
            player.ForceJump(velocity);
        }
    }

    [HarmonyPatch]
    internal static class AirJumpPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
        private static bool Jump(Character __instance) => AirJump.TryJump(__instance);

        // Runs for every character a peer owns, every physics step: one static bool read when no air jump is spent.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.UpdateGroundContact))]
        private static void GroundContact(Character __instance) => AirJump.OnGroundContact(__instance);
    }
}
