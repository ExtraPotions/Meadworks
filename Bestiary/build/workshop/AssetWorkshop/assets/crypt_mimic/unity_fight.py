"""Exports the choreographed fight (fight.py) for the Unity preview, which replays it on the real animator.

    python assets/crypt_mimic/unity_fight.py      (plain Python; nothing here needs Blender)

Writes out/unity/crypt_mimic_fight.json: the mimic's position and heading by frame, the clip cues its animator gets,
the player's keys, the sword's swings and the HUD timeline. Blender's (x, y) becomes Unity's (-x, z = -y); a mimic
heading h becomes Unity yaw -h; the player's yaw y becomes 180 - y.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)

from fight import choreograph  # noqa: E402
from poses import world_position  # noqa: E402

OUT = os.path.join(HERE, "out", "unity", "crypt_mimic_fight.json")


def mimic_keys(mimic):
    keys = {}
    for frame, values in mimic.keys:
        x, y = world_position(values)
        keys[frame] = {"frame": frame, "x": round(-x, 4), "z": round(-y, 4), "yaw": round(-values["heading"], 3)}
    return [keys[f] for f in sorted(keys)]


def player_keys(player):
    return [{"frame": f, "x": round(-x, 4), "z": round(-y, 4), "yaw": round(180 - yaw, 3), "lean": lean, "tilt": tilt,
             "crouch": crouch} for f, (x, y), yaw, lean, tilt, crouch in sorted(player.keys, key=lambda k: k[0])]


def hud_section(hud):
    data = hud.data()
    steps = lambda timeline: [{"frame": f, "value": v, "max": m} for f, v, m in timeline]  # noqa: E731
    return {"mimicHp": steps(data["mimicHp"]), "playerHp": steps(data["playerHp"]),
            "captions": [{"frame": f, "text": t} for f, t in data["captions"]],
            "popups": data["popups"], "flashes": data["flashes"], "cooldowns": data["cooldowns"],
            "promptStart": data["prompt"][0], "promptEnd": data["prompt"][1],
            "reveal": data["reveal"], "death": data["death"]}


def main():
    mimic, player, hud, end = choreograph()
    fight = {"fps": 30, "end": end, "mimic": mimic_keys(mimic),
             "cues": [{"frame": f, "clip": c} for f, c in sorted(mimic.cues)],
             "player": player_keys(player),
             "sword": [{"frame": f, "angle": a} for f, a in sorted(player.sword)], **hud_section(hud)}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as handle:
        json.dump(fight, handle)
    print("WORKSHOP fight", end, "frames,", len(fight["cues"]), "cues ->", OUT)


if __name__ == "__main__":
    main()
