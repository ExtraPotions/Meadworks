"""The kraken's motion, ported line for line from the mod (EliteCreaturesPack/Kraken/Motion and Body), for Blender
previews: tentacle poses (standing, reared, lying across a deck, trailing), blending, and the head's attack offsets.
Works in any right-handed or left-handed frame: poses are built in a frame's own axes (x out, y up, z side).
Only mathutils. Keep in step with the C# when either changes.
"""
import math

from mathutils import Vector

BONES = 16
POINTS = BONES + 1
SEGMENT = 0.5
LENGTH = BONES * SEGMENT
_AT = (0.0, 0.15, 0.35, 0.60, 0.85, 1.0)
_RADII = (0.50, 0.40, 0.28, 0.17, 0.09, 0.035)


def radius(u):
    u = min(1.0, max(0.0, u))
    for i in range(1, len(_AT)):
        if u <= _AT[i]:
            return _RADII[i - 1] + (_RADII[i] - _RADII[i - 1]) * (u - _AT[i - 1]) / (_AT[i] - _AT[i - 1])
    return _RADII[-1]


# ---- easing (Ease.cs)
def span(t, start, end):
    return min(1.0, max(0.0, (t - start) / max(end - start, 1e-4)))


def in_out(t):
    return t * t * (3.0 - 2.0 * t)


def smooth(a, b, x):
    return in_out(span(x, a, b))


def ease_in(t):
    return t * t * t


def ease_out(t):
    return 1.0 - (1.0 - t) * (1.0 - t)


def follow(rate, dt):
    return 1.0 - math.exp(-rate * dt)


# ---- frame (TentacleFrame.cs)
class Frame:
    def __init__(self, origin, outward, up, scale=1.0):
        self.up = up.normalized()
        level = outward - self.up * outward.dot(self.up)
        if level.length < 1e-6:
            level = Vector((0.0, 1.0, 0.0))
        self.out = level.normalized()
        self.side = self.out.cross(self.up)
        self.origin = origin.copy()
        self.scale = scale

    def world(self, local):
        return self.origin + (self.out * local.x + self.up * local.y + self.side * local.z) * self.scale


# ---- chain (TentacleChain.cs)
def lerp(a, b, t):
    return [p.lerp(q, t) for p, q in zip(a, b)]


def straighten(points, segment=SEGMENT):
    last = Vector((0.0, 0.0, 1.0))
    for i in range(1, len(points)):
        d = points[i] - points[i - 1]
        d = d if d.length_squared > 1e-8 else last
        last = d
        points[i] = points[i - 1] + d.normalized() * segment
    return points


def path_length(path):
    return sum((path[i] - path[i - 1]).length for i in range(1, len(path)))


def resample(path, frame):
    out = [frame.world(path[0])]
    at, nxt = path[0].copy(), 1
    for _ in range(1, POINTS):
        dist = SEGMENT
        while nxt < len(path):
            left = (path[nxt] - at).length
            if left >= dist:
                at = at + (path[nxt] - at).normalized() * dist
                break
            dist -= left
            at = path[nxt].copy()
            nxt += 1
        else:
            end = (path[-1] - path[-2]).normalized() if len(path) > 1 else Vector((0, 1, 0))
            at = at + end * dist
        out.append(frame.world(at))
    return out


# ---- curls (TentacleCurl.cs)
IDLE = dict(lean=-12.0, curl=115.0, curl_from=0.35, sway=24.0, side_sway=26.0, flick=42.0, base_depth=1.0, pace=1.8)
RAISED = dict(lean=-20.0, curl=160.0, curl_from=0.5, sway=3.0, side_sway=3.0, flick=10.0, base_depth=0.6, pace=1.0)
HOISTED = dict(lean=-40.0, curl=55.0, curl_from=0.3, sway=0.0, side_sway=0.0, flick=0.0, base_depth=0.6, pace=1.0)
FLUNG = dict(lean=-85.0, curl=25.0, curl_from=0.4, sway=0.0, side_sway=0.0, flick=0.0, base_depth=0.6, pace=1.0)


def shape_for(shape, index):
    a, b = math.sin(index * 12.9898) * 0.5, math.sin(index * 78.233 + 1.0) * 0.5
    out = dict(shape)
    out["lean"] += a * 8.0
    out["curl"] *= 1.0 + b * 0.2
    out["curl_from"] += a * 0.06
    out["flick"] *= 1.0 + a * 0.4
    return out


def straightened(shape, share):
    keep = 1.0 - min(1.0, max(0.0, share))
    out = dict(shape)
    out["lean"], out["curl"], out["flick"] = out["lean"] * keep, out["curl"] * keep, out["flick"] * keep
    return out


