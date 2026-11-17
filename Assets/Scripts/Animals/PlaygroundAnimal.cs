// Conductor Chaos — koe of schaap als fysiek lijf (issue #236), zelfde principe als de reizigers: één Rigidbody die
// zichzelf rechtop houdt en op een veer boven de grond 'staat'. Je kunt het dier duwen, trekken (grijpen met je
// handen), en een harde klap (trein!) gooit het om; daarna krabbelt het weer overeind.
//
//  Wander  loopt rustig rond binnen roamRadius van zijn beginplek en graast tussendoor.
//  Blocker staat koppig op zijn plek (bijvoorbeeld midden op het spoor) en loopt terug als je hem wegduwt. Met een
//          paar spelers tegelijk krijg je hem wel opzij; of lok hem weg met een hooibaal (AnimalLure): wie het hooi
//          ziet, sjokt erheen en blijft daar.
//
// Netwerk: de server simuleert (NetworkTransform + NetworkRigidbody op hetzelfde object, gezet door de prefabbouwer,
// Conductor Chaos > Art > Animals). Clients krijgen de stand van het lijf en het humeur (NetworkVariable); poten, kop,
// staart en oren bewegen daar alleen cosmetisch mee.
using System.Collections.Generic;
using ConductorChaos.Characters;
using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Animals
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class PlaygroundAnimal : NetworkBehaviour
    {
        public enum Temperament { Wander, Blocker }
        enum Mood : byte { Idle, Walk, Graze, Startled, Fallen }

        [SerializeField] Temperament temperament = Temperament.Wander;
        [Tooltip("Zo ver loopt een Wander-dier van zijn beginplek (m).")]
        [SerializeField] float roamRadius = 6f;
        [SerializeField] float walkSpeed = 0.65f;
        [SerializeField] float runSpeed = 1.8f;
        [Tooltip("Hoe hard het dier zich schrap zet als je duwt (m/s² tegenwerking). Hoger = meer spelers nodig.")]
        [SerializeField] float brace = 1.6f;
        [Tooltip("Een klap boven deze snelheidsverandering (m/s) laat het dier schrikken.")]
        [SerializeField] float startleImpulse = 1.2f;
        [Tooltip("Een klap boven deze snelheidsverandering (m/s) gooit het dier om.")]
        [SerializeField] float knockImpulse = 4.5f;
        [SerializeField] float lureRadius = 8f;
        [Header("Cosmetisch")]
        [SerializeField] float stride = 0.42f;
        [SerializeField] float legSwing = 30f;

        readonly NetworkVariable<byte> mood = new((byte)Mood.Idle);
        readonly RaycastHit[] hits = new RaycastHit[12];

        Rigidbody body;
        Transform model, head, tail, earL, earR, alert;
        readonly List<(Transform leg, Quaternion rest, float phase)> legs = new();
        Quaternion headRest, tailRest, earLRest, earRRest;
        CharacterFace face;
        Vector3 home, target, lastPos;
        Vector3 fleeFrom;
        float nextThink, moodUntil, fallenSince = -1f, gait, speedSeen, earTwitch;
        bool grounded;
        Mood shownMood = (Mood)255;

        public bool IsDown => (Mood)mood.Value == Mood.Fallen;
        public Temperament Kind => temperament;

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            model = transform;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Head": head = t; break;
                    case "Tail": tail = t; break;
                    case "Ear_L": earL = t; break;
                    case "Ear_R": earR = t; break;
                    case "Fx_Alert": alert = t; break;
                    case "Leg_FL": case "Leg_BR": legs.Add((t, t.localRotation, 0f)); break;
                    case "Leg_FR": case "Leg_BL": legs.Add((t, t.localRotation, Mathf.PI)); break;
                }
            }
            if (head) headRest = head.localRotation;
            if (tail) tailRest = tail.localRotation;
            if (earL) earLRest = earL.localRotation;
            if (earR) earRRest = earR.localRotation;
            face = CharacterFace.OnModel(model);
            home = target = lastPos = transform.position;
            gait = Random.value * 10f;
            if (IsServer)
            {
                body.constraints = RigidbodyConstraints.None;
                body.maxAngularVelocity = 12f;
                nextThink = Time.time + Random.Range(0.5f, 3f);
            }
        }

        // ---------------------------------------------------------------- server: lijf en gedrag
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !body || body.isKinematic) return;
            Vector3 up = transform.up;
            bool upright = Vector3.Dot(up, Vector3.up) > 0.5f;
            grounded = false;
            float groundY = 0f;
            Vector3 origin = transform.position + up * 0.5f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 0.85f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.attachedRigidbody == body || hits[i].normal.y < 0.5f) continue;
                if (hits[i].distance < best) { best = hits[i].distance; groundY = hits[i].point.y; grounded = true; }
            }

            bool beingDragged = false;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && player.TryGetControlHand(body, out _, out _, out _))
                {
                    beingDragged = true;
                    break;
                }
            var m = (Mood)mood.Value;
            if (m == Mood.Fallen) { if (!beingDragged) Recover(upright); return; }
            if (!upright) { SetMood(Mood.Fallen); fallenSince = -1f; return; }

            Vector3 v = body.linearVelocity;
            if (grounded)
            {
                // op de poten staan: veer naar de grond onder de pivot (voeten), plus zwaartekracht opheffen
                float error = groundY - transform.position.y;
                float lift = -Physics.gravity.y + error * 140f - v.y * 16f;
                body.AddForce(Vector3.up * Mathf.Max(0f, lift), ForceMode.Acceleration);
            }
            // rechtop blijven en niet tollen
            Vector3 tilt = Vector3.Cross(up, Vector3.up);
            Vector3 w = body.angularVelocity;
            body.AddTorque(tilt * 40f - new Vector3(w.x, 0f, w.z) * 9f, ForceMode.Acceleration);

            if (beingDragged)
            {
                home = transform.position;
                SetMood(Mood.Idle);
                nextThink = Time.time + .5f;
                return;
            }
            Think(m);
            m = (Mood)mood.Value;
            Vector3 flat = new(v.x, 0f, v.z);
            Vector3 wanted = Vector3.zero;
            if (grounded && (m == Mood.Walk || m == Mood.Startled))
            {
                Vector3 to = target - transform.position;
                to.y = 0f;
                float speed = m == Mood.Startled ? runSpeed : walkSpeed;
                if (to.sqrMagnitude > 0.04f)
                {
                    Vector3 dir = to.normalized;
                    float turn = Vector3.SignedAngle(Flat(transform.forward), dir, Vector3.up);
                    body.AddTorque(Vector3.up * (turn * Mathf.Deg2Rad * 9f - w.y * 4f), ForceMode.Acceleration);
                    float facing = Mathf.Clamp01(Vector3.Dot(Flat(transform.forward), dir));
                    wanted = Flat(transform.forward) * speed * facing * Mathf.Clamp01(to.magnitude);
                }
                else ArriveAt(m);
            }
            else body.AddTorque(Vector3.up * (-w.y * 4f), ForceMode.Acceleration);
            if (grounded)
            {
                // lopen = naar de gewenste snelheid trekken; stilstaan = schrap zetten (begrensd: hard genoeg duwen werkt)
                float cap = wanted == Vector3.zero ? brace : 2.5f;
                Vector3 accel = Vector3.ClampMagnitude((wanted - flat) * 4f, cap);
                body.AddForce(accel, ForceMode.Acceleration);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        void Think(Mood m)
        {
            if ((m == Mood.Startled || m == Mood.Walk) && Time.time < moodUntil) return;
            if (Time.time < nextThink && m != Mood.Startled && m != Mood.Walk) return;
            nextThink = Time.time + Random.Range(1.5f, 4f);

            var lure = AnimalLure.Nearest(transform.position, lureRadius);
            if (lure)
            {
                Vector3 to = lure.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > 1.3f) Walk(lure.transform.position - to.normalized * 1.0f, 1.5f);   // vaak bijsturen: hooi beweegt
                else { home = transform.position; SetMood(Mood.Graze); }       // gelokt: hier blijft hij
                return;
            }
            if (temperament == Temperament.Blocker)
            {
                Vector3 away = home - transform.position;
                away.y = 0f;
                if (away.magnitude > 0.6f) Walk(home, 4f);
                else SetMood(Random.value < 0.6f ? Mood.Graze : Mood.Idle);
                return;
            }
            float r = Random.value;
            if (r < 0.45f)
            {
                Vector2 c = Random.insideUnitCircle * roamRadius;
                Vector3 p = home + new Vector3(c.x, 0f, c.y);
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.normal.y > 0.8f && Mathf.Abs(hit.point.y - transform.position.y) < 0.8f)
                { Walk(hit.point, 12f); return; }
            }
            SetMood(r < 0.8f ? Mood.Graze : Mood.Idle);
        }

        void Walk(Vector3 to, float maxSeconds)
        {
            target = to;
            moodUntil = Time.time + maxSeconds;          // daarna opnieuw kiezen (vastgelopen of doel verplaatst)
            SetMood(Mood.Walk);
        }

        void ArriveAt(Mood m)
        {
            moodUntil = 0f;
            nextThink = Time.time + (m == Mood.Startled ? 1f : Random.Range(2f, 5f));
            SetMood(Random.value < 0.6f ? Mood.Graze : Mood.Idle);
        }

        void Recover(bool upright)
        {
            if (upright && grounded) { SetMood(Mood.Idle); nextThink = Time.time + 1.5f; return; }
            if (fallenSince < 0f)
            {
                if (body.linearVelocity.sqrMagnitude < 0.5f * 0.5f) fallenSince = Time.time;   // eerst uitrollen
                return;
            }
            if (Time.time - fallenSince < 2.2f) return;
            // overeind rollen, met een duwtje omhoog
            Vector3 tilt = Vector3.Cross(transform.up, Vector3.up);
            if (tilt.sqrMagnitude < 0.0004f) tilt = transform.forward * 0.3f;
            Vector3 w = body.angularVelocity;
            body.AddTorque(tilt * 60f - new Vector3(w.x, 0f, w.z) * 6f, ForceMode.Acceleration);
            if (Time.time - fallenSince < 2.5f) body.AddForce(Vector3.up * 3f, ForceMode.VelocityChange);
            if (Time.time - fallenSince > 9f) fallenSince = Time.time;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !IsServer || !body || body.isKinematic) return;
            float dv = collision.impulse.magnitude / Mathf.Max(1f, body.mass);
            if (dv >= knockImpulse && (Mood)mood.Value != Mood.Fallen)
            {
                SetMood(Mood.Fallen);
                fallenSince = -1f;
                return;
            }
            if (dv < startleImpulse || (Mood)mood.Value == Mood.Fallen) return;
            fleeFrom = collision.GetContact(0).point;
            Vector3 away = Flat(transform.position - fleeFrom);
            target = transform.position + away * (temperament == Temperament.Blocker ? 1.2f : 3.5f);
            moodUntil = Time.time + 2.5f;
            SetMood(Mood.Startled);
        }

        void SetMood(Mood m)
        {
            if (mood.Value != (byte)m) mood.Value = (byte)m;
        }

        // ---------------------------------------------------------------- iedereen: poten, kop, staart, gezicht
        void LateUpdate()
        {
            if (!IsSpawned) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 p = transform.position;
            Vector3 d = p - lastPos;
            lastPos = p;
            d.y = 0f;
            speedSeen = Mathf.Lerp(speedSeen, d.magnitude / dt, 1f - Mathf.Exp(-dt * 8f));
            var m = (Mood)mood.Value;
            bool down = m == Mood.Fallen;
            float move = down ? 0f : Mathf.Clamp01(speedSeen / Mathf.Max(0.2f, walkSpeed));
            gait += dt * speedSeen / Mathf.Max(0.1f, stride) * Mathf.PI;
            Vector3 side = transform.right;
            foreach (var (leg, rest, phase) in legs)
            {
                if (!leg || !leg.parent) continue;
                float a = down ? Mathf.Sin(Time.time * 7f + phase) * 12f : Mathf.Sin(gait + phase) * legSwing * move;
                leg.localRotation = Quaternion.AngleAxis(a, leg.parent.InverseTransformDirection(side)) * rest;
            }
            if (head && head.parent)
            {
                float pitch = m == Mood.Graze ? 38f + Mathf.Sin(Time.time * 5f) * 3f :
                              m == Mood.Startled ? -12f : Mathf.Sin(gait * 2f) * 4f * move;
                float yaw = m == Mood.Idle ? Mathf.Sin(Time.time * 0.6f + gait) * 14f : 0f;
                var pitchQ = Quaternion.AngleAxis(pitch, head.parent.InverseTransformDirection(side));
                var yawQ = Quaternion.AngleAxis(yaw, head.parent.InverseTransformDirection(transform.up));
                head.localRotation = Quaternion.Slerp(head.localRotation, yawQ * pitchQ * headRest, 1f - Mathf.Exp(-dt * 6f));
            }
            if (tail && tail.parent)
            {
                float sway = Mathf.Sin(Time.time * (m == Mood.Startled ? 9f : 2.2f)) * (m == Mood.Startled ? 30f : 16f);
                tail.localRotation = Quaternion.AngleAxis(sway, tail.parent.InverseTransformDirection(transform.up)) * tailRest;
            }
            earTwitch = Mathf.Max(0f, earTwitch - dt);
            if (earTwitch <= 0f && Random.value < dt * 0.3f) earTwitch = 0.35f;
            float flick = Mathf.Sin(earTwitch / 0.35f * Mathf.PI) * 25f;
            if (earL && earL.parent) earL.localRotation = Quaternion.AngleAxis(flick, earL.parent.InverseTransformDirection(transform.forward)) * earLRest;
            if (earR && earR.parent) earR.localRotation = Quaternion.AngleAxis(-flick * 0.5f, earR.parent.InverseTransformDirection(transform.forward)) * earRRest;
            if (m != shownMood)
            {
                shownMood = m;
                if (alert) alert.gameObject.SetActive(m == Mood.Startled || m == Mood.Fallen);
                if (face)
                {
                    face.SetExpression(m == Mood.Fallen ? EyeState.Closed : EyeState.Open,
                        m == Mood.Startled || m == Mood.Fallen ? MouthShape.Surprised :
                        m == Mood.Graze ? MouthShape.Happy : MouthShape.Neutral);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = temperament == Temperament.Blocker ? new Color(1f, 0.4f, 0.3f) : new Color(0.5f, 0.9f, 0.4f);
            Vector3 c = Application.isPlaying ? home : transform.position;
            if (temperament == Temperament.Wander) Gizmos.DrawWireSphere(c, roamRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.3f);
            Gizmos.DrawWireSphere(c, lureRadius);
        }
    }
}
