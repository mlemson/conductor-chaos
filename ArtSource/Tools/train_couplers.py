"""Shared magnetic coupler mesh with its pivot on the contact face."""

import math
import meshkit as mk


def add_couplers(scene, parent, rail_y, center_z, rear_mount, front_mount, metal, brass):
    height = rail_y + 0.79
    length = 0.36
    head = mk.merge(
        mk.cylinder(0.066, 0.31, 10, 0.018, mat=metal).transform(t=(-0.205, 0, 0)),
        mk.cylinder(0.108, 0.096, 10, 0.024, mat=metal).transform(t=(-0.061, 0, 0)),
        mk.cylinder(0.097, 0.018, 10, 0.004, mat=brass).transform(t=(-0.022, 0, 0)),
        mk.cylinder(0.080, 0.026, 10, 0.008, mat=metal).transform(t=(-0.013, 0, 0)),
    )
    scene.add(mk.Part("Coupler_Front", head, (front_mount + length, height, center_z), parent))
    scene.add(mk.Part("Coupler_Rear", head.transform(R=mk.rot_y(math.pi)),
                      (rear_mount - length, height, center_z), parent))
