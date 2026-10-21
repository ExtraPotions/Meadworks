"""Shared coarse bone paint, matched to the approved low-poly greataxe."""
from workshop import paint, materials, paint_nodes as nodes, regions


def bone(name='bone'):
    return paint.bone(name, tones=((.12,.083,.047),(.30,.235,.15),(.46,.38,.26)),
                      edges=0, grain=None, pattern=None, relief=0, density=65,
                      blotch_m=.075, rough=.76)


def vertebra(name='vertebra'):
    return bone(name)


def bone_blade(name='bone_blade'):
    mat = bone(name)
    bsdf = materials.principled(mat)
    colour = bsdf.inputs['Base Color'].links[0].from_socket
    attribute = nodes.node(mat, 'ShaderNodeAttribute')
    attribute.attribute_name = 'edge'
    edge = nodes.math(mat, 'POWER', attribute.outputs['Fac'], 2)
    nodes.link(mat, nodes.mix(mat, colour, (.60,.52,.38), edge), bsdf.inputs['Base Color'])
    return mat


def teeth(name='teeth'):
    return paint.bone(name, tones=((.20,.15,.085),(.43,.35,.23),(.60,.52,.38)),
                      edges=0, grain=None, pattern=None, relief=0, density=65)


def sinew(name='joint_bone'):
    # Kept as an API name for old model descriptions: the new material is bone.
    return paint.bone(name, tones=((.08,.05,.028),(.18,.12,.07),(.32,.24,.15)),
                      edges=0, grain=None, pattern=None, relief=0, density=65)


def leather(name='grip_bone'):
    return bone(name)


def feather(name='feather'):
    mat = materials.flat(name, (.065,.052,.035), roughness=.9)
    regions.mark(mat, 'base')
    return mat


def dark(name='hollow'):
    mat = materials.flat(name, (.035,.022,.012), roughness=.95)
    regions.mark(mat, 'bone', family='bone.bone')
    return mat
