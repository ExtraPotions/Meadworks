// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>What the right side of the window is made of, for the view to drive.</summary>
    internal sealed class Pane
    {
        public TMP_Text Title = null!;
        public TMP_Text Summary = null!;
        public VideoScreen Video = null!;
        public Timeline Timeline = null!;
        public ControlRow Controls = null!;
    }

    /// <summary>
    /// The right side of the window, from copies of the game's own parts: the killer and summary lines in the compendium's
    /// text, the video in a box of the compendium's dark inset, the timeline from the stack split slider (its tick sound
    /// removed, since following the playback moves it every frame), the hover preview, and Play/Pause and the speeds as
    /// copies of the compendium's Close button. Coordinates are from the frame's top-left corner.
    /// </summary>
    internal static class PaneBuilder
    {
        private const float Left = 340f;
        private const float Wide = 920f;
        private const float VideoX = 400f;
        private const float VideoY = 108f;
        private const float VideoW = 800f;
        private const float VideoH = 450f;
        private const float TimelineY = 566f;
        private const float ControlsY = 600f;

        public static Pane Build(WindowParts parts, Slider sliderTemplate, WindowView view)
        {
            Pane pane = new Pane
            {
                Title = Line(parts, "ecr_recap_title", 46f, 32f, 22f),
                Summary = Line(parts, "ecr_recap_summary", 78f, 24f, 16f),
                Video = Video(parts),
                Controls = Controls(parts, view),
            };
            Slider slider = Slider(parts, sliderTemplate, out RectTransform marks);
            pane.Timeline = new Timeline(slider, marks);
            Hover(parts, slider, marks, view);
            return pane;
        }

        private static TMP_Text Line(WindowParts parts, string name, float y, float h, float size)
        {
            TMP_Text text = UiParts.Text(parts.Text, parts.Frame, name, size, TextAlignmentOptions.MidlineLeft);
            UiParts.Place(text.rectTransform, Left, y, Wide, h);
            return text;
        }

        private static VideoScreen Video(WindowParts parts)
        {
            Image box = UiParts.Image(parts.Inset, parts.Frame, "ecr_recap_videobox");
            UiParts.Place(box.rectTransform, VideoX, VideoY, VideoW, VideoH);
            RectTransform area = UiParts.Node("ecr_recap_videoarea", box.transform);
            UiParts.Fill(area, 6f);
            RawImage image = UiParts.Node("ecr_recap_video", area).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            TMP_Text message = UiParts.Text(parts.Text, box.transform, "ecr_recap_novideo", 18f, TextAlignmentOptions.Center);
            UiParts.Fill(message.rectTransform, 20f);
            message.enableWordWrapping = true;
            return new VideoScreen(image, message);
        }

        // The marks sit over the slider's track exactly where the handle runs, drawn above the fill and under the handle.
        private static Slider Slider(WindowParts parts, Slider template, out RectTransform marks)
        {
            GameObject copy = Object.Instantiate(template.gameObject, parts.Frame, false);
            copy.name = "ecr_recap_timeline";
            copy.SetActive(true);
            UiParts.Destroy(copy.GetComponent("SliderSfx"));
            UiParts.Place((RectTransform)copy.transform, VideoX, TimelineY, VideoW, 28f);
            Slider slider = copy.GetComponent<Slider>();
            RectTransform track = slider.handleRect != null ? (RectTransform)slider.handleRect.parent : (RectTransform)copy.transform;
            marks = UiParts.Node("ecr_recap_marks", copy.transform);
            marks.anchorMin = track.anchorMin;
            marks.anchorMax = track.anchorMax;
            marks.pivot = track.pivot;
            marks.offsetMin = track.offsetMin;
            marks.offsetMax = track.offsetMax;
            if (track != copy.transform)
            {
                marks.SetSiblingIndex(track.GetSiblingIndex());
            }
            return slider;
        }

        private static void Hover(WindowParts parts, Slider slider, RectTransform marks, WindowView view)
        {
            Image popup = UiParts.Image(parts.Inset, parts.Frame, "ecr_recap_preview");
            popup.color = new Color(popup.color.r, popup.color.g, popup.color.b, 1f);
            popup.rectTransform.sizeDelta = new Vector2(208f, 144f);
            RectTransform picture = UiParts.Node("ecr_recap_preview_picture", popup.transform);
            UiParts.Place(picture, 8f, 8f, 192f, 108f);
            RawImage raw = picture.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            TMP_Text label = UiParts.Text(parts.Text, popup.transform, "ecr_recap_preview_time", 16f, TextAlignmentOptions.Center);
            UiParts.Place(label.rectTransform, 8f, 118f, 192f, 20f);
            slider.gameObject.AddComponent<TimelineHover>().Init(() => view.Playback.Recap, marks, popup.rectTransform, raw, label);
        }

        private static ControlRow Controls(WindowParts parts, WindowView view)
        {
            Button play = UiParts.Button(parts.Close, parts.Frame, "ecr_recap_play", "Play", view.TogglePlay);
            UiParts.Place((RectTransform)play.transform, VideoX, ControlsY, 130f, 40f);
            Button[] speeds = new Button[Playback.Speeds.Length];
            for (int i = 0; i < speeds.Length; i++)
            {
                float speed = Playback.Speeds[i];
                speeds[i] = UiParts.Button(parts.Close, parts.Frame, "ecr_recap_speed", SpeedLabel(speed), () => view.SetSpeed(speed));
                UiParts.Place((RectTransform)speeds[i].transform, VideoX + 142f + i * 80f, ControlsY, 72f, 40f);
            }
            TMP_Text time = UiParts.Text(parts.Text, parts.Frame, "ecr_recap_time", 18f, TextAlignmentOptions.MidlineRight);
            UiParts.Place(time.rectTransform, VideoX + VideoW - 240f, ControlsY, 240f, 40f);
            return new ControlRow(play, speeds, time);
        }

        private static string SpeedLabel(float speed) => speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "×";
    }
}
