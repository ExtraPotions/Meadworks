// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/FloatingText.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Draws short words in the world with the game's own floating damage text (DamageText.AddInworldText, local
    /// only), for the callouts of every module (<see cref="WoodCallout"/>, <see cref="MineCallout"/>). Runs on the client
    /// that shows the text: nothing is drawn without a camera (a dedicated server), while the HUD is hidden, or when
    /// the spot is farther than <see cref="Range"/> from the camera. Each caller checks its own "Show Callouts" first.
    /// </summary>
    public static class FloatingText
    {
        /// <summary>How far away a callout can be seen, in metres.</summary>
        public const float Range = 40f;

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/> on this client, if a camera is near it.</summary>
        public static void Show(Vector3 position, string text)
        {
            Camera camera = Utils.GetMainCamera();
            if (camera == null || DamageText.instance == null || Hud.IsUserHidden() || string.IsNullOrEmpty(text))
                return;
            float distance = Vector3.Distance(camera.transform.position, position);
            if (distance <= Range)
                DamageText.instance.AddInworldText(DamageText.TextType.Bonus, position, distance, text, false);
        }
    }
}
