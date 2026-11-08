"""Check both placeable platforms' Unity references and walking colliders."""
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
materials = {match.group(1) for meta in (root / 'Assets/Art/Materials').rglob('*.meta')
             if (match := re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8'), re.MULTILINE))}


def check(name, steps):
    text = (root / 'Assets/Art/Prefabs' / name).read_text(encoding='utf-8')
    objects = re.findall(r'^--- !u!(\d+) &(-?\d+)', text, re.MULTILINE)
    ids = [file_id for _, file_id in objects]
    assert len(ids) == len(set(ids)), f'{name}: duplicate Unity file IDs'
    known = set(ids)
    for reference in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}', text):
        file_id, suffix = reference.groups()
        if file_id != '0' and 'guid:' not in suffix:
            assert file_id in known, f'{name}: missing local object {file_id}'

    assert sum(kind == '1' for kind, _ in objects) == 9 + steps, f'{name}: incorrect object count'
    assert sum(kind == '23' for kind, _ in objects) == 8 + steps, f'{name}: missing renderer'
    assert sum(kind == '65' for kind, _ in objects) == 1 + steps, f'{name}: missing walking collider'
    assert 'm_Size: {x: 6, y: 1.1, z: 3}' in text, f'{name}: incorrect walking surface'
    actual_steps = re.findall(r'^  m_Name: Trap_\d+$', text, re.MULTILINE)
    assert len(actual_steps) == steps, f'{name}: incorrect step count'

    for guid in re.findall(r'guid: ([a-f0-9]{32})', text):
        if guid != '0000000000000000e000000000000000':
            assert guid in materials, f'{name}: missing material {guid}'
    print(f'PASS {name}: references, walking surface, {steps} steps and materials')


if __name__ == '__main__':
    check('Perron_6m.prefab', 4)
    check('Perron_6m_zonder_trap.prefab', 0)
