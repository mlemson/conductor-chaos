"""Wissel en herstelbaar spoor (issues #214, #215): geen dubbele vlakken in elke stand, en de knopen die Unity
(TrackSwitch / RepairPoint) nodig heeft bestaan.

* wissel: middenstand (kapot), rechtdoor en afbuigend (tongen + trekstang verschoven), hersteld (zonder Broken_Bits)
* kapot spoor: kapot (Broken_Bits) en hersteld (Fixed_Rail, bout erin)
"""
import math
import os
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Environment"))
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Tools"))
import build_switch_kit as SW  # noqa: E402
import build_track_kit as T  # noqa: E402
import zfight as Z  # noqa: E402



def rot_about_y(m, pivot, ang):
    """Draai een mesh (lokaal t.o.v. pivot) om de verticale as door de pivot."""
    return m.transform(R=T.mk.rot_y(ang))


def check(name, parts):
    sc = T.mk.Scene(name)
    for mname in sorted({m for mesh in parts for m in mesh.M}):
        ramp, sm, sh, op = T.MATERIALS[mname]
        sc.material(mname, ramp[2], ramp, sm, ramp_shift=sh, opts=dict(op))
    sc.add(T.mk.Part(name, None))
    for k, mesh in enumerate(parts):
        sc.add(T.mk.Part(f"P{k}", mesh, (0, 0, 0), name))
    path = os.path.join(tempfile.gettempdir(), f"_test_{name}.glb")
    sc.write_glb(path, include_hidden=False)
    res = Z.find(Z.glb_triangles(path))
    assert not res, f"{name}: {len(res)} dubbele vlakken, bv. {res[0]}"
    print(f"PASS {name}: geen dubbele vlakken")


def world(node):
    m, p = node
    return m.transform(t=p)


def test_switch_states():
    rng = np.random.default_rng([2026, 214])
    P, D, nodes, markers, socks, info = SW.build(rng)
    names = {n for n, _ in socks} | set(markers)
    for need in ("Socket_Start", "Socket_End", "DivSocket_Start", "DivSocket_End", "DivPath_00", "DivPath_12",
                 "Toe_Neutral", "Toe_Straight", "Toe_Diverge", "Lever_Tip_Neutral", "Lever_Tip_Straight",
                 "Lever_Tip_Diverge", "Lever_Tip_Broken", "Repair_Bolt_Home"):
        assert need in names, f"wissel mist {need}"
    for need in ("Blade_S", "Blade_D", "Tie_Bar", "Switch_Lever", "Switch_Target", "Repair_Bolt", "Broken_Bits"):
        assert need in nodes, f"wissel mist knoop {need}"
    assert abs(info["divergeOffset"] - 5.359) < 0.01
    base = nodes["Mesh"]
    tn, ts, td = (np.array(markers[k])[[0, 2]] for k in ("Toe_Neutral", "Toe_Straight", "Toe_Diverge"))
    heel = np.array(nodes["Blade_S"][1])[[0, 2]]

    def ang(target):
        a, b = tn - heel, target - heel
        return math.atan2(a[0] * b[1] - a[1] * b[0], a @ b)
    for state, target in (("kapot", tn), ("rechtdoor", ts), ("afbuigend", td)):
        a = ang(target)
        parts = [base, nodes["Switch_Lever"][0].transform(t=nodes["Switch_Lever"][1]),
                 nodes["Switch_Target"][0].transform(t=nodes["Switch_Target"][1])]
        for k in ("Blade_S", "Blade_D"):
            m, p = nodes[k]
            parts.append(m.transform(R=T.mk.rot_y(-a), t=p))   # rot_y(-a): + = naar +z, net als de generator
        if state == "kapot":
            parts += [nodes["Broken_Bits"], world(nodes["Repair_Bolt"])]
        else:
            m, p = nodes["Tie_Bar"]
            dz = (target - tn)[1]
            parts.append(m.transform(t=(p[0], p[1], p[2] + dz)))
            bm, bp = nodes["Repair_Bolt"]
            home = markers["Repair_Bolt_Home"]
            parts.append(bm.transform(t=home))
        check(f"Switch_{state}", parts)
    # afstand tong-stokrail in de aanliggende stand: geen overlap van de koppen
    assert SW.BLADE_GAP > T.RAIL_W


def test_broken_track_states():
    rng = np.random.default_rng([2026, 53])
    path, mesh, col, extras, markers = T.broken(rng)
    assert set(extras) == {"Broken_Bits", "Fixed_Rail", "Repair_Bolt"}
    assert not extras["Fixed_Rail"][2] and extras["Broken_Bits"][2]
    bolt, piv, _ = extras["Repair_Bolt"]
    home = dict(markers)["Repair_Bolt_Home"]
    check("Broken_kapot", [mesh, extras["Broken_Bits"][0], bolt.transform(t=piv)])
    check("Broken_hersteld", [mesh, extras["Fixed_Rail"][0], bolt.transform(t=home)])


if __name__ == "__main__":
    test_switch_states()
    test_broken_track_states()
