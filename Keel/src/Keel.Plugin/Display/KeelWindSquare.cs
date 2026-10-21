// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Wind square in the minimap column (user decision 2026-10-12 14:54).
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Display;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// While the local player is aboard a ship: the game's ship wind arrow turned to where the wind blows relative to the
    /// player's heading, over the wind's strength. Shown with the ship panel setting and hidden while the inventory is open,
    /// as Mantle's squares in the same column are. Display-only.
    /// </summary>
    internal static class KeelWindSquare
    {
        static readonly KitColumnSquare square = new KitColumnSquare(WindSquare.Rank, WindSquare.Id);
        static Sprite? source, light;
        static int shownPercent = int.MinValue;
        static string shownLanguage = "";
        static string shownValue = "";
        static string shownTopic = "";
        static string shownTip = "";

        internal static KitColumnSquare Square => square;

        public static void Update()
        {
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            var env = EnvMan.instance;
            var hud = Hud.instance;
            var show = settings != null && player != null && env != null && hud != null && !player.IsDead() && Ship.GetLocalShip() != null
                && settings.Get(KeelSettings.Display.ShipPanel) && KeelModules.ProductActive(out _) && !InventoryGui.IsVisible();
            if (!show) { square.Hide(); return; }
            var wind = env!.GetWindDir();
            var forward = player!.transform.forward;
            var intensity = env.GetWindIntensity();
            var percent = WindSquare.Percent(intensity);
            var language = LanguageNow();
            if (percent != shownPercent || language != shownLanguage)
            {
                shownPercent = percent;
                shownLanguage = language;
                shownValue = WindSquare.Strength(intensity);
                shownTopic = KeelText.Text(KeelTextKeys.SquareWind);
                shownTip = KeelText.Format(KeelTextKeys.SquareWindText, shownValue);
            }
            var native = hud!.m_shipWindIcon;
            var arrow = native != null ? Light(native.sprite) : null;
            square.Draw(true, arrow, shownValue, shownTopic, shownTip);
            if (square.Icon != null)
                // The game turns its arrow inside a rotated root; its own local turn is added so the arrow points as the game's does.
                square.Icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -WindSquare.RelativeAngle(forward.x, forward.z, wind.x, wind.z) + native!.rectTransform.localEulerAngles.z);
        }

        /// <summary>
        /// The game's wind arrow is dark line art for the bright sky behind the steering circle; on the dark square it is
        /// drawn from a white copy of the same sprite (its alpha kept), made once at runtime. Nothing is shipped.
        /// </summary>
        static Sprite? Light(Sprite? sprite)
        {
            if (sprite == null) return null;
            if (sprite == source && light != null) return light;
            DropLight();
            var area = sprite.textureRect;
            int width = Mathf.FloorToInt(sprite.rect.width), height = Mathf.FloorToInt(sprite.rect.height);
            int x = Mathf.FloorToInt(sprite.textureRectOffset.x), y = Mathf.FloorToInt(sprite.textureRectOffset.y);
            int areaWidth = Mathf.Min(Mathf.FloorToInt(area.width), width - x), areaHeight = Mathf.Min(Mathf.FloorToInt(area.height), height - y);
            var target = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            var active = RenderTexture.active;
            Graphics.Blit(sprite.texture, target);
            RenderTexture.active = target;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Keel_wind_light" };
            copy.SetPixels32(new Color32[width * height]);
            copy.ReadPixels(new Rect(area.x, area.y, areaWidth, areaHeight), x, y);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            var pixels = copy.GetPixels32();
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            copy.SetPixels32(pixels);
            copy.Apply();
            source = sprite;
            light = Sprite.Create(copy, new Rect(0, 0, width, height), new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height), sprite.pixelsPerUnit);
            light.name = sprite.name;
            return light;
        }

        static string LanguageNow()
        {
            var localization = global::Localization.instance;
            return localization != null ? localization.GetSelectedLanguage() ?? "" : "";
        }

        static void DropLight()
        {
            if (light != null) { Object.Destroy(light.texture); Object.Destroy(light); }
            light = null; source = null;
        }

        public static void Dispose() { square.Dispose(); DropLight(); }
    }
}