def curl(frame, shape, time, phase):
    at = Vector((0.0, -shape["base_depth"], 0.0))
    pts = [frame.world(at)]
    time *= shape.get("pace", 1.0) or 1.0
    for i in range(BONES):
        u = (i + 0.5) / BONES
        breathe = 1.0 + 0.12 * math.sin(time * 0.6 + phase * 2.1)
        restless = 0.6 * math.sin(time * 1.3 + phase + u * 3.0) + 0.4 * math.sin(time * 2.9 + phase * 3.1 + u * 5.0)
        bend = shape["lean"] + shape["curl"] * breathe * smooth(shape["curl_from"], 1.0, u)             + shape["sway"] * u * restless             + shape["flick"] * smooth(0.7, 1.0, u) * math.sin(time * 2.3 + phase * 1.9 - u * 6.0)
        side = shape["side_sway"] * u * (0.7 * math.sin(time * 0.9 + phase * 1.7 - u * 2.2) + 0.3 * math.sin(time * 2.1 + phase * 0.7))
        b, s = math.radians(bend), math.radians(side)
        at = at + Vector((math.sin(b) * math.cos(s), math.cos(b) * math.cos(s), math.sin(s))) * SEGMENT
        pts.append(frame.world(at))
    return pts


def emerge(frame, emerged, points):
    depth = (1.0 - min(1.0, max(0.0, emerged))) * (LENGTH + 1.5) * frame.scale
    return [p - frame.up * depth for p in points]


def rising(frame, shape, emerged, time, phase):
    e = min(1.0, max(0.0, emerged))
    return emerge(frame, e, curl(frame, straightened(shape, 1.0 - e * e), time, phase))


# ---- whip (TentacleWhip.cs)
def _angles(frame, d):
    x, y, z = d.dot(frame.out), d.dot(frame.up), d.dot(frame.side)
    return math.degrees(math.atan2(x, y)), math.degrees(math.atan2(z, math.sqrt(x * x + y * y)))


def _lerp_angle(a, b, t):
    delta = (b - a + 180.0) % 360.0 - 180.0
    return a + delta * min(1.0, max(0.0, t))


def whip(frame, a, b, t, lag):
    at = a[0].lerp(b[0], t)
    out = [at.copy()]
    for i in range(BONES):
        u = i / (BONES - 1)
        own = min(1.0, max(0.0, (t - lag * u) / max(1.0 - lag, 0.01)))
        ba, sa = _angles(frame, a[i + 1] - a[i])
        bb, sb = _angles(frame, b[i + 1] - b[i])
        bend, side = math.radians(_lerp_angle(ba, bb, own)), math.radians(sa + (sb - sa) * own)
        d = frame.out * (math.sin(bend) * math.cos(side)) + frame.up * (math.cos(bend) * math.cos(side)) + frame.side * math.sin(side)
        at = at + d * SEGMENT * frame.scale
        out.append(at.copy())
    return out


# ---- lying across a deck (TentacleDeck.cs)
WATER = -0.25


class Deck:
    """Heights of the ship's top along the strike line, every `step` metres out from the base, above the waterline."""

    def __init__(self, step, heights):
        self.step, self.heights = step, heights

    def height(self, x):
        if not self.heights:
            return WATER
        h = self.heights[max(0, min(len(self.heights) - 1, round(x / self.step)))]
        return WATER if h is None else h

    def rail(self):
        found = [h for i, h in enumerate(self.heights) if h is not None and i * self.step <= 2.5]
        return max(found) if found else 0.4


def deck_run(frame, deck, reach):
    arc, steps, clearance, min_under, max_drop = 0.7, 6, 0.08, 0.8, 1.1
    top = max(deck.rail() + radius(0.25) + clearance, 0.1)
    path = [Vector((0, 0, 0)), Vector((0, top, 0))]
    for i in range(1, steps + 1):
        a = math.pi - i * (math.pi / 2) / steps
        path.append(Vector((arc + arc * math.cos(a), top + arc * math.sin(a), 0)))
    used, last, along = path_length(path), path[-1], 0.0
    while along < reach and used < LENGTH - min_under:
        x = last.x + deck.step
        lying = deck.height(x) + radius((used + min_under) / LENGTH) * 0.8 + clearance
        point = Vector((x, max(lying, last.y - max_drop * deck.step), 0))
        used += (point - last).length
        path.append(point)
        last = point
        along += deck.step
    under = max(min_under, LENGTH - path_length(path))
    return resample([Vector((0, -under, 0))] + path, frame)


def writhe(frame, amount, time, points):
    if amount <= 0:
        return points
    out = [points[0]]
    for i in range(1, len(points)):
        u = i / BONES
        shift = 0.14 * amount * smooth(0.4, 1.0, u) * math.sin(time * 4.5 - u * 9.0)
        out.append(points[i] + frame.side * shift * frame.scale)
    return out


