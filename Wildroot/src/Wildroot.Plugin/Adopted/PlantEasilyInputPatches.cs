// Adapted from PlantEasily 2.3.0 Patches/InputPatches.cs, commit
// 864defe2f471f3c10e0ddd8237fc8fee74190433. Bytes match 2.2.2 (1deeb396). See PlantEasilyInput.PROVENANCE.md.
// SPDX-License-Identifier: GPL-3.0-only
using HarmonyLib;

namespace ExtraPotions.Wildroot.Runtime {
 [HarmonyPatch]
 static class PlantEasilyInputPatches {
  // Reference rule: a placement ghost and held configured gamepad modifier override native shortcuts.
  // Wildroot's existing controller additionally owns session, master, cultivator and UI availability gates.
  private static bool OverrideGamepadInput=>Plugin.Instance?.GameplayAvailable==true&&Plugin.Instance.Controls?.OverrideGamepadInput==true;

  [HarmonyPatch(typeof(HotkeyBar),"Update")]
  [HarmonyPatch(typeof(Player),"StartGuardianPower")]
  static bool Prefix()=>!OverrideGamepadInput;
 }
}
