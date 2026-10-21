// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaPatches.cs).
// Changed by expDARE, 2026: Bestiary namespace; no foot steering or facing patch (the holder stands where they took hold).
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// The holder's input while on a Bone Ballista, through the game's doodad control (which would otherwise let go on
    /// attack, as a ship's helm does): attack shoots or says there is nothing to shoot, the other buttons that would let
    /// go or swing do nothing (jump and dodge still let go). Local player only, from PlayerController.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    public static class BallistaControls
    {
        private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack,
            ref bool secondaryAttackHold, ref bool block, ref bool blockHold)
        {
            if (__instance.GetDoodadController() is not BallistaControl control)
            {
                return;
            }
            if (attack)
            {
                SafeCall.Run("bone ballista trigger", static (c, p) => BallistaOperator.Trigger(c, p), control, __instance);
            }
            (attack, attackHold, secondaryAttack, secondaryAttackHold, block, blockHold) = (false, false, false, false, false, false);
        }
    }

    /// <summary>
    /// The holder's view stays within the ballista's own turn and moves at its pace: no further than 45 degrees either
    /// side of where the ballista was placed, no further up than it tilts nor down than 30 degrees, and sideways no faster
    /// than it turns (<see cref="BallistaOperator.YawRate"/>). Local player only, after each mouse movement.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
    public static class BallistaLookLimit
    {
        private const float LookUp = 25f, LookDown = 30f;

        private static BallistaControl? held;
        private static float yaw;

        private static void Postfix(Player __instance)
        {
            if (__instance.GetDoodadController() is not BallistaControl control || !control.IsValid())
            {
                held = null;
                return;
            }
            float placed = control.transform.eulerAngles.y;
            if (control != held)
            {
                (held, yaw) = (control, control.Yaw);   // the view starts where the ballista points
            }
            float wanted = Mathf.Clamp(Mathf.DeltaAngle(placed, __instance.m_lookYaw.eulerAngles.y), -BallistaOperator.YawLimit, BallistaOperator.YawLimit);
            yaw = Mathf.MoveTowards(yaw, wanted, BallistaOperator.YawRate * Time.deltaTime);
            __instance.m_lookYaw = Quaternion.Euler(0f, placed + yaw, 0f);
            __instance.m_lookPitch = Mathf.Clamp(__instance.m_lookPitch, -LookUp, LookDown);
            __instance.UpdateEyeRotation();
            __instance.m_lookDir = __instance.m_eye.forward;
        }
    }
}
