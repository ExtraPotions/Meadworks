// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>Appearance on native startup, pause and settings hosts; native layout and actions stay in place.</summary>
    public static partial class KitNativeMenus
    {
        static readonly Dictionary<string, Surface> surfaces = new Dictionary<string, Surface>();
        static readonly HashSet<Image> owned = new HashSet<Image>();
        static readonly Dictionary<string, FieldInfo> startupFields = Fields(typeof(FejdStartup),
            "m_menuList", "m_characterSelectScreen", "m_startGamePanel", "m_createWorldPanel", "m_serverListPanel",
            "m_creditsPanel", "m_changeLog", "m_connectionFailedPanel", "m_settingsPopup",
            "m_cinematicsMenuList", "m_cloudStorageWarningNextSave", "m_newCharacterPanel");
        static readonly Dictionary<string, string> startupHostIds = HostIds();
        static readonly FieldInfo pauseSettings = typeof(Menu).GetField("m_settingsInstance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        static readonly Dictionary<string, GameObject> hosts = new Dictionary<string, GameObject>();
        static bool initialized, dirty;
        static float next;
        static int discovered = -1;
        public static bool On => KitAppearance.Active && (KitLook.GamePanels?.Value ?? KitGamePanels.Auto) != KitGamePanels.Never;
        public static bool Owns(Image image) => image != null && owned.Contains(image);
        internal static bool Reserves(Image image)
        {
            if (!initialized || image == null) return false;
            DiscoverHosts();
            if (ReservesBooks(image) || ReservesDropdowns(image) || ReservesSplit(image) || ReservesStore(image) || ReservesTrophies(image) || ReservesMap(image) || ReservesAchievements(image) || ReservesVariant(image) || ReservesFeedback(image)) return true;
            foreach (var root in hosts.Values)
                if (root != null && image.transform.IsChildOf(root.transform) && Native(image.transform, root)) return true;
            return false;
        }
        static Dictionary<string, string> HostIds()
        {
            var result = new Dictionary<string, string>();
            foreach (var field in startupFields.Keys) result.Add(field, "startup." + field.Substring(2));
            return result;
        }
        internal static object Status
        {
            get
            {
                var rows = new List<object>();
                foreach (var pair in surfaces) rows.Add(new { id = pair.Key, root = pair.Value.Root != null ? pair.Value.Root.name : "",
                    visible = pair.Value.Root != null && pair.Value.Root.activeInHierarchy, images = pair.Value.Images,
                    buttons = pair.Value.Buttons, labels = pair.Value.Labels, switches = pair.Value.Switches });
                return new { initialized, on = On, hosts = rows.ToArray(), inventory = InventoryStatus, books = BooksStatus, dropdowns = DropdownStatus, split = SplitStatus, store = StoreStatus, trophies = TrophiesStatus, achievements = AchievementsStatus, variant = VariantStatus, feedback = FeedbackStatus, map = MapStatus, sharedTooltip = SharedTooltipStatus, noticeLayout = NoticeLayoutStatus };
            }
        }
        static Dictionary<string, FieldInfo> Fields(Type type, params string[] names)
        {
            var result = new Dictionary<string, FieldInfo>();
            foreach (string name in names) result.Add(name, type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            return result;
        }
        public static void Initialize()
        {
            if (initialized || !MeadworksHeadless.AllowsUi) return;
            initialized = dirty = true; next = 0; KitLook.Changed += Changed;
        }
        static void Changed() { dirty = true; next = 0; MarkBooksChanged(); MarkSplitChanged(); MarkStoreChanged(); MarkMapChanged(); }
        public static void Tick()
        {
            if (!initialized || !CrucibleUiPlugin.Active) return;
            TickTooltip();
            TickSharedTooltip();
            TickBooks();
            TickDropdowns();
            TickSplit();
            TickStore();
            TickTrophies();
            TickAchievements();
            TickVariant();
            TickFeedback();
            TickCharacterName();
            TickNoticeLayout();
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .2f;
            DiscoverHosts();
            var obsolete = new List<string>();
            foreach (var pair in surfaces)
                if (!hosts.TryGetValue(pair.Key, out var root) || root != pair.Value.Root) obsolete.Add(pair.Key);
            foreach (string key in obsolete) { surfaces[key].Dispose(); surfaces.Remove(key); }
            foreach (var pair in hosts)
            {
                if (!surfaces.TryGetValue(pair.Key, out var surface))
                {
                    // Capture when first shown; inactive prefabs can still be populated by the native host.
                    if (!pair.Value.activeInHierarchy) continue;
                    surface = new Surface(pair.Value); surfaces.Add(pair.Key, surface);
                }
                surface.Refresh(On, dirty);
            }
            TickInventory();
            TickMap();
            dirty = false;
        }
        static void AdmitStandalone(string id, GameObject root)
        {
            if (root == null) return;
            foreach (var host in hosts.Values)
                if (host != null && (root.transform.IsChildOf(host.transform) || host.transform.IsChildOf(root.transform))) return;
            hosts[id] = root;
        }
        /// <summary>
        /// The hosts as they are this frame, found on demand and at most once a frame: the refresh, <see cref="Reserves"/> and
        /// <see cref="Native"/> ask for them. Frames where nothing asks read none of the game's fields (it ran every frame before).
        /// </summary>
        static void DiscoverHosts()
        {
            if (!initialized || discovered == Time.frameCount) return;
            discovered = Time.frameCount;
            // Reserve exact native hosts, including hidden instances, before the fallback's image scan.
            hosts.Clear();
            var startup = FejdStartup.instance;
            if (startup != null)
            {
                foreach (var pair in startupFields)
                {
                    var root = pair.Value?.GetValue(startup) as GameObject;
                    if (root != null) hosts[startupHostIds[pair.Key]] = root;
                }
                // Nested subpanels (new character, server options) are hosts of their own; Native() keeps them out of any parent surface.
                if (startup.m_serverOptions != null) hosts["startup.serverOptions"] = startup.m_serverOptions.gameObject;
                AdmitStandalone("startup.manageSavesMenu", startup.m_manageSavesMenu != null ? startup.m_manageSavesMenu.gameObject : null);
                AdmitStandalone("startup.eulaWindow", startup.m_eulaWindow != null ? startup.m_eulaWindow.m_window : null);
            }
            var menu = Menu.instance;
            if (menu != null)
            {
                var root = menu.m_settingsButton != null ? menu.m_settingsButton.transform.parent : null;
                if (root != null) hosts["pause.menu"] = root.gameObject;
                var settings = pauseSettings?.GetValue(menu) as GameObject;
                if (settings != null) hosts["pause.settings"] = settings;
            }
        }
        public static void Shutdown()
        {
            KitLook.Changed -= Changed;
            RestoreCharacterName();
            ShutdownNoticeLayout();
            ShutdownInventory();
            ShutdownTooltip();
            ShutdownSharedTooltip();
            ShutdownBooks();
            ShutdownDropdowns();
            ShutdownSplit();
            ShutdownStore();
            ShutdownTrophies();
            ShutdownAchievements();
            ShutdownVariant();
            ShutdownFeedback();
            ShutdownMap();
            foreach (var surface in surfaces.Values) surface.Dispose();
            surfaces.Clear(); hosts.Clear(); owned.Clear(); initialized = dirty = false; discovered = -1;
        }
        static readonly List<MonoBehaviour> nativeComponents = new List<MonoBehaviour>();
        static readonly Dictionary<Assembly, bool> nativeAssemblies = new Dictionary<Assembly, bool>();
        static bool NativeAssembly(Assembly assembly)
        {
            if (nativeAssemblies.TryGetValue(assembly, out var known)) return known;
            string name = assembly.GetName().Name;
            known = assembly == typeof(Menu).Assembly || name.StartsWith("Unity", StringComparison.Ordinal)
                || name == "gui_framework" || name == "assembly_utils" || name == "assembly_guiutils";
            nativeAssemblies[assembly] = known;
            return known;
        }
        static bool Native(Transform transform, GameObject root)
        {
            if (transform == null || root == null) return false;
            DiscoverHosts();
            for (var at = transform; at != null; at = at.parent)
            {
                if (at != root.transform)
                    foreach (var host in hosts.Values) if (host != null && host != root && at == host.transform) return false;
                if (at.name.StartsWith("Crucible_", StringComparison.Ordinal) || at.name.StartsWith("Meadworks_", StringComparison.Ordinal)) return false;
                // Runs every frame for open surfaces: reuse one component list and judge each assembly once
                // (Assembly.GetName allocates a new AssemblyName on every call).
                at.GetComponents(nativeComponents);
                foreach (var component in nativeComponents)
                {
                    if (component == null) continue;
                    if (!NativeAssembly(component.GetType().Assembly)) { nativeComponents.Clear(); return false; }
                }
                nativeComponents.Clear();
                if (at == root.transform) return true;
            }
            return false;
        }
    }
}
