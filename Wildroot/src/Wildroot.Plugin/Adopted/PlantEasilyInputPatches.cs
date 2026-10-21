// Adapted from PlantEasily 2.2.2 Patches/InputPatches.cs, commit
// 1deeb3962eb8681394961b0f35a4dfbc315c6f99. See PlantEasilyInput.PROVENANCE.md in this directory.
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
