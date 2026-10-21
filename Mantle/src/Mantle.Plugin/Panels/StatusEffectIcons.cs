using HarmonyLib;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// HUD buff and debuff icons (Icon and Cooldown) at half the template's width and height; the name and timer keep their
    /// size. Always set from the template's scale, so it never accumulates. Applied after UpdateStatusEffects and again in
    /// Hud.LateUpdate: each entry carries an Animator (the "flash" trigger) that runs between Update and LateUpdate and wrote
    /// the icon's scale back to full size every frame, so the Update-time scale alone never reached the screen.
    /// </summary>
    [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
    public static class StatusEffectIcons
    {
        [HarmonyPostfix]
        public static void Postfix(Hud __instance) => Apply(__instance, Factor);

        private static float Factor => InventorySettings.Enabled.Value ? 0.5f : 1f;

        [HarmonyPatch(typeof(Hud), "LateUpdate")]
        public static class AfterAnimation
        {
            [HarmonyPostfix]
            public static void Postfix(Hud __instance) => Apply(__instance, Factor);
        }

        public static void Restore() => Apply(Hud.instance, 1f);

        private static void Apply(Hud hud, float factor)
        {
            if (hud == null || hud.m_statusEffectTemplate == null) return;
            foreach (RectTransform effect in hud.m_statusEffects)
            {
                if (effect == null) continue;
                Resize(effect, hud.m_statusEffectTemplate, "Icon", factor);
                Resize(effect, hud.m_statusEffectTemplate, "Cooldown", factor);
            }
        }

        private static void Resize(Transform effect, Transform template, string name, float factor)
        {
            Transform icon = effect.Find(name), baseline = template.Find(name);
            if (icon == null || baseline == null) return;
            Vector3 scale = baseline.localScale, wanted = new Vector3(scale.x * factor, scale.y * factor, scale.z);
            // Twice a frame, so written only when it differs (the Animator's own writes still get overridden).
            if (icon.localScale != wanted) icon.localScale = wanted;
        }
    }
}
