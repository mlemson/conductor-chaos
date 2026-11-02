# Train collision, derailment & network physics — design intent

**Status:** design specification, not implemented or verified in Unity. Supplements [game vision](GAME_VISION.md), [architecture](ARCHITECTURE.md) and [test plan](TESTPLAN.md).

## Non-negotiable goal

**The train must be able to REALLY derail.** A broken rail, sharp bend at excessive speed, rockfall, collision, or bridge accident can cause one or multiple cars to leave the track, rotate, tumble, slide, collide and — where geometry allows — fall. This is not a pre-rendered sequence or a canned derailment animation. The post-impact outcome is decided by server-side physics and should vary naturally according to impact, mass, velocity, shape, angle and coupling.

A cow or other light obstacle must **not automatically derail** an entire train: its consequences depend on collision setup, speed, angle and stylized game balance. Keep collisions comedic and non-graphic. There should also be intermediate outcomes: emergency braking, shaking, damaged coupler, blocked path, partial derailment.

## Recommended hybrid model (PROPOSAL; prototype before freezing)

1. **ON_RAILS / guided:** each carriage follows a rail path using a controlled vehicle/guide model. Keep continuous obstacle sweeps and collision probes active; don't simply disable physics/collision or teleport through props. Leaning/sway and passenger forces can be approximated or physically driven.
2. **UNSTABLE / partial derail:** track damage, lost wheel support, overspeed or significant impact can progressively reduce rail constraints, with a controllable threshold/hysteresis to prevent accidental flicker. The leading carriage can leave the track while trailing ones remain constrained temporarily.
3. **OFF_RAILS / dynamic:** at an authoritative physics tick, release relevant rail constraints. Preserve linear velocity, angular velocity and each carriage's position/orientation. Preserve the relative velocities caused by turning and use contact impulse/geometry to initiate tumble. Enable full physics of key body colliders with continuous collision protection as measured.
4. **SETTLED / recovery:** wreckage remains physically present until an explicitly designed reset/respawn/recovery flow. No instant snap-to-track unless chosen as an intentional gameplay action.

**Avoid:** forcing the entire articulated train to become one rigidbody; switching to dynamic with zero velocity; snapping all cars into one pre-scripted wreck pose; each client independently simulating the authoritative crash; sending per-wheel physics every network frame.

## Train consist, couplers, passengers, cargo

- Prototype **one rigidbody per carriage** (or a small number per carriage only if necessary); couple carriages with bounded physically-tested constraints/joints and **break forces**. Carriages may derail independently or remain linked. Tune center of mass, drag, simple collision hulls and wheel/ground friction for fun and stability.
- While on rails, passengers and cargo inhabit a well-defined moving train frame. Define how world and train-relative transforms, player controllers and item velocities transition on derailment, including doors, doors-held-open, pickups, carried items and players standing on floors.
- Server should own impactful collision results: players can fall/stumble, luggage can slide and hit objects, doors can swing or jam. Cosmetic debris/sparks can be local or event-driven and should not require individual replicated rigidbodies.
- For fast trains, prevent missed collisions by continuous collision/sweep testing along the traveled distance, not just discrete `OnCollisionEnter` events. Unity CCD is a selectable tool and carries performance costs. Profile before enabling it everywhere.
- The simplest test may use a guided kinematic/controlled carriage then transition to a dynamic rigidbody; compare against a force-driven body with rail constraints. **This choice is not finalized.** Transition state and colliders require explicit Unity proof.

## Multiplayer & authority

- **Only the server/host determines** impact, damage, state transition, detachments and physical motion of carriages and gameplay objects. Each client receives coherent networked states/transforms/velocities (as required) and uses smoothing/correction for visual continuity.
- Replicate an explicit carriage/consist state (guided, unstable, derailed, resting), event identity, collision outcomes and current positions so **late joiners** reconstruct wreckage without relying on historic one-shot effects.
- Multiplayer does not guarantee **pixel-identical** local physics at each frame. The invariant is one shared, server-decided outcome with tolerable visual delay and no lasting disagreement.
- Validate crashes with a dedicated remote client and induced latency/jitter/loss. Prevent physics glitches from mismatched parenting, input ownership, large transform corrections and double application of train motion.
- Reconcile character motion and interaction reach when the train tips, including doors or pickups on a rotated car. Prefer stable controls and readable reactions over exact engineering realism.

## Implementation gates (not for P0-01)

- **P0:** demonstrate host+client and a moving wagon, shared door and suitcase without derailment. Choose train reference frames such that future track release and 6-DOF motion remain possible. No architectural assumption that train movement is permanently rail-locked.
- **P1 physics proof:** one carriage leaves a deliberately broken track at defined speed and ends up in a physical pose. Both players and a loose suitcase respond. Test at least a remote client. Compare a rigidbody-based track constraint vs guided-to-dynamic transition for quality, stability and complexity.
- **P2 production:** couple 2–3 wagons and test staggered derailment, breakable linkage, bridge edge, collisions with large obstacles, impacts on passengers/props, recovery or restart loop.
- **Scale:** profile up to 6 players, several cars and a defined reasonable number of important loose objects. Unimportant debris stays cosmetic where possible.

## Acceptance examples

- Different approach speeds/angles produce plausibly different derailment results.
- Hitting a rock at speed can cause partial derailment; a minor obstacle can cause a lesser incident.
- Two coupled carriages can react differently, and coupling can transmit force or break.
- Host and remote client agree which cars derailed and where they came to rest.
- Passengers/players do not float through floors, teleport permanently, or get catapulted by frame-space mistakes.
- Late joiners see the current wreck; disconnecting someone does not erase the crash.
- No hidden cutscene or client-specific randomized outcome substitutes for the collision.

## Unity references for later verification

- Unity NGO NetworkRigidbody server-authoritative physics: https://mp-docs.dl.it.unity3d.com/netcode/2.1.1/advanced-topics/physics/
- Unity 6 collision detection trade-offs: https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/physics-optimization/cpu/rigidbody-collision-modes

Check exact package/API compatibility against the version actually installed; these sources describe concepts, not a validated project configuration.
