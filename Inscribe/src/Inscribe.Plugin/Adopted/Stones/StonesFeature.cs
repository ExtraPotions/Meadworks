// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Stones
{
    /// <summary>
    /// Entry point of the Stones area, called once from plugin Awake after the rules, settings and words are loaded.
    /// Owns: the InventoryGui click gesture (<see cref="StoneClickPatch"/>), the check pipeline
    /// (<see cref="StonePipeline"/>), refusals and feedback, costs, the confirm gate, every stone verb
    /// (<see cref="StoneVerbs"/>), and the implementation of <c>Rolling.ItemRoller</c>.
    /// <para>
    /// Nothing to set up: the patch class is applied by the plugin's PatchAll, the verbs are a static table, and every
    /// use reads the running rules afresh. Where it runs: only on the client whose inventory holds the stone and the
    /// item; a dedicated server never sees a stone being used (the item state travels in custom data).
    /// </para>
    /// </summary>
    public static class StonesFeature
    {
        public static void Init()
        {
        }
    }
}
