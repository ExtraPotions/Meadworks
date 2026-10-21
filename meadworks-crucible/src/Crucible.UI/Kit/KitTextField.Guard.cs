using System;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>KX01 field registry and typing guard (see <see cref="KitTypingRules"/>), driven by the Crucible.UI plugin.</summary>
    public sealed partial class KitTextField
    {
        /// <summary>Plugin Update: debounced change events; destroyed fields are forgotten.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                KitTextField field = fields[i];
                if (field.Input == null)
                {
                    fields.RemoveAt(i);
                    continue;
                }
                field.Update(now);
            }
        }

        /// <summary>Plugin LateUpdate: whether a field was focused at the end of this frame (it may let go on Escape).</summary>
        internal static void LateTick() => wasTyping = AnyFocused;

        /// <summary>Before <c>InventoryGui.Update</c>: false when the game's update must skip this frame.</summary>
        internal static bool BeforeInventoryUpdate()
        {
            bool padB = ZInput.GetButtonDown(GamepadMap.ZInputName(PadButton.B));
            bool padY = ZInput.GetButtonDown(GamepadMap.ZInputName(PadButton.Y));
            KitTypingVerdict verdict = KitTypingRules.Decide(AnyFocused, wasTyping, ZInput.GetKeyDown(KeyCode.Escape), padB, padY);
            if (!verdict.Typing)
                return true;
            if (verdict.DropGameKeys)
                foreach (string key in KitTypingRules.GameKeys)
                    ZInput.ResetButtonStatus(key);
            if (verdict.ConsumePad)
                foreach (PadButton button in KitTypingRules.LeavePad)
                    MeadworksInput.PadConsume(button);
            if (verdict.LeaveField)
                foreach (KitTextField field in fields)
                    if (field.Focused)
                        field.Release();
            return !verdict.SkipInventoryUpdate;
        }

        internal static bool ReportsChat
        {
            get
            {
                foreach (KitTextField item in fields)
                    if (KitTypingRules.ReportsChat(item.Focused, item.ReportChat))
                        return true;
                return false;
            }
        }

        private static void Register()
        {
            if (registered)
                return;
            registered = true;
            MeadworksInput.AddTypingWindow(() => Typing);
        }
    }

    /// <summary>The typing guard and the optional chat report (KX01), PatchGuard style: a failing patch leaves the game alone.</summary>
    [HarmonyPatch]
    internal static class KitTextFieldPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "Update")]
        private static bool InventoryUpdate()
        {
            try
            {
                return KitTextField.BeforeInventoryUpdate();
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error("Crucible.UI: typing guard failed: " + e);
                return true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
        private static void ChatFocus(ref bool __result)
        {
            try
            {
                if (!__result && KitTextField.ReportsChat)
                    __result = true;
            }
            catch (Exception e)
            {
                CrucibleUiPlugin.Log?.Error("Crucible.UI: chat focus report failed: " + e);
            }
        }
    }
}
