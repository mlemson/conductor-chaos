"""Check that the Unity FBX and preview GLB have the same usable door step."""
import json
import re
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[2]
fbx = (root / 'Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx').read_text(encoding='utf-8')
block = re.search(r'^\tGeometry: \d+, "Geometry::DoorSill", "Mesh" \{\n.*?^\t\}\n', fbx, re.MULTILINE | re.DOTALL)
assert block and '"Model::DoorSill"' in fbx, 'Wagon must keep its named step mesh and node'
vertices = re.search(r'Vertices: \*\d+ \{\s*a: ([^\n]+)', block.group())
assert vertices, 'FBX step vertices are missing'
numbers = [float(value) for value in vertices.group(1).split(',')]
assert len(numbers) % 3 == 0
fbx_z = numbers[2::3]

raw = (root / 'ArtSource/Exports/RegionalWagon_Blockout.glb').read_bytes()
magic, version, total = struct.unpack_from('<III', raw)
assert (magic, version, total) == (0x46546C67, 2, len(raw))
json_length, json_type = struct.unpack_from('<II', raw, 12)
assert json_type == 0x4E4F534A
doc = json.loads(raw[20:20 + json_length])
binary_offset = 20 + json_length
binary_length, binary_type = struct.unpack_from('<II', raw, binary_offset)
assert binary_type == 0x004E4942 and binary_offset + 8 + binary_length == len(raw)
binary_offset += 8
node = next(n for n in doc['nodes'] if n['name'] == 'DoorSill')
glb_z = []
for primitive in doc['meshes'][node['mesh']]['primitives']:
    accessor = doc['accessors'][primitive['attributes']['POSITION']]
    view = doc['bufferViews'][accessor['bufferView']]
    assert accessor['componentType'] == 5126 and accessor['type'] == 'VEC3'
    start = binary_offset + view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
    stride = view.get('byteStride', 12)
    glb_z.extend(struct.unpack_from('<fff', raw, start + i * stride)[2] for i in range(accessor['count']))

for label, values in (('FBX', fbx_z), ('GLB', glb_z)):
    assert abs(min(values) + 0.14) < 0.001, f'{label}: step back edge changed'
    assert abs(max(values) - 0.32) < 0.001, f'{label}: step does not extend far enough'
print('PASS regional wagon step: FBX and GLB both extend 0.32 m toward the platform')
