// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; the active render texture is restored after a grab.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// The finished screen, HUD included, as a small picture on the GPU: the game's own end-of-frame screen capture into a
    /// screen-sized texture, then halved step by step (a single big step would shimmer) down to the video size. Where the
    /// graphics API keeps textures upside down (Direct3D, Metal) the first step turns the picture over.
    /// </summary>
    internal sealed class ScreenGrab
    {
        private RenderTexture? _screen;
        private RenderTexture? _video;

        public int Width => _video != null ? _video.width : 0;
        public int Height => _video != null ? _video.height : 0;

        /// <summary>
        /// Call at the end of a frame. The video-sized picture, valid until the next call. The render texture that was active
        /// before is active again afterwards: Graphics.Blit leaves its target active, and another end-of-frame screen copy in
        /// the same frame would read the recap video instead of the screen.
        /// </summary>
        public RenderTexture Capture(int height)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                _screen = Fit(_screen, Screen.width, Screen.height);
                _video = Fit(_video, VideoWidth(height), height);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_screen);
                Shrink(_screen, _video);
                return _video;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        public void Release()
        {
            Free(ref _screen);
            Free(ref _video);
        }

        // The screen's shape at the chosen height, never wider than the screen, in even numbers.
        private static int VideoWidth(int height)
        {
            int width = Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height));
            return Mathf.Min(Screen.width, width) & ~1;
        }

        private static void Shrink(RenderTexture screen, RenderTexture video)
        {
            bool flip = SystemInfo.graphicsUVStartsAtTop;
            RenderTexture current = screen;
            while (current.height > video.height * 2)
            {
                RenderTexture half = Temporary(current.width / 2, current.height / 2);
                Blit(current, half, flip);
                flip = false;
                if (current != screen)
                {
                    RenderTexture.ReleaseTemporary(current);
                }
                current = half;
            }
            Blit(current, video, flip);
            if (current != screen)
            {
                RenderTexture.ReleaseTemporary(current);
            }
        }

        private static void Blit(RenderTexture from, RenderTexture to, bool flip)
        {
            if (flip)
            {
                Graphics.Blit(from, to, new Vector2(1f, -1f), new Vector2(0f, 1f));
            }
            else
            {
                Graphics.Blit(from, to);
            }
        }

        private static RenderTexture Temporary(int width, int height)
        {
            RenderTexture texture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static RenderTexture Fit(RenderTexture? texture, int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height)
            {
                return texture;
            }
            Free(ref texture);
            RenderTexture made = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "ecr_recap_capture",
                filterMode = FilterMode.Bilinear,
            };
            made.Create();
            return made;
        }

        private static void Free(ref RenderTexture? texture)
        {
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
                texture = null;
            }
        }
    }
}
