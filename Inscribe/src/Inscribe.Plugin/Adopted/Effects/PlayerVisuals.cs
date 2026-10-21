// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// What other players see of a player's affixes, drawn by every client from the published stats
    /// (<see cref="PlayerStats"/>): Hearthlight's soft light on the player, and Mistbane's wider mist clearing around
    /// the player's own demister (the mist is drawn per client, so each client widens the demisters of the players it
    /// sees). Once a second, over the loaded players and demisters only; nothing on a headless server. Each player's
    /// light and each demister's owner (or none: a placed demister) and first range are remembered in weak tables, so a
    /// tick does no search.
    /// </summary>
    internal static class PlayerVisuals
    {
        private const float Interval = 1f;
        private const string LightName = "ECF_Hearthlight";

        /// <summary>Judgement calls: a warm, shadowless light a little above the head, 6 m reach.</summary>
        private static readonly Color LightColor = new Color(1f, 0.82f, 0.55f);

        private static readonly ConditionalWeakTable<Player, LightRef> Lights = new ConditionalWeakTable<Player, LightRef>();
        private static readonly ConditionalWeakTable<Demister, DemisterRef> Demisters = new ConditionalWeakTable<Demister, DemisterRef>();
        private static readonly ConditionalWeakTable<Player, LightRef>.CreateValueCallback FindLight = Find;
        private static readonly ConditionalWeakTable<Demister, DemisterRef>.CreateValueCallback ResolveDemister = Resolve;
        private static float _next;
        private static bool? _headless;

        private sealed class LightRef
        {
            public GameObject? Light;
        }

        private sealed class DemisterRef
        {
            public Player? Owner;
            public float BaseRange;
        }

        public static void Tick()
        {
            if (Time.time < _next || Headless)
            {
                return;
            }
            _next = Time.time + Interval;
            foreach (Player player in Player.GetAllPlayers())
            {
                UpdateLight(player, PlayerStats.Of(player, PlayerStats.Light) > 0f);
            }
            UpdateDemisters();
        }

        private static bool Headless => _headless ??= SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        private static void UpdateLight(Player player, bool on)
        {
            LightRef known = Lights.GetValue(player, FindLight);
            GameObject? light = known.Light;
            if (light != null)
            {
                if (light.activeSelf != on)
                {
                    light.SetActive(on);
                }
                return;
            }
            if (on)
            {
                known.Light = CreateLight(player.transform);
            }
        }

        // First sight of a player: a light made earlier under it (none on a new player object).
        private static LightRef Find(Player player)
        {
            Transform? existing = player.transform.Find(LightName);
            return new LightRef { Light = existing != null ? existing.gameObject : null };
        }

        private static GameObject CreateLight(Transform parent)
        {
            GameObject holder = new GameObject(LightName);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.1f;
            light.color = LightColor;
            light.shadows = LightShadows.None;
            return holder;
        }

        // Each demister under a player gets that player's published radius bonus over the range it had when first seen.
        private static void UpdateDemisters()
        {
            foreach (Demister demister in Demister.GetDemisters())
            {
                if (demister == null || demister.m_forceField == null)
                {
                    continue;
                }
                DemisterRef known = Demisters.GetValue(demister, ResolveDemister);
                if (known.Owner != null)
                {
                    Scale(demister, known.BaseRange * (1f + PlayerStats.Of(known.Owner, PlayerStats.Demist)));
                }
            }
        }

        // First sight of a demister: the player it hangs under (a worn Wisplight), or none (a placed one), and its range.
        private static DemisterRef Resolve(Demister demister) =>
            new DemisterRef { Owner = demister.GetComponentInParent<Player>(), BaseRange = demister.m_forceField.endRange };

        private static void Scale(Demister demister, float range)
        {
            if (demister.m_forceField.endRange != range)
            {
                demister.m_forceField.endRange = range;
            }
        }
    }
}
