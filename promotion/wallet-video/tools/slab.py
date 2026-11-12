"""slab.py <out.gltf> <width> <height> <depth> <corner> <edge> <r> <g> <b> <metallic> <roughness> [open]

A device body: a rounded rectangle with depth, its edge rounded, as one glTF mesh with a PBR material.
Units are the comp's pixels, the body centred on the origin, its faces looking down -z and +z.
"""
import base64
import json
import math
import struct
import sys

out = sys.argv[1]
W, H, D, R, E = (float(v) for v in sys.argv[2:7])
color = [float(v) for v in sys.argv[7:10]] + [1.0]
metallic, roughness = float(sys.argv[10]), float(sys.argv[11])
# An OPEN front: no cap where the screen goes, so nothing of the body stands in front of it.
OPEN_FRONT = len(sys.argv) > 12 and sys.argv[12] == "open"
CORNER_STEPS, EDGE_STEPS = 24, 8

# The outline: the four corner arcs, each point with its outward direction.
outline = []
corners = [(W / 2 - R, H / 2 - R, 0.0), (-(W / 2 - R), H / 2 - R, 90.0),
           (-(W / 2 - R), -(H / 2 - R), 180.0), (W / 2 - R, -(H / 2 - R), 270.0)]
for cx, cy, start in corners:
    for i in range(CORNER_STEPS + 1):
        a = math.radians(start + 90.0 * i / CORNER_STEPS)
        outline.append((cx, cy, math.cos(a), math.sin(a)))

# The rings along the depth: a quarter circle at the front edge, the flat side, a quarter at the back.
rings = []
for i in range(EDGE_STEPS + 1):
    a = math.pi / 2 * i / EDGE_STEPS
    rings.append((-D / 2 + E - E * math.cos(a), E - E * math.sin(a), math.sin(a), -math.cos(a)))
for i in range(EDGE_STEPS + 1):
    a = math.pi / 2 + math.pi / 2 * i / EDGE_STEPS
    rings.append((D / 2 - E - E * math.cos(a), E - E * math.sin(a), math.sin(a), -math.cos(a)))

positions, normals, indices = [], [], []


def vertex(p, n):
    positions.append(p)
    normals.append(n)
    return len(positions) - 1


def ring_point(point, ring):
    cx, cy, dx, dy = point
    z, inset, side, nz = ring
    radius = R - inset
    return (cx + radius * dx, cy + radius * dy, z), (dx * side, dy * side, nz)


grid = [[vertex(*ring_point(point, ring)) for point in outline] for ring in rings]
count = len(outline)
for r in range(len(rings) - 1):
    for i in range(count):
        j = (i + 1) % count
        a, b, c, d = grid[r][i], grid[r][j], grid[r + 1][j], grid[r + 1][i]
        indices += [a, d, c, a, c, b]

# The caps, fanned from their centre, each with its flat normal.
caps = ((rings[-1], 1.0, True),) if OPEN_FRONT else ((rings[0], -1.0, False), (rings[-1], 1.0, True))
for ring, nz, flip in caps:
    centre = vertex((0.0, 0.0, ring[0]), (0.0, 0.0, nz))
    edge = [vertex(ring_point(point, ring)[0], (0.0, 0.0, nz)) for point in outline]
    for i in range(count):
        j = (i + 1) % count
        indices += [centre, edge[j], edge[i]] if not flip else [centre, edge[i], edge[j]]

position_bytes = b"".join(struct.pack("<3f", *p) for p in positions)
normal_bytes = b"".join(struct.pack("<3f", *n) for n in normals)
index_bytes = b"".join(struct.pack("<I", i) for i in indices)
buffer = position_bytes + normal_bytes + index_bytes
xs, ys, zs = zip(*positions)
gltf = {
    "asset": {"version": "2.0", "generator": "slab.py"},
    "scene": 0,
    "scenes": [{"nodes": [0]}],
    "nodes": [{"mesh": 0}],
    "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1}, "indices": 2, "material": 0}]}],
    "materials": [{"pbrMetallicRoughness": {"baseColorFactor": color, "metallicFactor": metallic,
                                             "roughnessFactor": roughness}, "doubleSided": True}],
    "buffers": [{"byteLength": len(buffer),
                 "uri": "data:application/octet-stream;base64," + base64.b64encode(buffer).decode()}],
    "bufferViews": [
        {"buffer": 0, "byteOffset": 0, "byteLength": len(position_bytes), "target": 34962},
        {"buffer": 0, "byteOffset": len(position_bytes), "byteLength": len(normal_bytes), "target": 34962},
        {"buffer": 0, "byteOffset": len(position_bytes) + len(normal_bytes), "byteLength": len(index_bytes),
         "target": 34963},
    ],
    "accessors": [
        {"bufferView": 0, "componentType": 5126, "count": len(positions), "type": "VEC3",
         "min": [min(xs), min(ys), min(zs)], "max": [max(xs), max(ys), max(zs)]},
        {"bufferView": 1, "componentType": 5126, "count": len(normals), "type": "VEC3"},
        {"bufferView": 2, "componentType": 5125, "count": len(indices), "type": "SCALAR"},
    ],
}
json.dump(gltf, open(out, "w"))
print(out, len(positions), "vertices", len(indices) // 3, "triangles")
