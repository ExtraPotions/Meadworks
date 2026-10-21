"""An all-bone spine: broad vertebral bodies, rear crests and paired side processes."""
import math
from mathutils import Vector
import axe_mesh as geo
from workshop import paint


def build(point):
    bone = paint.bone('Worn_haft_bone',
                      tones=((.12, .083, .047), (.30, .235, .15), (.46, .38, .26)),
                      edges=0, grain=None, pattern=None, relief=0,
                      density=65, blotch_m=.075, rough=.76)
    joint = paint.bone('Recessed_bone_joints',
                       tones=((.06, .035, .017), (.13, .082, .041), (.23, .16, .09)),
                       edges=0, grain=None, pattern=None, relief=0, rough=.9)

    # A continuous dark core joins the bones without the old floating hinge pieces.
    zs = [.026 + i * 1.17 / 12 for i in range(13)]
    geo.sweep('Spinal_core', [point(z) for z in zs],
              [(.019, .018)] * len(zs), joint, hint=(0, 1, 0), sides=6)

    # Twelve contiguous vertebrae. Every segment has a rear crest and paired
    # transverse processes, shortened at the fists without hiding the bones.
    grips = ((.08, .052), (.34, .077), (.73, .077))
    for i in range(12):
        z = .071 + i * .0955
        width = .029 + .008 * (i / 11) + .002 * math.sin(i * 1.9)
        in_grip = any(abs(z - centre) < half + .025 for centre, half in grips)
        if in_grip:
            width = .026
        stations = []
        for dz, scale in ((-.045, .90), (-.030, 1.12), (.006, .77), (.043, 1.04)):
            p = point(z + dz)
            stations.append((p.z, width * scale, width * scale * .83, p.x, p.y))
        geo.loft_z('Haft_bone_%02d' % i, stations, bone, sides=6, smooth=False)
        p = point(z)
        reach = .032 if in_grip else .046 + .017 * (i / 11)
        geo.sweep('Spinal_crest_%02d' % i,
                  [p + Vector((-.016, 0, .024)),
                   p + Vector((-reach, 0, -.002)),
                   p + Vector((-reach - .012, 0, -.032))],
                  [(.021, .024), (.014, .017), (.003, .004)],
                  bone, hint=(0, 1, 0), sides=4)
        for side in (-1, 1):
            spread = .030 if in_grip else .046 + .006 * (i / 11)
            geo.sweep('Transverse_%02d_%d' % (i, side),
                      [p + Vector((-.006, side * .014, .012)),
                       p + Vector((-.022, side * spread, -.005))],
                      [(.019, .019), (.006, .011)],
                      bone, hint=(1, 0, 0), sides=4)

    # An angular bone heel and a flared collar seat the unchanged axehead.
    geo.loft_z('Bone_heel', [(0, .007, .007, 0, 0),
                            (.022, .030, .025, 0, 0),
                            (.049, .025, .022, 0, 0)], bone, sides=6, smooth=False)
    zs = [1.13, 1.16, 1.20, 1.225]
    geo.sweep('Head_seat', [point(z) for z in zs],
              [(.027, .029), (.032, .035), (.040, .041), (.034, .035)],
              bone, hint=(0, 1, 0), sides=6)