# ---- away from a ship (TentacleSwim.cs)
def trail(frame, time, phase):
    at = Vector((0, 0, 0))
    pts = [frame.world(at)]
    for i in range(BONES):
        u = (i + 0.5) / BONES
        pitch = math.radians(-9 + 12 * math.sin(time * 3 - u * 5 + phase))
        yaw = math.radians(20 * math.sin(time * 2.2 - u * 4 + phase * 1.3))
        at = at + Vector((math.cos(pitch) * math.cos(yaw), math.sin(pitch), math.cos(pitch) * math.sin(yaw))) * SEGMENT
        pts.append(frame.world(at))
    return pts


# ---- a tentacle strike's timeline (TentacleStrike.cs); kind: "slam", "smash" or "grab"
RAISE = 0.6


def hold_of(kind):
    return 1.5 if kind == "grab" else 1.0


def impact_of(kind):
    return hold_of(kind) + 0.2


def release_of(kind):
    return impact_of(kind) + (0.8 if kind == "grab" else 1.2)


def end_of(kind):
    return hold_of(kind) + (2.1 if kind == "grab" else 2.0)


def reared(frame, kind, index, time):
    shape = shape_for(RAISED, index)
    if kind == "grab":
        shape.update(sway=7.0, side_sway=7.0, pace=7.0, curl=shape["curl"] + 12.0)
    return curl(frame, shape, time, index)


def strike(age, frame, kind, index, start_pose, lying, rest, time):
    hold, impact, release, end = hold_of(kind), impact_of(kind), release_of(kind), end_of(kind)
    if age < hold:
        return whip(frame, start_pose, reared(frame, kind, index, time), in_out(span(age, 0, RAISE)), 0.3)
    if age < release:
        amount = span(age, impact, impact + 0.3) * (1.0 - span(age, release - 0.3, release))
        pose = whip(frame, reared(frame, kind, index, time), writhe(frame, amount, time, lying), ease_in(span(age, hold, impact)), 0.45)
        if kind == "grab" and age >= impact:
            pose = whip(frame, pose, curl(frame, HOISTED, time, index), in_out(span(age, impact, release)), 0.2)
        return pose
    if kind != "grab":
        return whip(frame, lying, rest, in_out(span(age, release, end)), 0.3)
    fling = release + 0.3
    if age < fling:
        return whip(frame, curl(frame, HOISTED, time, index), curl(frame, FLUNG, time, index), ease_out(span(age, release, fling)), 0.3)
    return whip(frame, curl(frame, FLUNG, time, index), rest, in_out(span(age, fling, end)), 0.3)


# ---- the head's attacks (HeadAction.cs): raise, forward, lean, beak, siphon, turn
BITE_WINDUP, BITE_SNAP, BITE_HOLD, BITE_END = 0.55, 0.8, 1.1, 1.8
INK_WINDUP, INK_SQUEEZED, INK_HOLD, INK_END = 1.0, 1.3, 1.6, 2.2
REACH, MIN_LUNGE, MAX_LUNGE = 2.2, 1.0, 5.5
REST = dict(raise_=0.0, forward=0.0, lean=0.0, beak=0.0, siphon=0.0, turn=0.0)
BITE_REARED = dict(raise_=1.0, forward=-0.4, lean=-12.0, beak=1.0, siphon=0.0, turn=1.0)
BITE_LUNGED = dict(raise_=2.2, forward=2.2, lean=25.0, beak=0.0, siphon=0.0, turn=1.0)
INK_REARED = dict(raise_=1.3, forward=-0.6, lean=-18.0, beak=1.0, siphon=0.0, turn=1.0)
INK_SQUIRTED = dict(raise_=1.3, forward=-0.2, lean=-6.0, beak=0.85, siphon=0.0, turn=1.0)


def mix(a, b, t):
    return {k: a[k] + (b[k] - a[k]) * t for k in a}


def lunged(lunge):
    extra = max(0.0, lunge - REACH)
    out = dict(BITE_LUNGED)
    out["forward"] = lunge
    out["raise_"] += extra * 0.4
    out["lean"] = min(BITE_LUNGED["lean"] + extra * 4.0, 40.0)
    return out


def bite(age, lunge=REACH):
    if age < BITE_WINDUP:
        return mix(REST, BITE_REARED, in_out(span(age, 0, BITE_WINDUP)))
    if age < BITE_HOLD:
        out = mix(BITE_REARED, lunged(lunge), ease_out(span(age, BITE_WINDUP, BITE_SNAP)))
        out["beak"] = 1.0 - span(age, BITE_SNAP - 0.08, BITE_SNAP)
        return out
    return mix(lunged(lunge), REST, in_out(span(age, BITE_HOLD, BITE_END)))


def ink(age):
    if age < INK_WINDUP:
        return mix(REST, INK_REARED, in_out(span(age, 0, INK_WINDUP)))
    if age < INK_HOLD:
        return mix(INK_REARED, INK_SQUIRTED, ease_out(span(age, INK_WINDUP, INK_SQUEEZED)))
    return mix(INK_SQUIRTED, REST, in_out(span(age, INK_HOLD, INK_END)))
