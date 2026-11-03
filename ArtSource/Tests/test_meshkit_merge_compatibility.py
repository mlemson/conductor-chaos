"""Keep smooth character normals and faceted train faces through mesh operations."""
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Tools'))
import meshkit as mk

vertices = np.array([[0., 0., 0.], [1., 0., 0.], [0., 1., 0.]])
smooth = mk.Mesh(vertices, [(0, 1, 2)], ['smooth'], N=np.tile([0., 0., 1.], (3, 1)))
faceted = mk.Mesh(vertices + [0., 0., 1.], [(0, 1, 2)], ['facet'], sharp=True)
combined = mk.merge(smooth, faceted)
assert combined.face_sharp() == [False, True]
assert np.allclose(combined.N[:3], smooth.N)
assert np.allclose(mk.corner_normals(combined), np.tile([0., 0., 1.], (6, 1)))
rotated = combined.transform(R=mk.rot_x(np.pi / 2))
assert rotated.face_sharp() == [False, True]
assert np.allclose(rotated.N[:3], np.tile([0., -1., 0.], (3, 1)))
assert combined.copy().N is not combined.N
print('PASS: smooth normals and per-face facets survive merge, copy and transform.')
