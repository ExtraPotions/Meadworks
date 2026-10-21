"""The kraken's long tentacle, ecp_kraken_tentacle: straight along Unity +Z from the base at the origin to z = 8.0,
suckers in two alternating rows on the underside (Unity -Y), 16 bones kt_00..kt_15 every 0.5 m plus kt_end at the tip.

Each vertex follows the two bones whose midpoints it lies between, blended linearly by its distance along the tentacle,
so the skin bends smoothly at every joint.
"""
import math

from geo import U, Part, monotone, smoothstep
from suckers import cup, rows

LENGTH, BONES, BONE = 8.0, 16, 0.5
SEGMENTS, STEP = 16, 0.17
FLAT = 0.8                    # the underside is flattened to this fraction of the radius
ROW_ANGLE = 0.42              # the two sucker rows sit this many radians either side of the underside's middle
SUCKER = 0.27                 # sucker radius as a fraction of the tentacle's radius
LOW_CUP = 0.046               # suckers at least this big are modelled on the game mesh; smaller ones are paint only
RADIUS = monotone([(0.0, 0.50), (0.15, 0.40), (0.35, 0.28), (0.60, 0.17), (0.85, 0.09), (1.0, 0.035)])


def radius(u):
    return RADIUS(u)


def bone_name(i):
    return f"kt_{i:02d}"


def rig():
    """(name, parent, Unity position) for the bones, and the markers (no skin) the same way."""
    bones = [(bone_name(i), bone_name(i - 1) if i else "", (0.0, 0.0, BONE * i)) for i in range(BONES)]
    markers = [("kt_end", bone_name(BONES - 1), (0.0, 0.0, LENGTH))]
    return bones, markers


def weights_at(z):
    """The two bones either side of z, blended linearly between their midpoints."""
    f = min(max(z / BONE - 0.5, 0.0), BONES - 1.0)
    i = min(int(f), BONES - 2)
    t = f - i
    return {bone_name(i): 1.0 - t, bone_name(i + 1): t} if t > 1e-4 else {bone_name(i): 1.0}


def _z_of(p):
    return -p.y                      # Blender -Y is Unity +Z


def _vertex_weights(p, a=None):
    return weights_at(_z_of(p))


# Blender frame of the straight tentacle: T along it (Unity +Z), N up (Unity +Y), S = N x T.
T, N = U(0, 0, 1), U(0, 1, 0)
S = N.cross(T)


def _shape(r):
    return lambda a: (r * 1.04, r * (FLAT if math.sin(a) < 0 else 1.0))


def _paint(z):
    u = z / LENGTH
    return {"under": lambda p, a: smoothstep(0.5, 0.93, -math.sin(a)),
            "dorsal": lambda p, a: max(0.0, math.sin(a)), "tip": u}


def body():
    """The tube: rings every STEP metres, a shallow domed base, a rounded tip that ends exactly at LENGTH."""
    part = Part("tentacle_body")
    tip_r = radius(1.0 - 0.035 / LENGTH)
    end = LENGTH - tip_r
    count = int(math.ceil(end / STEP))
    zs = [end * i / count for i in range(count + 1)]
    rings = [part.ring(U(0, 0, z), S, N, _shape(radius(z / LENGTH)), SEGMENTS, _vertex_weights, **_paint(z)) for z in zs]
    base_inner = part.ring(U(0, 0, -0.03), S, N, _shape(0.33), SEGMENTS, _vertex_weights, **_paint(0.0))
    part.strip(base_inner, rings[0])
    part.cap_start(base_inner, part.vert(U(0, 0, -0.05), weights_at(0.0), under=0.5, tip=0.0))
    for a, b in zip(rings, rings[1:]):
        part.strip(a, b)
    last = rings[-1]
    for angle in (35.0, 65.0):
        z = end + tip_r * math.sin(math.radians(angle))
        ring = part.ring(U(0, 0, z), S, N, _shape(tip_r * math.cos(math.radians(angle))), SEGMENTS,
                         _vertex_weights, **_paint(z))
        part.strip(last, ring)
        rings.append(ring)
        last = ring
    pole = part.vert(U(0, 0, LENGTH), weights_at(LENGTH), tip=1.0, dorsal=0.5)
    part.cap_end(last, pole)
    _seams(part, rings, zs, pole)
    return part


def _seams(part, rings, zs, pole):
    """UV cuts: round the tube at a few lengths (and at the base cap), and along the underside's middle."""
    bottom = SEGMENTS * 3 // 4
    for cut in (0.0, 1.5, 3.25, 5.25):
        ring = rings[min(range(len(zs)), key=lambda k: abs(zs[k] - cut))]
        part.seams += [(ring[i], ring[(i + 1) % SEGMENTS]) for i in range(SEGMENTS)]
    part.seams += [(a[bottom], b[bottom]) for a, b in zip(rings, rings[1:])]
    part.seams.append((rings[-1][bottom], pole))


def surface(z, a):
    """Point and outward normal (Blender) on the skin at distance z and ring angle a."""
    r = radius(z / LENGTH)
    rs, rn = _shape(r)(a)
    point = U(0, 0, z) + S * (rs * math.cos(a)) + N * (rn * math.sin(a))
    normal = (S * (math.cos(a) / rs) + N * (math.sin(a) / rn)).normalized()
    return point, normal


def suckers(detailed):
    """Every sucker for the bake mesh (detailed); only the big ones, simpler, for the game mesh."""
    part = Part("tentacle_suckers" + ("_detail" if detailed else ""), closed=False)
    bottom = 1.5 * math.pi
    count = 0
    for u, row in rows(0.05, 0.97, LENGTH, lambda u: SUCKER * radius(u)):
        rho = SUCKER * radius(u)
        if not detailed and rho < LOW_CUP:
            continue
        point, normal = surface(u * LENGTH, bottom + (ROW_ANGLE if row else -ROW_ANGLE))
        segments = (12 if rho > 0.04 else 8) if detailed else (10 if rho > 0.085 else 8 if rho > 0.06 else 6)
        cup(part, point, normal, T, rho, segments, _vertex_weights, detailed)
        count += 1
    return part, count


def build():
    """Returns (game parts, bake parts, bones, markers, notes)."""
    skin = body()
    low_cups, low_count = suckers(detailed=False)
    high_cups, high_count = suckers(detailed=True)
    bones, markers = rig()
    notes = {"suckers": high_count, "modelled suckers": low_count}
    return [skin, low_cups], [body(), high_cups], bones, markers, notes
