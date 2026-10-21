// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Honing and Tempering Runes wear tinted copies of existing rune art (user decision 2026-10-19).
using UnityEngine;

namespace ExtraPotions.Inscribe.Runtime
{
    internal static class TintedRuneArt
    {
        /// <summary>The prefab whose original art a rune wears: Honing the Ascension Rune's, the legacy Tempering Rune (until it converts) the Recasting Rune's.</summary>
        public static string ArtPrefab(string prefab) => prefab switch
        {
            "ECF_Honing" => "ECF_Ascension",
            "ECF_Tempering" => "ECF_Recasting",
            _ => prefab,
        };

        /// <summary>Multiplies the copy's own material instances by the tint; the shared original model is untouched.</summary>
        public static void TintModel(GameObject copy, Color tint)
        {
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;   // instances owned by this copy
                foreach (Material material in materials)
                {
                    if (material.HasProperty("_Color")) material.SetColor("_Color", material.GetColor("_Color") * tint);
                    if (material.HasProperty("_EmissionColor") && material.IsKeywordEnabled("_EMISSION"))
                        material.SetColor("_EmissionColor", material.GetColor("_EmissionColor") * tint);
                }
                renderer.materials = materials;
            }
        }
    }
}
