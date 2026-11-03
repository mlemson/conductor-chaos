"""Exercise character and train geometry after shared meshkit changes.

Shading is disabled here: this checks the geometry pipeline, not final renders.
"""
from pathlib import Path
import importlib.util
import sys

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root / 'Tools'))
import meshkit as mk

mk.Scene.bake_shading = lambda self, *args, **kwargs: None
builders = [
    ('Characters/Conductor/build_conductor.py', 'build_scene'),
    ('Train/Locomotive/build_locomotive.py', 'build_locomotive'),
    ('Train/RegionalWagon/build_regional_wagon.py', 'build_wagon'),
    ('Train/Tender/build_tender.py', 'build_tender'),
    ('Train/CattleWagon/build_cattle_wagon.py', 'build_cattle_wagon'),
]
for index, (path, function) in enumerate(builders):
    spec = importlib.util.spec_from_file_location(f'generator_{index}', root / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    scene = getattr(module, function)()
    assert scene.parts and sum(part.mesh.tri_count() for part in scene.parts if part.mesh is not None) > 0, path
    print(f'PASS: {path} geometry builds ({len(scene.parts)} parts; shading skipped)')
