using UnityEngine;
using UnityEngine.Rendering;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Unity side of <see cref="HeadlessGuard"/> (C10). A dedicated server on Windows or Linux has a null graphics
    /// device (Jotunn <c>GUIManager.IsHeadless()</c>); batch mode counts too. Products guard UI, HUD messages, fonts and
    /// input with <see cref="IsHeadless"/>, following Wayfarer's pattern. The answer never changes within a process.
    /// </summary>
    public static class MeadworksHeadless {
        static HostSurface? surface;
        public static HostSurface Surface=>surface??=HeadlessGuard.Detect(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null,Application.isBatchMode);
        public static bool IsHeadless=>Surface==HostSurface.Headless;
        public static bool AllowsUi=>HeadlessGuard.AllowsUi(Surface);
        public static bool AllowsInput=>HeadlessGuard.AllowsInput(Surface);
        /// <summary>Where a player notice goes now: the HUD when this is a graphical client with a MessageHud, otherwise the log.</summary>
        public static NoticeChannel Channel=>HeadlessGuard.Channel(Surface,AllowsUi&&MessageHud.instance);
    }
}
