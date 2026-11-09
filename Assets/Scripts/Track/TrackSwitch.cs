// Conductor Chaos — spoorwissel (issue #214). Model: ArtSource/Environment/build_switch_kit.py.
// Twee routes vanaf hetzelfde begin: rechtdoor (de TrackPiece op de wortel) en afbuigend (TrackPiece op het kind
// Route_Diverge). TrainDrive vraagt bij het oprijden welke route actief is (ActiveRoute) en laat een trein ontsporen
// op een kapotte wissel. Omzetten gaat fysiek: geef de hendel met je hand een duw naar de andere kant.
// De server beslist (route, kapot, bezet); clients tonen tongen, trekstang, hendel en richtingsschijf.
using System.Collections.Generic;
using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Track
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RepairPoint))]
    public sealed class TrackSwitch : NetworkBehaviour
    {
        static readonly Dictionary<TrackPiece, TrackSwitch> ByRoute = new Dictionary<TrackPiece, TrackSwitch>();

        [SerializeField] TrackPiece straight;
        [SerializeField] TrackPiece diverge;
        [Tooltip("Begin op de afbuigende route.")]
        [SerializeField] bool startDiverging;
        [Tooltip("Hoe hard (m/s) een hand de hendel naar de andere kant moet duwen om om te zetten.")]
        [SerializeField] float pushSpeed = 0.7f;

        readonly NetworkVariable<bool> diverging = new NetworkVariable<bool>(false);

        RepairPoint repair;
        Transform bladeS, bladeD, tieBar, lever, target;
        Quaternion bladeSBase, bladeDBase, leverBase, targetBase;
        Vector3 tieBase, toeNeutral, toeStraight, toeDiverge, heelS;
        Vector3 leverPivot, tipNeutral, tipStraight, tipDiverge, tipBroken, trackAxis;
        float shown;                         // -1 = afbuigend, 0 = middenstand (kapot), 1 = rechtdoor (getoond)
        float shownBroken;
        float nextThrow;
        readonly Collider[] contacts = new Collider[24];
        Bounds occupancy;

        public TrackPiece Straight => straight;
        public TrackPiece Diverge => diverge;
        public bool IsDiverging => IsSpawned ? diverging.Value : startDiverging;
        public bool IsBroken => repair && repair.IsBroken;
        /// <summary>De route waar een trein die vanaf het begin oprijdt op terechtkomt.</summary>
        public TrackPiece ActiveRoute => IsDiverging ? diverge : straight;

        /// <summary>De wissel waar dit spoorstuk een route van is (of null).</summary>
        public static TrackSwitch For(TrackPiece piece) => piece && ByRoute.TryGetValue(piece, out var s) ? s : null;

        public void Configure(TrackPiece straightRoute, TrackPiece divergeRoute)
        {
            straight = straightRoute;
            diverge = divergeRoute;
        }

        void Awake()
        {
            repair = GetComponent<RepairPoint>();
            if (!straight) straight = GetComponent<TrackPiece>();
            bladeS = Find("Blade_S");
            bladeD = Find("Blade_D");
            tieBar = Find("Tie_Bar");
            lever = Find("Switch_Lever");
            target = Find("Switch_Target");
            toeNeutral = Local("Toe_Neutral");
            toeStraight = Local("Toe_Straight");
            toeDiverge = Local("Toe_Diverge");
            tipNeutral = Local("Lever_Tip_Neutral");
            tipStraight = Local("Lever_Tip_Straight");
            tipDiverge = Local("Lever_Tip_Diverge");
            tipBroken = Local("Lever_Tip_Broken");
            if (bladeS) { bladeSBase = bladeS.localRotation; heelS = transform.InverseTransformPoint(bladeS.position); }
            if (bladeD) bladeDBase = bladeD.localRotation;
            if (tieBar) tieBase = tieBar.localPosition;
            if (lever) { leverBase = lever.localRotation; leverPivot = transform.InverseTransformPoint(lever.position); }
            if (target) targetBase = target.localRotation;
            var s0 = Find("Socket_Start");
            var s1 = Find("Socket_Start_Fwd");
            trackAxis = s0 && s1 ? transform.InverseTransformDirection(s1.position - s0.position).normalized : Vector3.forward;
            var col = Find("Col_Main");
            var mf = col ? col.GetComponent<MeshFilter>() : null;
            occupancy = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(20f, 3f, 8f));
            shown = startDiverging ? -1f : 1f;
            shownBroken = repair && repair.IsBroken ? 1f : 0f;
            Pose(true);
        }

        void OnEnable()
        {
            if (straight) ByRoute[straight] = this;
            if (diverge) ByRoute[diverge] = this;
        }

        void OnDisable()
        {
            if (straight && ByRoute.TryGetValue(straight, out var a) && a == this) ByRoute.Remove(straight);
            if (diverge && ByRoute.TryGetValue(diverge, out var b) && b == this) ByRoute.Remove(diverge);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) diverging.Value = startDiverging;
        }

        /// <summary>Server: zet de wissel om (mag niet als hij kapot of bezet is).</summary>
        public bool ServerThrow(bool toDiverge)
        {
            if (!IsServer || IsBroken || diverging.Value == toDiverge || Occupied()) return false;
            diverging.Value = toDiverge;
            nextThrow = Time.time + 0.6f;
            return true;
        }

        [ContextMenu("Omzetten (server)")]
        void ThrowFromMenu() => ServerThrow(!diverging.Value);

        /// <summary>Staat er een trein of wagon op de wissel?</summary>
        public bool Occupied()
        {
            var b = occupancy;
            b.Expand(new Vector3(0.5f, 2f, 0.5f));
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(b.center), Vector3.Scale(b.extents, Abs(transform.lossyScale)),
                contacts, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (TrainDrive.IsTrainBody(contacts[i].attachedRigidbody)) return true;
            return false;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !lever || IsBroken || Time.time < nextThrow) return;
            // hand bij de knop die de hendel naar de andere stand duwt -> omzetten
            Vector3 from = diverging.Value ? tipDiverge : tipStraight;
            Vector3 to = diverging.Value ? tipStraight : tipDiverge;
            Vector3 knob = transform.TransformPoint(from);
            Vector3 push = transform.TransformDirection(to - from).normalized;
            int n = Physics.OverlapSphereNonAlloc(knob, 0.25f, contacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = contacts[i];
                var player = c.GetComponentInParent<PlaygroundPlayer>();
                if (!player || !player.IsSpawned || !player.IsHandCollider(c) || !c.attachedRigidbody) continue;
                if (Vector3.Dot(c.attachedRigidbody.GetPointVelocity(c.bounds.center), push) < pushSpeed) continue;
                if (ServerThrow(!diverging.Value)) break;
            }
        }

        void Update() => Pose(false);

        void Pose(bool instant)
        {
            float want = IsBroken ? 0f : (IsDiverging ? -1f : 1f);
            float wantBroken = IsBroken ? 1f : 0f;
            float k = instant ? 1f : 1f - Mathf.Exp(-Time.deltaTime * 9f);
            shown = Mathf.Lerp(shown, want, k);
            shownBroken = Mathf.Lerp(shownBroken, wantBroken, k);

            // tongen: draaien om hun hiel zodat de tongpunt naar de stand van de route schuift
            Vector3 toe = shown >= 0f ? Vector3.Lerp(toeNeutral, toeStraight, shown) : Vector3.Lerp(toeNeutral, toeDiverge, -shown);
            float angle = Vector3.SignedAngle(Flat(toeNeutral - heelS), Flat(toe - heelS), Vector3.up);
            var turn = Quaternion.AngleAxis(angle, Vector3.up);
            if (bladeS) bladeS.localRotation = ParentTurn(bladeS, turn) * bladeSBase;
            if (bladeD) bladeD.localRotation = ParentTurn(bladeD, turn) * bladeDBase;
            if (tieBar)
            {
                tieBar.localPosition = tieBase + tieBar.parent.InverseTransformVector(transform.TransformVector(toe - toeNeutral));
                RepairPoint.Show(tieBar, shownBroken < 0.5f);
            }
            // hendel: van rechtop naar de stand van de route, of hangend als hij kapot is
            if (lever)
            {
                Vector3 tip = shown >= 0f ? Vector3.Lerp(tipNeutral, tipStraight, shown) : Vector3.Lerp(tipNeutral, tipDiverge, -shown);
                tip = Vector3.Lerp(tip, tipBroken, shownBroken);
                lever.localRotation = ParentTurn(lever, Quaternion.FromToRotation(tipNeutral - leverPivot, tip - leverPivot)) * leverBase;
            }
            // richtingsschijf: plat naar de trein = rechtdoor, kwartslag = afbuigend, scheef = kapot
            if (target)
            {
                float yaw = Mathf.Lerp(90f, 0f, (shown + 1f) * 0.5f);
                var rot = Quaternion.AngleAxis(yaw + 35f * shownBroken, Vector3.up) * Quaternion.AngleAxis(22f * shownBroken, trackAxis);
                target.localRotation = ParentTurn(target, rot) * targetBase;
            }
        }

        Transform Find(string n) => RepairPoint.FindDeep(transform, n);

        Vector3 Local(string n)
        {
            var t = Find(n);
            return t ? transform.InverseTransformPoint(t.position) : Vector3.zero;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // draaiing in de ruimte van de wissel omzetten naar de ruimte van de ouder van een knoop
        Quaternion ParentTurn(Transform node, Quaternion inSwitchSpace)
        {
            if (node.parent == transform) return inSwitchSpace;
            var toParent = Quaternion.Inverse(node.parent.rotation) * transform.rotation;
            return toParent * inSwitchSpace * Quaternion.Inverse(toParent);
        }
    }
}
