// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>Recorded frames as textures for the screen: one texture each for a viewer, filled again frame by frame.</summary>
    internal static class RecapPictures
    {
        public static Texture2D NewTexture(string name) => new Texture2D(2, 2, TextureFormat.RGB24, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        /// <summary>The picture at the moment of death; null without video or when it cannot be decoded.</summary>
        public static Texture2D? Thumbnail(DeathRecap recap)
        {
            if (!recap.HasVideo)
            {
                return null;
            }
            Texture2D texture = NewTexture("ecr_recap_thumbnail");
            if (Show(texture, recap.Frames[Mathf.Max(0, recap.FrameAt(recap.DeathAt))]))
            {
                return texture;
            }
            Object.Destroy(texture);
            return null;
        }

        public static bool Show(Texture2D texture, Frame frame) => JpegCodec.Decode(texture, frame.Jpeg);
    }
}
