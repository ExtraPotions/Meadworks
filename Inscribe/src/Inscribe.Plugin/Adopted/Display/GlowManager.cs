// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Config;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The ground glow's single manager (display.md section 5). Every <c>Glow refresh seconds</c> it walks the game's
    /// live <c>ItemDrop</c> list, reloads an item's data only when its saved item bytes changed (<see cref="GlowItem.Reload"/>,
    /// never per tick for a drop that only moves), decides
    /// glow and color only for new or changed items, and lights the nearest <c>Glow max lights</c> glowing items to the
    /// camera, with their loot beams when <c>Loot beam</c> is on; the rest keep a disabled light and beam. No per-item Update, no per-frame work besides one timer compare.
    /// Lives on every client with graphics, never on a headless server; purely local, nothing is sent.
    /// </summary>
    internal sealed class GlowManager : MonoBehaviour
    {
        /// <summary>A burst of new items (a loot explosion, a zone loading) costs one tick, this soon after the first.</summary>
        private const float BurstDelay = 0.1f;

        private static readonly System.Comparison<GlowItem> Nearest = (a, b) => a.SqrDistance.CompareTo(b.SqrDistance);

        private readonly Dictionary<int, GlowItem> _items = new Dictionary<int, GlowItem>();
        private readonly List<GlowItem> _candidates = new List<GlowItem>();
        private readonly List<int> _gone = new List<int>();
        private float _nextTick;
        private int _stamp;

        public static GlowManager? Instance { get; private set; }

        /// <summary>Creates the manager on a client with graphics. Plugin Awake, via <c>DisplayFeature.Init</c>.</summary>
        public static void Create()
        {
            if (Instance != null || GlowLights.Headless)
            {
                return;
            }
            GameObject host = new GameObject("ecf_glow_manager");
            global::ExtraPotions.Inscribe.Runtime.NativeGameplay.Own(host);
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<GlowManager>();
        }

        /// <summary>Re-evaluate soon instead of at the next timer tick (new item, setting or rules change).</summary>
        public void RequestTick() => _nextTick = Mathf.Min(_nextTick, Time.time + BurstDelay);

        private void Update()
        {
            if (Time.time < _nextTick)
            {
                return;
            }
            _nextTick = Time.time + ModSettings.GlowRefreshSeconds.Value;
            if (!ModSettings.GroundGlow.Value || ModSettings.GlowMaxLights.Value <= 0)
            {
                Clear();
                return;
            }
            if (TryViewPoint(out Vector3 view))
            {
                Tick(view);
            }
        }

        private void Tick(Vector3 view)
        {
            _stamp++;
            _candidates.Clear();
            Collect(view);
            _candidates.Sort(Nearest);
            int cap = ModSettings.GlowMaxLights.Value;
            float intensity = ModSettings.GlowIntensity.Value;
            float range = ModSettings.GlowRange.Value;
            bool beam = ModSettings.LootBeam.Value;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (i < cap)
                {
                    _candidates[i].Shine(intensity, range, beam);
                }
                else
                {
                    _candidates[i].Dim();
                }
            }
            Prune();
        }

        private void Collect(Vector3 view)
        {
            List<ItemDrop> drops = ItemDrop.s_instances;
            for (int i = 0; i < drops.Count; i++)
            {
                ItemDrop drop = drops[i];
                if (drop == null || drop.m_nview == null || !drop.m_nview.IsValid())
                {
                    continue;
                }
                GlowItem item = Track(drop);
                item.Reload(drop.m_nview.GetZDO());
                item.Refresh();
                if (item.Glows)
                {
                    item.SqrDistance = (drop.transform.position - view).sqrMagnitude;
                    _candidates.Add(item);
                }
            }
        }

        private GlowItem Track(ItemDrop drop)
        {
            int id = drop.GetInstanceID();
            if (!_items.TryGetValue(id, out GlowItem item) || item.Drop != drop)
            {
                item = new GlowItem(drop);
                _items[id] = item;
            }
            item.Stamp = _stamp;
            return item;
        }

        /// <summary>Forgets items that left the list (picked up, unloaded); their lights and beams died with them.</summary>
        private void Prune()
        {
            _gone.Clear();
            foreach (KeyValuePair<int, GlowItem> pair in _items)
            {
                if (pair.Value.Stamp != _stamp)
                {
                    _gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                GlowItem item = _items[_gone[i]];
                if (item.Drop != null)
                {
                    item.RemoveGlow();
                }
                _items.Remove(_gone[i]);
            }
        }

        /// <summary>Glow switched off (or a cap of 0): every light and beam is removed and the list forgotten.</summary>
        private void OnDestroy() { Clear(); if (Instance == this) Instance = null; }
        internal void Shutdown() => Clear();
        private void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }
            foreach (GlowItem item in _items.Values)
            {
                if (item.Drop != null)
                {
                    item.RemoveGlow();
                }
            }
            _items.Clear();
            _candidates.Clear();
        }

        /// <summary>The camera, or the local player when no game camera exists; false in menus.</summary>
        private static bool TryViewPoint(out Vector3 view)
        {
            view = default;
            if (GameCamera.instance != null)
            {
                view = GameCamera.instance.transform.position;
                return true;
            }
            if (Player.m_localPlayer != null)
            {
                view = Player.m_localPlayer.transform.position;
                return true;
            }
            return false;
        }
    }
}
