// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// The video: one texture filled with the frame showing at the playback time, decoded only when the frame changes
    /// (a few milliseconds for a small JPEG), kept in the screen's shape inside its box. Without video a line says why.
    /// </summary>
    internal sealed class VideoScreen
    {
        private readonly RawImage _image;
        private readonly AspectRatioFitter _fit;
        private readonly TMP_Text _message;
        private readonly Texture2D _texture = RecapPictures.NewTexture("ecr_recap_video");
        private DeathRecap? _recap;
        private int _shown = -1;

        public VideoScreen(RawImage image, TMP_Text message)
        {
            _image = image;
            _fit = image.gameObject.AddComponent<AspectRatioFitter>();
            _fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fit.aspectRatio = 16f / 9f;
            _message = message;
            _image.texture = _texture;
        }

        public void Sync(DeathRecap? recap, float time)
        {
            if (recap != _recap)
            {
                _recap = recap;
                _shown = -1;
                Message(recap);
            }
            if (recap == null || !recap.HasVideo)
            {
                return;
            }
            int index = Mathf.Max(0, recap.FrameAt(time));
            if (index != _shown && RecapPictures.Show(_texture, recap.Frames[index]))
            {
                _shown = index;
                _fit.aspectRatio = _texture.width / (float)Mathf.Max(1, _texture.height);
            }
        }

        public void Destroy() => Object.Destroy(_texture);

        private void Message(DeathRecap? recap)
        {
            bool video = recap != null && recap.HasVideo;
            _image.enabled = video;
            _message.text = recap == null ? "" : video ? "" : "No video of this death: recording was off or not possible.";
        }
    }
}
