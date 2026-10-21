"""Draws a Valheim-style HUD over the rendered combat frames (run with the system Python, which has Pillow):

    python assets/crypt_mimic/hud.py

Reads out/combat/frames/, screen.json and hud.json; writes out/combat/hud/. While it is disguised, the mimic shows the
chest's hover prompt; once revealed, a name and health bar over its lid with the bite cooldown beneath. The player's
health sits bottom left, damage numbers rise from whoever was hit, and captions name each beat. Fonts are the game's
own (Norse, Averia Serif Libre) from the local reference export; the video never ships.
"""
import json
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "combat")
FONTS = os.path.join(os.environ["USERPROFILE"], "ValheimReference", "ExportedProject", "Assets", "3rd party",
                     "TextMesh Pro", "Resources", "Fonts")
COLORS = {"red": (230, 60, 50), "blue": (140, 200, 255), "gold": (255, 205, 90), "white": (255, 255, 255)}


def font(name, size):
    paths = {"norse": "Norse/Norsebold.otf", "serif": "Averia_Serif_Libre/AveriaSerifLibre-Bold.ttf"}
    return ImageFont.truetype(os.path.join(FONTS, paths[name]), size)


class Overlay:
    def __init__(self, hud, screen):
        self.hud, self.screen = hud, screen
        self.fonts = {"name": font("norse", 30), "big": font("norse", 88), "text": font("serif", 22),
                      "small": font("serif", 17), "number": font("serif", 30)}

    def draw(self, frame, image):
        layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
        pen = ImageDraw.Draw(layer)
        self._prompt(pen, frame)
        self._nameplate(pen, frame)
        self._player(pen, frame)
        self._popups(pen, frame)
        self._flash(pen, frame)
        self._caption(pen, frame)
        return Image.alpha_composite(image.convert("RGBA"), layer).convert("RGB")

    def _point(self, frame, who, width, height):
        entry = self.screen.get(str(frame)) or self.screen[max(self.screen, key=int)]
        x, y = entry[who]
        return x * width, (1 - y) * height

    def _text(self, pen, xy, text, style, color=(255, 255, 255), alpha=255, anchor="mm"):
        pen.text(xy, text, font=self.fonts[style], fill=(*color, alpha), anchor=anchor,
                 stroke_width=2, stroke_fill=(0, 0, 0, alpha))

    def _prompt(self, pen, frame):
        start, end = self.hud["prompt"]
        if start <= frame <= end:
            x, y = self._point(frame, "mimic", *pen.im.size)
            self._text(pen, (x, y - 20), "Stone chest", "text")
            self._text(pen, (x - 34, y + 8), "[E]", "text", COLORS["gold"])
            self._text(pen, (x + 16, y + 8), "Open", "text")

    def _nameplate(self, pen, frame):
        reveal, death = self.hud["reveal"], self.hud["death"]
        if reveal is None or frame < reveal or (death is not None and frame > death + 30):
            return
        alpha = 255 if death is None or frame < death else int(255 * (1 - (frame - death) / 30))
        x, y = self._point(frame, "mimic", *pen.im.size)
        self._text(pen, (x, y - 34), "Crypt Mimic", "name", alpha=alpha)
        hp, top = step(self.hud["mimicHp"], frame)
        bar(pen, (x - 95, y - 14, x + 95, y - 4), hp / top, (200, 40, 35), alpha)
        self._cooldown(pen, frame, x, y, alpha)

    def _cooldown(self, pen, frame, x, y, alpha):
        state, fraction = cooldown(self.hud, frame)
        if state is None:
            return
        colour = (255, 170, 40) if state == "ready" else (205, 175, 125)
        bar(pen, (x - 95, y, x + 95, y + 5), fraction, colour, alpha)
        label = "bite ready" if state == "ready" else "bite recharging"
        self._text(pen, (x, y + 17), label, "small", colour, alpha)

    def _player(self, pen, frame):
        hp, top = step(self.hud["playerHp"], frame)
        self._text(pen, (40, 650), "Health", "small", anchor="lm")
        bar(pen, (40, 664, 300, 682), hp / top, (190, 35, 30), 255)
        self._text(pen, (312, 673), str(hp), "text", anchor="lm")

    def _popups(self, pen, frame):
        for popup in self.hud["popups"]:
            age = frame - popup["frame"]
            if 0 <= age < 26:
                x, y = self._point(popup["frame"], popup["target"], *pen.im.size)
                alpha = int(255 * min(1.0, (26 - age) / 10))
                dx = 135 if popup["target"] == "mimic" else 45
                self._text(pen, (x + dx, y - 26 - age * 3.0), popup["text"], "number", COLORS[popup["color"]], alpha)

    def _flash(self, pen, frame):
        for flash in self.hud["flashes"]:
            age = frame - flash["frame"]
            if 0 <= age < 22:
                alpha = int(255 * min(1.0, (22 - age) / 8))
                self._text(pen, (pen.im.size[0] / 2, 150 - min(age, 4) * 3), flash["text"], "big",
                           COLORS[flash["color"]], alpha)

    def _caption(self, pen, frame):
        text = step_text(self.hud["captions"], frame)
        if not text:
            return
        width = pen.textlength(text, font=self.fonts["text"]) + 48
        cx, y = pen.im.size[0] / 2, 610
        pen.rounded_rectangle((cx - width / 2, y - 22, cx + width / 2, y + 22), 6, fill=(10, 12, 14, 170))
        self._text(pen, (cx, y), text, "text")


def bar(pen, box, fraction, colour, alpha):
    left, top, right, bottom = box
    pen.rectangle(box, fill=(20, 16, 14, int(alpha * 0.8)), outline=(0, 0, 0, alpha))
    fill = left + 2 + (right - left - 4) * max(0.0, min(1.0, fraction))
    if fill > left + 2:
        pen.rectangle((left + 2, top + 2, fill, bottom - 2), fill=(*colour, alpha))


def step(timeline, frame):
    """The latest (value, maximum) at or before frame."""
    value, top = timeline[0][1], timeline[0][2]
    for at, v, m in timeline:
        if at <= frame:
            value, top = v, m
    return value, top


def step_text(captions, frame):
    current = ""
    for at, text in captions:
        if at <= frame:
            current = text
    return current


def cooldown(hud, frame):
    """('recharging', fraction) after a bite, ('ready', 1) once recharged, until the next bite or its death."""
    if hud["death"] is not None and frame >= hud["death"]:
        return None, 0
    current = None
    for c in hud["cooldowns"]:
        if c["start"] <= frame:
            current = c
    if current is None:
        return None, 0
    if frame < current["ready"]:
        return "recharging", (frame - current["start"]) / (current["ready"] - current["start"])
    return "ready", 1.0


def main():
    with open(os.path.join(OUT, "hud.json"), encoding="utf-8") as handle:
        hud = json.load(handle)
    with open(os.path.join(OUT, "screen.json"), encoding="utf-8") as handle:
        screen = json.load(handle)
    overlay = Overlay(hud, screen)
    target = os.path.join(OUT, "hud")
    os.makedirs(target, exist_ok=True)
    names = sorted(n for n in os.listdir(os.path.join(OUT, "frames")) if n.endswith(".png"))
    for name in names:
        frame = int(name[2:-4])
        image = Image.open(os.path.join(OUT, "frames", name))
        overlay.draw(frame, image).save(os.path.join(target, name))
    print("WORKSHOP hud frames", len(names), flush=True)


if __name__ == "__main__":
    main()
