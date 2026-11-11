# Train physics & Level 1 verification — Unity Play Mode

Status: **NOT RUN** (Unity/play tests by project owner).
Changes build on the existing server-authoritative movement, wagon and cargo systems. Do not treat static code review as gameplay verification.

## Solo host, moving train
1. Start Level 1 from the lobby, enter the passenger wagon and drive at constant speed. Walk both directions and stand still. The conductor should stay on the train without drift or double speed.
2. Sprint and jump vertically, forward and backward. The initial jump should preserve wagon horizontal speed; no sudden push backwards. Repeat during acceleration, braking and a curve.
3. Reach outside and grab a rail-guided wagon wall or edge with one hand then both hands. Stay attached as it moves; W should pull over a reachable top lip. Releasing either hand should work immediately. Compare with stationary rocks and normal loose props.
4. Test falling off / switching wagons / derailing: the controller must not preserve a stale train anchor or snap to the old wagon.
5. On the coal tender, leave coal undisturbed as the train starts, stops and turns. Watch for visual jumping. Pick up and shovel several pieces; sleeping pieces must still wake and work.

## Objectives
1. Force/roll until "Vervoer drie kratten" is selected. The passenger wagon, **not the coal tender**, is the valid load area.
2. Put all three crates on the passenger floor. Loaded should reach 3/3; remove one before departure and verify that the train cannot depart with only two currently aboard.
3. Put it back, ring the bell and drive. Travelled should rise when each crate is physically aboard during motion.
4. Stop in the destination train zone and unload each crate into the yellow cargo target. Delivered should rise from 0/3 to 3/3 only after the crate is actually there and has settled.
5. Also check "Bezorg de gevonden bagage", the introduction, and a non-cargo objective.

## Host plus separate remote client
- Repeat walking/jumping, grabbing/mantling, cargo loading/delivery and coal interaction on a remote client. Both clients must see the same result; check for delayed snapping, joint breakage, doubled motion and stale grips after disconnect.
- Log every failure with scene, build commit, host/client role, train speed, reproducible steps and Unity Console messages.
