"""Geometric regressions for the cap-band and sleeve attachments."""
from pathlib import Path
import sys
import math
import numpy as np

root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'ArtSource/Tools'))
sys.path.insert(0,str(root/'ArtSource/Characters/Conductor'))
import build_conductor as conductor
import meshkit as mk

band_a=conductor.BODY_R+0.012
band_b=band_a*conductor.BODY_DEPTH+0.02
brim=conductor.build_brim(band_a,band_b)
assert brim.V[:,1].max() < 0, 'brim crosses the gold band'
for side in (-1,1):
    rotation=mk.rot_z(side*math.radians(50))
    arm=conductor.build_arm().transform(R=rotation,t=(side*conductor.SHOULDER[0],conductor.SHOULDER[1],0))
    assert arm.V[:,1].max() <= conductor.Y_COLLAR, 'sleeve rises above uniform'
    assert np.isfinite(mk.vertex_normals(arm)).all()
buttons=conductor.build_buttons()
assert 'M_Conductor_GoldLow' in buttons.M
print('PASS: brim remains below gold band; sleeves remain below uniform edge; lower button uses muted gold.')

