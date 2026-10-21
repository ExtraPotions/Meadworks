// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The three effects that are a field the game reads directly, written on rebuild instead of patched
    /// (affixes.md section 6): pickup radius (<c>Player.m_autoPickupRange</c>), build and repair range
    /// (<c>Player.m_maxPlaceDistance</c>, fed only while the tool is in hand because only held tools count) and map
    /// reveal radius (<c>Minimap.m_exploreRadius</c>). Each keeps the base value it found on that object before our
    /// first write and writes <c>base Ã— (1 + total)</c>, so a rebuild with nothing equipped restores the base.
    /// Local player and the local minimap only: all three are read only on the player's own client.
    /// </summary>
    internal static class FieldWrites
    {
        private static Player? _player;
        private static float _basePickup;
        private static float _basePlace;
        private static Minimap? _minimap;
        private static float _baseExplore;

        public static void Apply(Player player, AggregateValues values)
        {
            CaptureBase(player);
            player.m_autoPickupRange = _basePickup * (1f + values[EffectKind.PickupRadius]);
            player.m_maxPlaceDistance = _basePlace * (1f + values[EffectKind.BuildRange]);
            Minimap? minimap = Minimap.instance;
            if (minimap != null)
            {
                CaptureBase(minimap);
                minimap.m_exploreRadius = _baseExplore * (1f + values[EffectKind.ExploreRadius]);
            }
        }

        // A new Player (respawn) or Minimap (new session) starts from its prefab values, so the first sight of an
        // object is its base.
        private static void CaptureBase(Player player)
        {
            if (!ReferenceEquals(player, _player))
            {
                _player = player;
                _basePickup = player.m_autoPickupRange;
                _basePlace = player.m_maxPlaceDistance;
            }
        }

        private static void CaptureBase(Minimap minimap)
        {
            if (!ReferenceEquals(minimap, _minimap))
            {
                _minimap = minimap;
                _baseExplore = minimap.m_exploreRadius;
            }
        }
    }
}
