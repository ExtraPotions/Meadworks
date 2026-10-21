# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Semantic checks for the original raid-chest mesh source; no renderer or game data."""
import copy
import json
import math
from pathlib import Path
import unittest


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def bounds(points):
    return ([min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)])


def check_interior_and_bands(data):
    meshes = {m['name']: m for m in data['meshes']}
    body = meshes['body_mesh']
    vertices = list(zip(*[iter(body['vertices'])]*3))
    # Downward rays through the opening must see the wooden floor, not a sealed cap.
    for x in (-.4, 0, .4):
        for z in (-.25, 0, .25):
            hits = []
            for at in range(0, len(body['triangles']), 3):
                ids = body['triangles'][at:at+3]
                a,b,c = [vertices[i] for i in ids]
                determinant = (b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
                if abs(determinant) < 1e-10: continue
                u = ((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/determinant
                v = ((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/determinant
                if min(u,v,1-u-v) >= -1e-8:
                    hits.append((u*a[1]+v*b[1]+(1-u-v)*c[1], int(body['uv'][ids[0]*2]*4)))
            assert hits and .15 < max(hits)[0] < .3, 'body opening must expose the floor'
            assert max(hits)[1] == 0, 'interior floor must use wood atlas tile'
    lid = meshes['lid_mesh']
    for at in range(0, len(lid['vertices']), 3):
        vertex = at//3
        if int(lid['uv'][vertex*2]*4) == 1 and abs(lid['normals'][at+2]) > .99:
            z = lid['vertices'][at+2]
            assert min(abs(z),abs(z-1.12)) >= .008, 'lid band must clear wooden end faces'


def check(data):
    assert data['asset'] == 'mw_raid_chest_model'
    nodes = {n['name']: n for n in data['nodes']}
    assert len(nodes) == len(data['nodes']), 'duplicate node'
    assert nodes['lid']['parent'] == 'model' and nodes['horn']['parent'] == 'lid', 'moving hierarchy'
    assert nodes['horn_mouth']['parent'] == 'horn', 'socket follows horn'
    assert nodes['lid']['rotation'] == [0, 0, 0], 'closed hinge rotation'
    assert nodes['lid']['position'] == [0, .86, -.59], 'hinge location'
    for node in nodes.values():
        assert node['parent'] == '' or node['parent'] in nodes
        assert all(math.isfinite(x) for x in node['position'] + node['rotation'])
        seen = {node['name']}
        parent = node['parent']
        while parent:
            assert parent not in seen, 'hierarchy cycle'
            seen.add(parent)
            parent = nodes[parent]['parent']
    meshes = {m['name']: m for m in data['meshes']}
    assert set(meshes)=={'body_mesh','lid_mesh','horn_mesh'} and len(data['meshes'])==3
    count = 0
    for mesh in meshes.values():
        v, normals, uv, triangles = mesh['vertices'], mesh['normals'], mesh['uv'], mesh['triangles']
        assert len(v) % 3 == 0 and len(normals) == len(v) and len(uv) == len(v)//3*2
        assert all(math.isfinite(x) for x in v + normals + uv)
        assert all(0 <= x <= 1 for x in uv), 'atlas coordinates'
        points = list(zip(*[iter(v)]*3))
        assert len(triangles) % 3 == 0
        for i in range(0, len(triangles), 3):
            ids = triangles[i:i+3]
            assert all(type(x) is int and 0 <= x < len(points) for x in ids), 'triangle index'
            a,b,c = [points[x] for x in ids]
            normal = cross([b[j]-a[j] for j in range(3)], [c[j]-a[j] for j in range(3)])
            assert sum(x*x for x in normal) > 1e-16, 'degenerate triangle'
            assert sum(normal[j]*normals[ids[0]*3+j] for j in range(3)) > 0, 'winding/normal mismatch'
        assert all(abs(sum(x*x for x in normals[i:i+3])-1) < 1e-5 for i in range(0,len(normals),3)), 'unit normals'
        count += len(triangles)//3
    assert 100 < count < 8000, 'triangle budget'
    check_interior_and_bands(data)
    assert len(data['boxes']) == 3 and len({b['name'] for b in data['boxes']}) == 3, 'three root boxes'
    for box in data['boxes']:
        assert all(math.isfinite(x) for x in box['center'] + box['size']) and all(x > 0 for x in box['size'])
    def world_points(name, angle=0):
        node = nodes[name]
        mesh = meshes[node['mesh']]
        points = list(zip(*[iter(mesh['vertices'])]*3))
        if name in ('lid','horn'):
            c,s = math.cos(math.radians(angle)),math.sin(math.radians(angle))
            points = [(x,c*y-s*z,s*y+c*z) for x,y,z in points]
            h = nodes['lid']['position']
            points = [tuple(p[i]+h[i] for i in range(3)) for p in points]
        return points
    closed = world_points('body') + world_points('lid') + world_points('horn')
    low,high = bounds(closed)
    assert all(abs(a-b)<1e-6 for a,b in zip(low+high,data['boundsMin']+data['boundsMax'])), 'closed bounds'
    for point in closed:
        assert any(all(abs(point[i]-box['center'][i]) <= box['size'][i]/2 + 1e-5 for i in range(3)) for box in data['boxes']), 'closed collision enclosure'
    body_low,body_high = bounds(world_points('body'))
    for angle in range(0,-101,-1):
        for name in ('lid','horn'):
            lo,hi = bounds(world_points(name,angle))
            overlap = [min(hi[i],body_high[i])-max(lo[i],body_low[i]) for i in range(3)]
            assert not all(x>1e-5 for x in overlap), f'{name} intersects body at {angle}'
    mouth = nodes['horn_mouth']['position']
    assert sum((mouth[i]-data['hornMouth'][i])**2 for i in range(3)) < 1e-10
    assert mouth[1] > .25 and mouth[2] > 0, 'bell socket above lid'
    pitch,yaw,_ = [math.radians(v) for v in nodes['horn_mouth']['rotation']]
    forward=[math.sin(yaw)*math.cos(pitch),-math.sin(pitch),math.cos(yaw)*math.cos(pitch)]
    assert sum((forward[i]-data['hornDirection'][i])**2 for i in range(3))<1e-10, 'bell faces horn tangent'
    return count


class SourceTests(unittest.TestCase):
    def setUp(self):
        import chest_source
        self.generator = chest_source
        self.data = chest_source.build()

    def test_geometry_and_opening(self):
        self.assertGreater(check(self.data),100)

    def test_repeatable(self):
        self.assertEqual(self.generator.encoded(self.data),self.generator.encoded(self.generator.build()))

    def test_body_opening_exposes_wood_floor_and_bands_clear_lid(self):
        check_interior_and_bands(self.data)

    def test_rejects_sealed_body_top(self):
        cap = self.generator.Mesh('body_mesh')
        cap.data = copy.deepcopy(self.data['meshes'][0])
        cap.box([0,.80,0],[1.52,.065,1.12],1)
        data = copy.deepcopy(self.data);data['meshes'][0] = cap.data
        with self.assertRaisesRegex(AssertionError,'body opening'):
            check_interior_and_bands(data)

    def test_rejects_coplanar_lid_bands(self):
        data = copy.deepcopy(self.data);lid = data['meshes'][1]
        for at in range(0,len(lid['vertices']),3):
            if int(lid['uv'][at//3*2]*4) == 1:
                if abs(lid['vertices'][at+2]) < .02: lid['vertices'][at+2] = 0
                if abs(lid['vertices'][at+2]-1.12) < .02: lid['vertices'][at+2] = 1.12
        with self.assertRaisesRegex(AssertionError,'lid band'):
            check_interior_and_bands(data)

    def test_rejects_broken_hierarchy_and_colliders(self):
        for change in ('socket','boxes','winding','hinge'):
            data = copy.deepcopy(self.data)
            if change == 'socket': next(n for n in data['nodes'] if n['name']=='horn_mouth')['parent']='model'
            if change == 'boxes': data['boxes'].pop()
            if change == 'winding': data['meshes'][0]['triangles'][0:3]=list(reversed(data['meshes'][0]['triangles'][0:3]))
            if change == 'hinge': next(n for n in data['nodes'] if n['name']=='lid')['position'][1]=.4
            with self.assertRaises(AssertionError,msg=change): check(data)

    def test_icon_is_editable_original_vector(self):
        import xml.etree.ElementTree as ET
        svg=Path(__file__).with_name('raid-chest.svg').read_bytes()
        root=ET.fromstring(svg)
        self.assertEqual(root.attrib['viewBox'],'0 0 256 256')
        self.assertGreater(len(root.findall('.//{http://www.w3.org/2000/svg}polygon')),5)
        self.assertNotIn(b'<image',svg)
        self.assertNotIn(b'href=',svg)


if __name__ == '__main__':
    unittest.main()
