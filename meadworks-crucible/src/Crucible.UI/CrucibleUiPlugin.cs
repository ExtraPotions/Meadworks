using System;
using BepInEx;
using ExtraPotions.Meadworks.Crucible.UI.WindowInput;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine.SceneManagement;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Crucible.UI (task C08): the PackPanel 0.12.0 UI kit every Meadworks product builds its panels with. A separate
    /// plugin assembly next to Crucible.dll so the GPL art (2.4 MB of Timber images) and the Unity UI, TextMeshPro and
    /// GUI references stay out of the core Crucible runtime a dedicated server loads; it depends on Crucible one way
    /// (input C07, localization C05, blocked options C03, headless guard C10, peer verdicts C04) and never the reverse.
    /// On a headless process (C10) it binds nothing, patches nothing and creates no UI.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CruciblePlugin.Guid, CruciblePlugin.Version)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class CrucibleUiPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.meadworks.crucible.ui";
        public const string Name = "ExtraPotions Meadworks: Crucible UI";
        public const string Version = "0.3.23";
        public const string Product = KitText.Product;

        internal static MeadworksLog Log { get; private set; }

        /// <summary>The kit runs: a graphical client with the plugin awake. Products check this before building UI.</summary>
        public static bool Active { get; private set; }

        private Harmony harmony;
        private MeadworksTranslations texts;

        private void Awake()
        {
            Log = MeadworksLog.For(Logger, Product);
            if (!MeadworksHeadless.AllowsUi)
            {
                Log.Info("Crucible.UI idle: headless process, no UI is created.");
                return;
            }
            KitCanvas.HangulWordBreaks();
            KitLook.Bind(Config);
            KitNoticePanel.Bind(Config);
            MeadworksPauseMenu.Bind(Config);
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(CrucibleUiPlugin).Assembly);
            GameWindow.Install(harmony);
            GamePanelTheme.Initialize();
            KitNativeMenus.Initialize();
            KitNativeDialogs.Initialize();
            KitNativeNotices.Initialize();
            try
            {
                texts = MeadworksTranslations.Register(Info.Metadata, KitText.Default, log: Logger);
            }
            catch (Exception error)
            {
                Logger.LogWarning("Kit texts stay built-in; translation overrides unavailable: " + error.GetType().Name);
            }
            SceneManager.sceneUnloaded += SceneUnloaded;
            Active = true;
            KitDialogs.Initialize();
            KitHudLayout.Initialize(Config);
            Log.Info($"Crucible.UI {Version} ready (PackPanel 0.12.0 UI kit)");
        }

        private void Update()
        {
            if (!Active)
                return;
            KitCanvas.HangulWordBreaks();
            KitDialogs.Tick();
            KitNativeMenus.Tick();
            GamePanelTheme.Update();
            KitNativeDialogs.Tick();
            NightShade.Update();
            KitMapFrames.Update();
            KitInventoryScreen.Update();
            KitUnderInventoryDock.Update();
            KitWindow.Tick();
            KitTextField.Tick();
            KitStepper.Tick();
            KitBuildTab.TickAll();
            MeadworksPauseMenu.Tick();
            texts?.Update();
        }

        private void LateUpdate()
        {
            if (Active)
            {
                KitTextField.LateTick();
                KitHudLayout.Tick();
                KitNativeNotices.Tick();
                KitNoticePanel.Tick();
                KitNativeMenus.LateTickNotices();
            }
        }

        private void SceneUnloaded(Scene scene)
        {
            KitTooltip.Prune();
            KitForged.Prune();
            KitNoticePanel.Reset();
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= SceneUnloaded;
            Active = false;
            KitNoticePanel.Reset();
            KitHudLayout.Shutdown();
            KitDialogs.Shutdown();
            KitNativeMenus.Shutdown();
            KitNativeDialogs.Shutdown();
            KitNativeNotices.Shutdown();
            KitTooltip.Shutdown();
            KitBuildTab.Shutdown();
            MeadworksPauseMenu.Shutdown();
            harmony?.UnpatchSelf();
            texts?.Dispose();
        }

        /// <summary>The player's language for kit texts (the game's, normalized to the seven Meadworks languages).</summary>
        public static string Language => MeadworksTranslations.GameLanguage();
    }
}
