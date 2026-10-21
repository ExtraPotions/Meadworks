"""Cross-checks a game skeleton read from its prefab (gamerig_source) against the codex's own measurement of it
(codex/data/rigs.json, written by codex/measure/rigs.py), so a reading mistake on either side shows up:

- the rig: the sample whose prefab is this one, else the one listing this creature (the Skeleton is measured as the
  player's rig, which it shares bone for bone);
- bone names, their paths below the armature (the hierarchy) and the body's bone order must be the codex's exactly;
- rest positions must match to the millimetre when the codex measured this very prefab. Another prefab of a rig has
  the rig's names and order but may have bones of its own lengths in another pose: the game's Skeleton stands in a
  T-pose, 1.99 m, its spine starting 12 cm higher and its hips 28 cm wide, where the player stands in an A-pose with
  hips 20 cm wide. Its positions are checked in Unity instead, against the game prefab itself (GameRigCheck);
- the armature node's scale must be the codex's armature_scale on the rig's own prefab.
"""
import json
import os

import numpy as np

RIGS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "codex", "data", "rigs.json"))
TOLERANCE_M = 0.002          # rest_m is written to the millimetre


def check(skeleton):
    """A dict of findings; raises SystemExit when the skeleton and the codex disagree."""
    sample = _sample(skeleton)
    if sample is None:
        return {"rig": None, "note": "not measured in codex/data/rigs.json"}
    ours = {skeleton.name(n): _unity(skeleton.world(n).translation) for n in skeleton.nodes if n in skeleton.bones}
    theirs = {b["bone"]: np.array(b["rest_m"]) for b in sample["bones"]}
    problems = _names(skeleton, sample, ours)
    same = sample["prefab"] == skeleton.source
    worst = max(float(np.abs(ours[n] - theirs[n]).max()) for n in ours if n in theirs) if same else None
    if same and worst > TOLERANCE_M:
        problems.append(f"rest positions off by up to {worst * 1000:.1f} mm")
    if same and abs(skeleton.local(skeleton.armature)[2][0] - sample["armature_scale"][0]) > 1e-3:
        problems.append(f"armature scale {skeleton.local(skeleton.armature)[2][0]} against {sample['armature_scale'][0]}")
    if problems:
        raise SystemExit("gamerig: the prefab and codex/data/rigs.json disagree: " + "; ".join(problems))
    return {"rig": sample["rig"], "measured_on": sample["prefab"], "bones": len(ours), "names_paths_order": "same",
            "positions": f"within {worst * 1000:.1f} mm" if same else "not compared: the codex measured another prefab"}


def _sample(skeleton):
    """The rigs.json sample for this prefab, or for the creature it is named after."""
    with open(RIGS, encoding="utf-8") as handle:
        data = json.load(handle)
    samples = [s for category in data["categories"].values() for s in category["samples"]]
    own = [s for s in samples if s["prefab"] == skeleton.source]
    if own:
        return own[0]
    creature = skeleton.name(skeleton.tree.root())
    return next((s for s in samples if creature in s.get("creatures", [])), None)


def _names(skeleton, sample, ours):
    """Bone names, their paths below the armature (the hierarchy) and the body's bone order against the codex's."""
    problems = []
    paths = {b["bone"]: b["path"] for b in sample["bones"]}
    if set(ours) != set(paths):
        problems.append(f"bones differ: {sorted(set(ours) ^ set(paths))}")
    wrong = [skeleton.name(n) for n in skeleton.bones
             if skeleton.name(n) in paths and not skeleton.path(n).endswith(paths[skeleton.name(n)])]
    if wrong:
        problems.append(f"bones under other parents: {wrong}")
    if skeleton.order != sample["bone_order"]:
        problems.append("the body's bone order differs from the codex's bone_order")
    return problems


def _unity(v):
    """Blender (x, y, z) -> Unity (-x, z, -y)."""
    return np.array([-v.x, v.z, -v.y])
