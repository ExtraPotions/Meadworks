// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the catalog runtime (K07b, design 1.1 C). TheGreatestShips 0.9.10 is a behavior reference only
// (README pinned at 8aa408759078a666b1d485cdd28d7a2db5cb5d09: ships cloned from the player's own vanilla hulls, shaped,
// tinted, named on rudder and hold, placed in the hammer after their vanilla ship). No TheGreatestShips code was read.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Keel.Catalog;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Builds the eight Keel ships as clones of the game's own Karve and Longship (nothing derived from Valheim is
    /// shipped), registers them as hammer pieces, and keeps their recipe, shape, hold and paint in step with the catalog
    /// settings: server values and edits apply to the prefab and to every loaded ship of that kind. Speed, health and
    /// turning stay with the tuning entries of each ship (design C11). Main thread only.
    /// </summary>
    public static class KeelCatalog
    {
        /// <summary>All three pen prefabs have their runtime native geometry; physical qualification is separate.</summary>
        public static bool PensReady => built.Values.Count(b => b.Type.HasPen && b.PenReady) == 3;

        private const string Hammer = "Hammer";
        private const string Workbench = "piece_workbench";
        private static readonly string[] VanillaShips = { "Raft", ShipCatalog.Karve, ShipCatalog.Longship, "VikingShip_Ashlands" };

        internal sealed class Built
        {
            public Built(ShipType type, GameObject prefab, float steerForceOffset, float waterLevelOffset)
            {
                Type = type;
                Prefab = prefab;
                SteerForceOffset = steerForceOffset;
                WaterLevelOffset = waterLevelOffset;
            }

            public ShipType Type { get; }
            public GameObject Prefab { get; }
            public float SteerForceOffset { get; }
            public float WaterLevelOffset { get; }
            public CatalogSettings Settings => KeelSettings.Catalog(Type.Ship);
            public string AppliedRecipe { get; set; } = "";
            public string RefusedReason { get; set; } = "";
            public bool PenReady { get; set; }
        }

        private static readonly Dictionary<KeelShip, Built> built = new Dictionary<KeelShip, Built>();
        private static readonly Dictionary<string, Built> byPrefab = new Dictionary<string, Built>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> vanillaSailForce = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> vanillaRudderSpeed = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<ConfigEntryBase, KeelShip> watched = new Dictionary<ConfigEntryBase, KeelShip>();
        private static readonly HashSet<KeelShip> dirty = new HashSet<KeelShip>();
        private static MeadworksLog? log;
        private static ConfigFile? file;
        private static bool created;
        private static bool hammerDirty;
        private static bool? lastActive;
        private static long lastGeneration = -1;

        public static int Count => built.Count;

        /// <summary>The catalog may act now: product active and TheGreatestShips not installed.</summary>
        public static bool Active => KeelModules.Active(KeelModule.Catalog);

        private static bool SteppedAside => KeelModules.StepAside.SteppedAside(KeelModule.Catalog);

        public static void Initialize(MeadworksLog logger)
        {
            log = logger;
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            if (SteppedAside) return;
            PieceManager.OnPiecesRegistered += OnPiecesRegistered;
            var s = Plugin.Settings;
            if (s == null) return;
            file = s.File;
            file.SettingChanged += OnSettingChanged;
            file.ConfigReloaded += OnReloaded;
            SynchronizationManager.OnConfigurationSynchronized += OnSynchronized;
            foreach (var type in ShipCatalog.All)
                foreach (var setting in KeelSettings.Catalog(type.Ship).Entries())
                    watched[s.Entry(setting)] = type.Ship;
        }

        public static void Shutdown()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            PieceManager.OnPiecesRegistered -= OnPiecesRegistered;
            if (file != null)
            {
                file.SettingChanged -= OnSettingChanged;
                file.ConfigReloaded -= OnReloaded;
                SynchronizationManager.OnConfigurationSynchronized -= OnSynchronized;
                file = null;
            }
            watched.Clear();
            dirty.Clear();
            KeelPaint.Dispose();
            lastActive = null;
            lastGeneration = -1;
            log = null;
        }

        /// <summary>The Keel ship built from this prefab name, if any.</summary>
        internal static bool TryGet(string prefab, out Built ship) => byPrefab.TryGetValue(prefab ?? "", out ship!);

        /// <summary>The game's own sail force of a vanilla hull, or of the hull a Keel ship is built from (ship panel).</summary>
        public static bool TryVanillaSailForce(string prefab, out float sailForce)
        {
            if (byPrefab.TryGetValue(prefab ?? "", out var ship)) prefab = ship.Type.BaseHull;
            return vanillaSailForce.TryGetValue(prefab ?? "", out sailForce);
        }
        internal static bool TryVanillaRudderSpeed(string prefab, out float speed) => vanillaRudderSpeed.TryGetValue(prefab, out speed);

        /// <summary>Every frame: re-applies changed ships and refreshes the hammer when the session or settings change.</summary>
        public static void Tick()
        {
            if (!created || SteppedAside) return;
            var active = Active;
            var generation = CruciblePlugin.Session.Generation;
            if (lastActive != active || generation != lastGeneration)
            {
                lastActive = active;
                lastGeneration = generation;
                RequestAll();
            }
            if (dirty.Count > 0)
            {
                foreach (var ship in dirty.ToList())
                    if (built.TryGetValue(ship, out var b)) Guard("apply " + b.Type.Prefab, () => Apply(b, scene: false));
                dirty.Clear();
            }
            if (hammerDirty)
            {
                hammerDirty = false;
                Guard("hammer", RefreshHammer);
            }
        }

        private static void RequestAll()
        {
            foreach (var ship in built.Keys) dirty.Add(ship);
            hammerDirty = true;
        }

        // Creation (first menu start).

        private static void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            foreach (var name in VanillaShips)
            {
                var ship = PrefabManager.Instance.GetPrefab(name)?.GetComponent<Ship>();
                if (ship != null) { vanillaSailForce[name] = ship.m_sailForceFactor; vanillaRudderSpeed[name] = ship.m_rudderSpeed; }
            }
            if (SteppedAside || Plugin.Settings == null) return;
            foreach (var type in ShipCatalog.All) Guard("clone " + type.Prefab, () => Create(type));
            created = built.Count > 0;
            log?.Info("Keel ships built from the game's hulls: " + built.Count + " of " + ShipCatalog.All.Count + ".");
        }

        private static void Create(ShipType type)
        {
            var hull = PrefabManager.Instance.GetPrefab(type.BaseHull);
            var hullShip = hull != null ? hull.GetComponent<Ship>() : null;
            if (hullShip == null || hull!.GetComponent<WearNTear>() == null || hull.GetComponent<Piece>() == null)
            {
                log?.Warning("Keel " + type.Name + ": the game's " + type.BaseHull + " is missing; the ship is not built.");
                return;
            }
            var clone = PrefabManager.Instance.CreateClonedPrefab(type.Prefab, hull);
            var ship = clone.GetComponent<Ship>();
            var b = new Built(type, clone, ship.m_stearForceOffset, ship.m_waterLevelOffset);
            var size = Shape(b);
            // Vanilla-equivalent values of the new ship; the tuning entries read them as this ship's defaults.
            clone.GetComponent<WearNTear>().m_health = type.Health;
            ship.m_sailForceFactor = type.DefaultSailForce(hullShip.m_sailForceFactor);
            ship.m_rudderSpeed = type.DefaultRudderSpeed(hullShip.m_rudderSpeed);
            ship.m_sailForceOffset = hullShip.m_sailForceOffset * size.Y;
            var piece = clone.GetComponent<Piece>();
            piece.m_name = KeelText.Token(type.TextKey);
            piece.m_description = KeelText.Token(type.DescriptionKey);
            var recipe = Resolve(b, PrefabManager.Instance.GetPrefab);
            SetResources(piece, recipe, PrefabManager.Instance.GetPrefab);
            Hold(b.Prefab, b, scene: true);
            b.PenReady = KeelPen.Build(clone, type);
            if (type.HasPen && !b.PenReady) log?.Warning("Keel " + type.Name + ": native wood meshes missing; ship stays hidden.");
            if (Plugin.AllowsUi)
            {
                KeelPaint.Apply(b);
                piece.m_icon = Icon(clone) ?? piece.m_icon;
            }
            var config = new PieceConfig { PieceTable = Hammer, CraftingStation = Workbench, Name = piece.m_name, Description = piece.m_description };
            foreach (var item in recipe.Recipe.Items) config.AddRequirement(item.Prefab, item.Amount, true);
            if (!PieceManager.Instance.AddPiece(new CustomPiece(clone, false, config)))
                throw new InvalidOperationException("the hammer refused the piece");
            built[type.Ship] = b;
            byPrefab[type.Prefab] = b;
        }

        private static Sprite? Icon(GameObject prefab)
        {
            try { return RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation); }
            catch (Exception error)
            {
                log?.Verbose("Keel icon for " + prefab.name + " not rendered: " + error.GetType().Name);
                return null;
            }
        }

        // Scene start (before tuning discovery reads the prefabs).

        /// <summary>Called at scene start before ship discovery: recipes resolve against every item the scene knows.</summary>
        internal static void OnSceneReady()
        {
            if (!created || SteppedAside) return;
            foreach (var b in built.Values) Guard("scene " + b.Type.Prefab, () => Apply(b, scene: true));
            hammerDirty = true;
        }

        private static void Apply(Built b, bool scene)
        {
            ApplyRecipe(b, scene);
            var size = Shape(b);
            foreach (IMonoUpdater updater in Ship.Instances)
            {
                var ship = updater as Ship;
                if (ship == null || Utils.GetPrefabName(ship.gameObject) != b.Type.Prefab) continue;
                ship.transform.localScale = new Vector3(size.X, size.Y, size.Z);
                ship.m_stearForceOffset = b.Prefab.GetComponent<Ship>().m_stearForceOffset;
                ship.m_waterLevelOffset = b.Prefab.GetComponent<Ship>().m_waterLevelOffset;
                Hold(ship.gameObject, b, scene: false);
            }
            Hold(b.Prefab, b, scene: true);
            if (Plugin.AllowsUi) KeelPaint.Apply(b);
            hammerDirty = true;
        }

        // Recipe (design 4.3).

        private static ResolvedRecipe Resolve(Built b, Func<string, GameObject?> find)
        {
            var text = Plugin.Settings!.Get(b.Settings.Recipe);
            return Catalog.Recipe.Resolve(text, b.Type.Recipe, item => find(item)?.GetComponent<ItemDrop>() != null);
        }

        private static void ApplyRecipe(Built b, bool scene)
        {
            Func<string, GameObject?> find = name => (ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null) ?? PrefabManager.Instance.GetPrefab(name);
            var resolved = Resolve(b, find);
            if (resolved.UsedFallback && resolved.Reason != b.RefusedReason)
                log?.Warning(KeelText.Format(KeelTextKeys.RecipeInvalid, b.Type.Name, resolved.Reason));
            b.RefusedReason = resolved.UsedFallback ? resolved.Reason : "";
            var text = resolved.Recipe.ToString();
            if (!scene && text == b.AppliedRecipe) return;
            var piece = b.Prefab.GetComponent<Piece>();
            if (!SetResources(piece, resolved, find)) return;
            b.AppliedRecipe = text;
            KeelShips.SetBuildCosts(b.Type.Prefab, resolved.Recipe.Items.Select(i => i.Amount).ToArray());
        }

        private static bool SetResources(Piece piece, ResolvedRecipe recipe, Func<string, GameObject?> find)
        {
            if (piece == null || recipe.Recipe.Items.Count == 0) return false;
            piece.m_resources = recipe.Recipe.Items.Select(i => new Piece.Requirement
            {
                m_resItem = find(i.Prefab)!.GetComponent<ItemDrop>(),
                m_amount = i.Amount,
                m_recover = true,
            }).ToArray();
            return true;
        }

        // Shape.

        private static (float X, float Y, float Z) Shape(Built b)
        {
            var s = Plugin.Settings!;
            var c = b.Settings;
            var size = ShipShape.LocalScale(s.Get(c.Width), s.Get(c.Length), s.Get(c.Scale));
            b.Prefab.transform.localScale = new Vector3(size.X, size.Y, size.Z);
            var offsets = ShipShape.Offsets(b.SteerForceOffset, 0f, b.WaterLevelOffset, s.Get(c.Width), s.Get(c.Length), s.Get(c.Scale));
            var ship = b.Prefab.GetComponent<Ship>();
            ship.m_stearForceOffset = offsets.SteerForceOffset;
            ship.m_waterLevelOffset = offsets.WaterLevelOffset;
            return size;
        }

        // Hold.

        /// <summary>
        /// Sizes a ship's hold. On the prefab the configured size; on a container that just woke, at least the size its
        /// stored items were kept at; on a loaded hold, at least what its items need. The ship's owner records the size.
        /// </summary>
        internal static void Hold(GameObject root, Built b, bool scene)
        {
            var container = root != null ? root.GetComponentInChildren<Container>(true) : null;
            if (container == null) return;
            var s = Plugin.Settings!;
            var columns = s.Get(b.Settings.CargoColumns);
            var rows = s.Get(b.Settings.CargoRows);
            if (root == b.Prefab)
            {
                var size = HoldSize.Resolve(columns, rows, 0, 0);
                container.m_width = size.Columns;
                container.m_height = size.Rows;
                return;
            }
            var inventory = container.GetInventory();
            if (inventory == null || container.IsInUse()) return;
            var needed = HoldSize.Needed(inventory.GetAllItems().Select(i => (i.m_gridPos.x, i.m_gridPos.y)));
            Resize(container, inventory, HoldSize.Resolve(columns, rows, needed.Columns, needed.Rows));
        }

        /// <summary>A ship's container woke (its items load on the next frame): size it from the saved hold.</summary>
        internal static void OnContainerAwake(Container container, Ship ship)
        {
            var name = Utils.GetPrefabName(ship.gameObject);
            Name(container, ship);
            // Sized even while the catalog is off for the session: keeping stored items is not an action.
            if (!byPrefab.TryGetValue(name, out var b)) return;
            var zdo = container.m_nview != null ? container.m_nview.GetZDO() : null;
            var inventory = container.GetInventory();
            if (zdo == null || inventory == null) return;
            var items = zdo.GetByteArray(ZDOVars.s_items);
            var needed = HoldSize.NeededAtLoad(HoldSize.HasItems(items != null ? items.Length : -1), zdo.GetInt(HoldSize.ColumnsKey),
                zdo.GetInt(HoldSize.RowsKey), b.Type.CargoColumns, b.Type.CargoRows);
            var s = Plugin.Settings!;
            Resize(container, inventory, HoldSize.Resolve(s.Get(b.Settings.CargoColumns), s.Get(b.Settings.CargoRows), needed.Columns, needed.Rows));
        }

        private static void Resize(Container container, Inventory inventory, (int Columns, int Rows) size)
        {
            container.m_width = size.Columns;
            container.m_height = size.Rows;
            inventory.m_width = size.Columns;
            inventory.m_height = size.Rows;
            var view = container.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var zdo = view.GetZDO();
            if (zdo.GetInt(HoldSize.ColumnsKey) != size.Columns) zdo.Set(HoldSize.ColumnsKey, size.Columns);
            if (zdo.GetInt(HoldSize.RowsKey) != size.Rows) zdo.Set(HoldSize.RowsKey, size.Rows);
        }

        // Names (design C12).

        /// <summary>The ship's name as the game shows its piece; empty when this ship is not named.</summary>
        internal static string ShipName(Ship ship)
        {
            if (ship == null || ship.gameObject == null || !Active || Plugin.Settings == null) return "";
            var keel = byPrefab.ContainsKey(Utils.GetPrefabName(ship.gameObject));
            if (!keel && !Plugin.Settings.Get(KeelSettings.Display.NameVanillaShips)) return "";
            var piece = ship.GetComponent<Piece>();
            return piece != null && global::Localization.instance != null ? global::Localization.instance.Localize(piece.m_name) : "";
        }

        private static void Name(Container container, Ship ship)
        {
            if (container == null || ship == null || ship.gameObject == null) return;
            var name = ShipName(ship);
            // Unnamed (for example Name Vanilla Ships turned off): the hold keeps or gets back the game's own name.
            var text = name.Length == 0 ? ZNetScene.instance?.GetPrefab(Utils.GetPrefabName(ship.gameObject))?.GetComponentInChildren<Container>(true)?.m_name : KeelText.Format(KeelTextKeys.StorageName, name);
            if (string.IsNullOrEmpty(text) || container.m_name == text) return;
            container.m_name = text;
            var inventory = container.GetInventory();
            if (inventory != null) inventory.m_name = text;
        }

        /// <summary>Applies the current naming to every loaded ship's hold (setting changed in game).</summary>
        private static void RenameLoadedHolds()
        {
            foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
            {
                if (ship == null || ship.gameObject == null) continue;
                var container = ship.GetComponentInChildren<Container>();
                if (container != null) Name(container, ship);
            }
        }

        // Hammer.

        private static void OnPiecesRegistered() => hammerDirty = true;

        private static void RefreshHammer()
        {
            var table = PieceManager.Instance.GetPieceTable(Hammer);
            if (table == null || Plugin.Settings == null) return;
            var known = table.m_pieces.Where(p => p != null).GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var names = table.m_pieces.Where(p => p != null && !byPrefab.ContainsKey(p.name)).Select(p => p.name).ToList();
            foreach (var b in built.Values)
            {
                known[b.Type.Prefab] = b.Prefab;
                if (Visible(b)) names.Add(b.Type.Prefab);
            }
            var arranged = HammerOrder.Arrange(names, ShipCatalog.All);
            table.m_pieces.Clear();
            foreach (var name in arranged)
                if (known.TryGetValue(name, out var prefab)) table.m_pieces.Add(prefab);
        }

        private static bool Visible(Built b) =>
            Active && Plugin.Settings!.Get(b.Settings.Enabled) && (b.PenReady || !b.Type.HasPen);

        // Change tracking.

        private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (e?.ChangedSetting != null && watched.TryGetValue(e.ChangedSetting, out var ship)) dirty.Add(ship);
            if (e?.ChangedSetting != null && Plugin.Settings != null && ReferenceEquals(e.ChangedSetting, Plugin.Settings.Entry(KeelSettings.Display.NameVanillaShips)))
                Guard("ship hold names", RenameLoadedHolds);
        }

        private static void OnReloaded(object sender, EventArgs e) => RequestAll();

        private static void OnSynchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs e) => RequestAll();

        private static void Guard(string what, Action action)
        {
            try { action(); }
            catch (Exception error) { log?.Warning("Keel catalog " + what + ": " + error.GetType().Name + ": " + error.Message); }
        }

        internal static void Warn(string where, Exception error) => log?.Warning("Keel " + where + ": " + error.GetType().Name + ": " + error.Message);
    }
}
