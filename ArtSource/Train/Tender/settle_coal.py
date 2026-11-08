"""Laat losse kolenbrokken in de open kolenbak van de tender vallen en schrijft hun rustposities naar coal_pile.json.

Alleen nodig als je de vulling opnieuw wilt maken (aantal, grootte, diepte); build_tender.py leest het JSON-bestand,
zodat het bouwen van de modellen geen physics-pakket nodig heeft.

Vereist: pip install pybullet numpy
Gebruik: python ArtSource/Train/Tender/settle_coal.py [--count 170] [--seed 11]

De vorm van elk brok is dezelfde gefacetteerde ellipsoïde als in build_tender.py; de botsvorm in de simulatie is de
convexe omhulling ervan (net als de convexe MeshCollider die TrainDrive in Unity gebruikt). Zo liggen de brokken
zonder overlap op elkaar en blijven ze liggen wanneer ze in het spel wakker worden.
"""
import argparse
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
sys.path.insert(0, HERE)
import meshkit as mk  # noqa: E402
import build_tender as bt  # noqa: E402


def chunk_sizes(rng, count):
    out = []
    for _ in range(count):
        s = rng.uniform(0.10, 0.13)
        out.append((round(s * rng.uniform(0.95, 1.15), 4), round(s * rng.uniform(0.65, 0.85), 4),
                    round(s * rng.uniform(0.85, 1.05), 4)))
    return out


def main():
    import pybullet as p

    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=170)
    ap.add_argument("--seed", type=int, default=11)
    args = ap.parse_args()
    rng = np.random.default_rng(args.seed)
    x0, x1, y0, z0, z1 = bt.bin_interior()
    p.connect(p.DIRECT)
    p.setGravity(0, -9.81, 0)
    p.setPhysicsEngineParameter(fixedTimeStep=1 / 240, numSolverIterations=60)
    # statische bak: vloer + vier wanden tot de bovenkant van de gouden kraag (Y omhoog, net als het model);
    # wat er niet in past rolt over de rand en wordt weggelaten
    t = 0.2
    hh = (bt.BODY_TOP + 0.08 - y0) / 2
    walls = [((x0 + x1) / 2, y0 - t / 2, (z0 + z1) / 2, (x1 - x0) / 2 + t, t / 2, (z1 - z0) / 2 + t),
             (x0 - t / 2, y0 + hh, (z0 + z1) / 2, t / 2, hh, (z1 - z0) / 2 + t),
             (x1 + t / 2, y0 + hh, (z0 + z1) / 2, t / 2, hh, (z1 - z0) / 2 + t),
             ((x0 + x1) / 2, y0 + hh, z0 - t / 2, (x1 - x0) / 2 + t, hh, t / 2),
             ((x0 + x1) / 2, y0 + hh, z1 + t / 2, (x1 - x0) / 2 + t, hh, t / 2)]
    ground = p.createMultiBody(0, p.createCollisionShape(p.GEOM_BOX, halfExtents=[20, 0.1, 20]), basePosition=[0, -0.1, 0])
    for (cx, cy, cz, hx, hy, hz) in walls:
        shape = p.createCollisionShape(p.GEOM_BOX, halfExtents=[hx, hy, hz])
        body = p.createMultiBody(0, shape, basePosition=[cx, cy, cz])
        p.changeDynamics(body, -1, lateralFriction=0.9, restitution=0.0)
    sizes = chunk_sizes(rng, args.count)
    bodies = []
    per_layer = max(1, int(((x1 - x0) / 0.25) * ((z1 - z0) / 0.25)))
    for k, (a, h, b) in enumerate(sizes):
        mesh = bt.coal_chunk_mesh(a, h, b)
        shape = p.createCollisionShape(p.GEOM_MESH, vertices=mesh.V.tolist())
        layer = k // per_layer
        pos = [rng.uniform(x0 + 0.16, x1 - 0.16), y0 + 0.18 + layer * 0.22 + rng.uniform(0, 0.06),
               rng.uniform(z0 + 0.16, z1 - 0.16)]
        q = p.getQuaternionFromEuler(rng.uniform(0, 2 * math.pi, 3).tolist())
        body = p.createMultiBody(0.18, shape, basePosition=pos, baseOrientation=q)
        p.changeDynamics(body, -1, lateralFriction=0.9, rollingFriction=0.02, spinningFriction=0.02,
                         restitution=0.0, linearDamping=0.1, angularDamping=0.3)
        bodies.append(body)
    for step in range(240 * 12):
        p.stepSimulation()
    # alles moet in de bak liggen en tot rust zijn gekomen
    pile = []
    for body, (a, h, b) in zip(bodies, sizes):
        pos, q = p.getBasePositionAndOrientation(body)
        v, w = p.getBaseVelocity(body)
        inside = x0 - 0.01 < pos[0] < x1 + 0.01 and z0 - 0.01 < pos[2] < z1 + 0.01 and y0 < pos[1] < bt.BODY_TOP + 0.12
        if not inside:
            continue
        if np.linalg.norm(v) > 0.05 or np.linalg.norm(w) > 0.5:
            print("waarschuwing: brok nog in beweging", pos, np.linalg.norm(v))
        pile.append({"size": [a, h, b], "pos": [round(c, 5) for c in pos], "quat": [round(c, 6) for c in q]})
    top = max(c["pos"][1] for c in pile)
    ys = np.array([c["pos"][1] for c in pile])
    print("hoogteverdeling middelpunten:", np.round(np.percentile(ys, [10, 50, 90, 100]), 3))
    out = os.path.join(HERE, "coal_pile.json")
    json.dump({"note": "gegenereerd door settle_coal.py (pybullet); x/y/z in tender-ruimte, quat = x, y, z, w",
               "seed": args.seed, "chunks": pile}, open(out, "w", encoding="utf-8"), indent=1)
    print(f"{len(pile)} brokken, hoogste middelpunt {top:.3f} m (bakrand {bt.BODY_TOP:.2f} m) -> {out}")


if __name__ == "__main__":
    main()
