"""The roots of the six long tentacles: thick arms that emerge from under the crown, wrap a little round the column
as they run down it, half sunk into it, and sink back into its tapering lower end. They suggest where the long
tentacles grow from; the markers kh_tentacle_0..5 sit on their centre lines at y -3.5 (on kh_body_3), where the mod
attaches the long tentacles while the kraken swims. Suckers along their flanks exist as paint only.

Unity coordinates; every vertex takes the column's weights at its own height (body.body_weights), so the roots bend
and lean exactly as the skin under them does.
"""
import math

import body
from geo import U, Part, frame, monotone, smoothstep, to_unity
from suckers import cup

TENTACLE_YAWS = (60, 110, 160, 200, 250, 300)     # from +Z towards +X, degrees
MARKER_Y = -3.5
TOP, BOTTOM, STEP = 0.62, -4.9, 0.3
TWIST = 34.0                                      # degrees further round at the top than at MARKER_Y
RADIUS = monotone([(-4.9, 0.12), (-4.5, 0.30), (-3.5, 0.38), (-2.0, 0.44), (-0.4, 0.48), (0.62, 0.32)])
SEGMENTS = 10


def yaw_at(k, y):
    return math.radians(TENTACLE_YAWS[k] + TWIST * (y - MARKER_Y) / (TOP - MARKER_Y))


def centre(k, y):
    """Unity point on root k's centre line at height y: sunk into the skin at its two ends, half out between."""
    yaw = yaw_at(k, y)
    surface, out = body.surface_along(y, yaw)
    distance = (surface - body.axis_point(y)).length
    r = RADIUS(y)
    out_of_skin = min(smoothstep(0.55, -0.45, y), smoothstep(BOTTOM, BOTTOM + 0.6, y))
    inset = 0.25 * r * out_of_skin + (r + 0.12) * (1 - out_of_skin)
    return body.axis_point(y) + out * (distance - inset)


def root_frame(k, y):
    """Blender (S, N, T): T down the root, N facing out from the column (the root's back), S = N x T."""
    tangent = U(*(centre(k, y - 0.01) - centre(k, y + 0.01)))
    yaw = yaw_at(k, y)
    return frame(tangent, U(math.sin(yaw), 0.0, math.cos(yaw)))


def heights():
    count = int(round((TOP - BOTTOM) / STEP))
    return [TOP + (BOTTOM - TOP) * i / count for i in range(count + 1)]


def weights(p, a=None):
    """The column's weights at a Blender point's own height."""
    return body.body_weights(to_unity(p)[1])


def root_mesh(k):
    part = Part(f"root_{k}")
    rings = []
    for y in heights():
        s, n, _ = root_frame(k, y)
        rings.append(part.ring(U(*centre(k, y)), s, n, RADIUS(y), SEGMENTS, weights,
                               under=lambda p, a: 0.8 * smoothstep(0.2, 0.9, -math.sin(a)),
                               dorsal=lambda p, a: 0.4 * max(0.0, math.sin(a))))
    for a, b in zip(rings, rings[1:]):
        part.strip(a, b)
    top = U(*centre(k, TOP)) + root_frame(k, TOP)[2] * -0.1
    bottom = U(*centre(k, BOTTOM)) + root_frame(k, BOTTOM)[2] * 0.08
    part.cap_start(rings[0], part.vert(top, weights(top)))
    part.cap_end(rings[-1], part.vert(bottom, weights(bottom)))
    return part


def root_suckers(k):
    """Paint-only suckers in a row down each flank of the root (for the bake mesh)."""
    part = Part(f"root_{k}_suckers", closed=False)
    for side in (-1.0, 1.0):
        y = TOP - 0.55 - (0.12 if side > 0 else 0.0)
        while y > BOTTOM + 0.5:
            r = RADIUS(y)
            s, n, t = root_frame(k, y)
            a = 1.5 * math.pi + side * 1.15
            normal = (s * math.cos(a) + n * math.sin(a)).normalized()
            point = U(*centre(k, y)) + normal * r
            cup(part, point, normal, t, 0.24 * r, 10, weights, True)
            y -= 2.5 * 0.24 * r
    return part


def markers():
    """(name, parent, Unity position) of kh_tentacle_0..5: on the roots' centre lines at MARKER_Y, on kh_body_3."""
    return [(f"kh_tentacle_{k}", "kh_body_3", tuple(round(c, 4) for c in centre(k, MARKER_Y)))
            for k in range(len(TENTACLE_YAWS))]
