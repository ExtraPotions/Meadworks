// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Meadworks.Crucible {
    public static partial class MeadworksConfig {
        static partial void EnableEditTiming(ref bool enabled){enabled=Plugin.Log!=null&&MeadworksLogging.Enabled("Crucible",LogSeverity.Debug);}
        static partial void CaptureEditFrame(ref int frame){frame=UnityEngine.Time.frameCount;}
        static partial void WriteEditTiming(string line){if(MeadworksLogging.Enabled("Crucible",LogSeverity.Debug))Plugin.Log?.LogDebug("[Meadworks:Crucible] "+line);}
    }
}
