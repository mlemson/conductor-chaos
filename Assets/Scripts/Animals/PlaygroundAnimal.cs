// Conductor Chaos — koe of schaap als fysiek lijf (issue #236), zelfde principe als de reizigers: één Rigidbody die
// zichzelf rechtop houdt en op een veer boven de grond 'staat'. Je kunt het dier duwen, trekken (grijpen met je
// handen), en een harde klap (trein!) gooit het om; daarna krabbelt het weer overeind.
//
//  Wander  loopt rustig rond binnen roamRadius van zijn beginplek en graast tussendoor.
//  Blocker staat koppig op zijn plek (bijvoorbeeld midden op het spoor) en loopt terug als je hem wegduwt. Met een
//          paar spelers tegelijk krijg je hem wel opzij; of lok hem weg met een hooibaal (AnimalLure): wie het hooi
//          ziet, sjokt erheen en blijft daar.
//
//  Kudde   (woestijn, levels 6-7) de kudde wacht onzichtbaar onder de grond (kinematisch). ServiceIncidents laat hem
//          op het juiste moment opduiken (ServerEmerge) en in volle galop met een stofwolk over het spoor stormen
//          (ServerCross). Komt de trein te hard aan, dan bevriezen de dieren op de rails (ServerHold): duw er één ver
//          genoeg opzij en hij geeft het op en loopt aan die kant verder, of lok hem weg met hooi.
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
        [Tooltip("Galopsnelheid bij een stormloop over het spoor (m/s).")]
        [SerializeField] float stampedeSpeed = 7f;
        [Tooltip("Kudde: wacht kinematisch en onzichtbaar (onder de grond) tot ServiceIncidents hem laat opduiken.")]
        [SerializeField] bool waitHidden;
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

        enum Cross : byte { None, Waiting, ToTrack, Holding, ToFar }
        Cross cross;
        Vector3 crossVia, crossAcross, crossTo;
        float crossStart;
        bool crossStubborn;
        const float GiveUpDistance = 2.9f;        // zo ver van de spoorlijn geduwd geeft een koppig dier het op

        public bool IsDown => (Mood)mood.Value == Mood.Fallen;
        /// <summary>Server: dit dier is nog in de stormloop of staat op het spoor (kudde-incident niet klaar).</summary>
        public bool CrossingBusy => cross != Cross.None;

        /// <summary>Server: ren na <paramref name="delay"/> s over het spoor via <paramref name="via"/> (op de rails) naar
        /// <paramref name="to"/>. Koppig = blijf op de rails staan tot je weggeduwd of weggelokt wordt.</summary>
        public void ServerCross(Vector3 via, Vector3 to, bool stubborn, float delay)
        {
            if (!IsServer) return;
            crossVia = via;
            crossTo = to;
            crossAcross = Flat(to - via);
            crossStubborn = stubborn;
            crossStart = Time.time + delay;
            cross = Cross.Waiting;
            temperament = Temperament.Wander;
        }

        /// <summary>Server: duik op uit de verborgen wachtplek (kinematisch onder de grond) op <paramref name="at"/>.</summary>
        public void ServerEmerge(Vector3 at, Quaternion rotation)
        {
            if (!IsServer || !body) return;
            waitHidden = false;
            body.isKinematic = false;
            if (TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var sync)) sync.Teleport(at, rotation, transform.localScale);
            else transform.SetPositionAndRotation(at, rotation);
            body.position = at;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            home = target = lastPos = at;
        }

        /// <summary>Server: de trein komt te hard aan, blijf op de rails staan (koppig) tot je weggeduwd of weggelokt wordt.</summary>
        public bool ServerHold()
        {
            if (!IsServer || cross is not (Cross.ToTrack or Cross.ToFar)) return false;
            cross = Cross.Holding;
            temperament = Temperament.Blocker;
            home = crossVia;
            moodUntil = 0f;
            nextThink = Time.time;
            SetMood(Mood.Idle);
            return true;
        }

        /// <summary>Server: dit dier staat nu op de rails (gestopt voor de trein).</summary>
        public bool IsHolding => cross == Cross.Holding;
        public bool IsStampeding => cross is Cross.ToTrack or Cross.ToFar;

        // true = de kudde-logica heeft deze denkstap afgehandeld
        bool ThinkCrossing()
        {
            switch (cross)
            {
                case Cross.Waiting:
                    if (Time.time < crossStart) { SetMood(Mood.Idle); nextThink = crossStart; return true; }
                    // koppig: naar de rails en daar blijven; anders in één ruk door naar de overkant
                    cross = crossStubborn ? Cross.ToTrack : Cross.ToFar;
                    Run(crossStubborn ? crossVia : crossTo, 14f);
                    return true;
                case Cross.ToTrack:
                    if (Flat3(transform.position - crossVia).magnitude > 1.2f) { Run(crossVia, 6f); return true; }
                    if (crossStubborn)
                    {
                        cross = Cross.Holding;
                        temperament = Temperament.Blocker;
                        home = crossVia;
                        SetMood(Mood.Idle);
                        return false;
                    }
                    cross = Cross.ToFar;
                    Run(crossTo, 14f);
                    return true;
                case Cross.ToFar:
                    if (Flat3(transform.position - crossTo).magnitude > 1.5f) { Run(crossTo, 8f); return true; }
                    cross = Cross.None;
                    home = transform.position;
                    roamRadius = Mathf.Min(roamRadius, 3f);
                    return false;
            }
            return false;
        }

        // Koppig dier ver genoeg van de rails geduwd (of weggelokt): het geeft het op en loopt aan die kant verder.
        void CheckGiveUp()
        {
            if (cross != Cross.Holding) return;
            Vector3 off = Flat3(transform.position - crossVia);
            float side = Vector3.Dot(off, crossAcross);
            bool lured = AnimalLure.Nearest(transform.position, lureRadius);
            if (Mathf.Abs(side) < GiveUpDistance && !lured) return;
            cross = Cross.None;
            temperament = Temperament.Wander;
            roamRadius = Mathf.Min(roamRadius, 3f);
            if (lured) return;                                      // het hooi houdt hem verder vast
            float reach = Flat3(crossTo - crossVia).magnitude;
            home = crossVia + crossAcross * Mathf.Sign(side) * reach;
            Run(home, 12f);
        }

        void Run(Vector3 to, float maxSeconds)
        {
            target = to;
            moodUntil = Time.time + maxSeconds;
            SetMood(Mood.Startled);
        }

        static Vector3 Flat3(Vector3 v) { v.y = 0f; return v; }
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
            if (!IsSpawned || !IsServer || !body) return;
            if (waitHidden)
            {
                // NetworkRigidbody zet de server bij het spawnen op niet-kinematisch: hier vasthouden op de wachtplek
                if (!body.isKinematic) body.isKinematic = true;
                return;
            }
            if (body.isKinematic) return;
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

            CheckGiveUp();
            if (beingDragged)
            {
                if (cross == Cross.Holding) { nextThink = Time.time + .5f; return; }   // wordt weggetrokken: niet terug
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
                float speed = m == Mood.Startled ? (IsStampeding ? stampedeSpeed : runSpeed) : walkSpeed;
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
                // galop: hard optrekken; net bevroren voor de trein: hard remmen. Daarna weer het gewone schrap zetten.
                float cap = IsStampeding || (cross == Cross.Holding && flat.magnitude > 1.5f) ? 14f : wanted == Vector3.zero ? brace : 2.5f;
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
            if (cross != Cross.None && cross != Cross.Holding && ThinkCrossing()) return;

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
            if (cross is Cross.ToTrack or Cross.ToFar) nextThink = Time.time;          // kudde: meteen door
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
            bool jumped = d.sqrMagnitude > 25f;                 // opgedoken uit de verborgen wachtplek
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
            KickUpDust(m, dt, jumped);
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

        // ---------------------------------------------------------------- iedereen: stofwolk bij een stormloop (cosmetisch)
        ParticleSystem dust;
        static Mesh dustMesh;
        static Material dustMaterial;
        float dustDue;

        void KickUpDust(Mood m, float dt, bool teleported)
        {
            bool running = m == Mood.Startled && speedSeen > 2.6f;
            if (!running && !teleported) return;
            if (!dust && !MakeDust()) return;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            if (teleported)                                          // opgedoken: grote wolk waar hij uit tevoorschijn komt
            {
                for (int i = 0; i < 26; i++)
                {
                    Vector2 c = Random.insideUnitCircle * 1.6f;
                    emit.position = transform.position + new Vector3(c.x, Random.Range(0f, 1.2f), c.y);
                    emit.velocity = new Vector3(c.x, Random.Range(.6f, 1.6f), c.y) * 1.2f;
                    emit.startSize = Random.Range(.6f, 1.3f);
                    emit.startLifetime = Random.Range(1.2f, 2.2f);
                    dust.Emit(emit, 1);
                }
                return;
            }
            dustDue -= dt;
            if (dustDue > 0f) return;
            dustDue = 0.03f;
            Vector3 back = -transform.forward;
            Vector2 j = Random.insideUnitCircle * 0.45f;
            emit.position = transform.position + back * 0.6f + new Vector3(j.x, 0.15f, j.y);
            emit.velocity = back * Random.Range(.4f, 1.2f) + Vector3.up * Random.Range(.5f, 1.2f);
            emit.startSize = Random.Range(.55f, 1.15f) * Mathf.Clamp(stride / .5f, .6f, 1.3f);
            emit.startLifetime = Random.Range(1.0f, 1.7f);
            dust.Emit(emit, 1);
        }

        bool MakeDust()
        {
            if (!dustMaterial)
            {
                var shader = Resources.Load<Shader>("AmbientDust");
                if (!shader) return false;
                dustMaterial = new Material(shader) { name = "Stofwolk" };
                dustMesh = DustPuffMesh();
            }
            var go = new GameObject("Stofwolk (cosmetisch)");
            go.transform.SetParent(transform, false);
            dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 320;
            main.gravityModifier = -0.04f;
            // desert_sand / desert_band (palette.json): licht zand met hier en daar een roder plukje
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(253, 198, 121, 255), new Color32(231, 140, 96, 255));
            main.startRotation3D = true;
            var angle = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationX = main.startRotationY = main.startRotationZ = angle;
            var emission = dust.emission;
            emission.enabled = false;
            var shape = dust.shape;
            shape.enabled = false;
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .45f), new Keyframe(.25f, 1f), new Keyframe(1f, 0f)));
            var drag = dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.6f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = dustMesh;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
            renderer.sharedMaterial = dustMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            dust.Play();
            return true;
        }

        // grove low-poly wolkbol (icosaëder), zoals de stoomwolkjes in het concept
        static Mesh DustPuffMesh()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized * .5f;
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            // platte facetten: elk vlak eigen hoekpunten
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < f.Length; i++) { verts.Add(v[f[i]]); tris.Add(i); }
            var mesh = new Mesh { name = "Stofbol" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
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
