"""Check the placeable platform's Unity object references and walking colliders."""
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
prefab = root / 'Assets/Art/Prefabs/Perron_6m.prefab'
text = prefab.read_text(encoding='utf-8')
objects = re.findall(r'^--- !u!(\d+) &(-?\d+)', text, re.MULTILINE)
ids = [file_id for _, file_id in objects]
assert len(ids) == len(set(ids)), 'Duplicate Unity file IDs'
known = set(ids)
for reference in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}', text):
    file_id, suffix = reference.groups()
    if file_id != '0' and 'guid:' not in suffix:
        assert file_id in known, f'Missing local object {file_id}'

assert sum(kind == '1' for kind, _ in objects) == 13, 'Platform must have one root and twelve parts'
assert sum(kind == '23' for kind, _ in objects) == 12, 'Visible parts must have renderers'
assert sum(kind == '65' for kind, _ in objects) == 5, 'Platform and four steps must have colliders'
assert 'm_Size: {x: 6, y: 1.1, z: 3}' in text, 'Main walking surface has the wrong dimensions'
for step in range(1, 5):
    assert f'm_Name: Trap_{step}\n' in text, f'Missing step {step}'

for guid in re.findall(r'guid: ([a-f0-9]{32})', text):
    if guid == '0000000000000000e000000000000000':
        continue
    assert any(f'guid: {guid}' in meta.read_text(encoding='utf-8')
               for meta in (root / 'Assets/Art/Materials').rglob('*.meta')), f'Missing material {guid}'
print('PASS platform prefab: references, walking surface, steps and materials')
