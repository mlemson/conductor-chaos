// Conductor Chaos — reiziger met een treinkaartje (issue #216). De reiziger houdt het kaartje omhoog in zijn
// (fysieke, wiebelende) rechterhand; met de kaartjestang (TicketPunch) tik je er een gaatje in. Daarna laat hij de arm
// zakken en kijkt blij. De server beslist of er geknipt is; iedereen ziet het gaatje en het gezicht.
// Werkt naast PlaygroundPassenger (die maakt de armen fysiek op de server).
using ConductorChaos.Characters;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    public sealed class PassengerTicket : NetworkBehaviour
    {
        [Tooltip("Kaartjesmodel (Prop_Ticket uit de stationskit).")]
        [SerializeField] GameObject ticketPrefab;
        [Tooltip("Houdt het kaartje omhoog tot het geknipt is.")]
        [SerializeField] bool holdUp = true;
        [Tooltip("Kracht waarmee de arm omhoog gehouden wordt (lager = wiebeliger, dus lastiger).")]
        [SerializeField] float armSpring = 55f;

        readonly NetworkVariable<bool> punched = new NetworkVariable<bool>(false);

        Transform arm, ticket, hole;
        static readonly Vector3 TicketGrip = new(0f, -0.33f, 0.05f);
        BoxCollider ticketCollider;
        ConfigurableJoint shoulder;
        CharacterFace face;
        bool armRaised;
        Quaternion armRest;

        public bool IsPunched => punched.Value;

        public void Configure(GameObject prefab) => ticketPrefab = prefab;

        internal bool IsTicketCollider(Collider candidate) => ticketCollider && candidate == ticketCollider;

        public override void OnNetworkSpawn()
        {
            Transform model = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (model)
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Arm_R") arm = t;
            if (!arm || !ticketPrefab) { Debug.LogWarning("PassengerTicket: Arm_R of kaartjesmodel ontbreekt.", this); return; }
            armRest = arm.localRotation;                       // de stand waarin het schoudergewricht begint
            face = CharacterFace.OnModel(model);

            ticket = Instantiate(ticketPrefab, arm).transform;
            ticket.name = "Ticket";
            LateUpdate();
            foreach (var c in ticket.GetComponentsInChildren<Collider>(true)) Destroy(c);
            hole = ConductorChaos.Track.RepairPoint.FindDeep(ticket, "Ticket_Hole");
            if (IsServer)
            {
                ticketCollider = ticket.gameObject.AddComponent<BoxCollider>();
                ticketCollider.size = new Vector3(0.14f, 0.075f, 0.012f);
                ticketCollider.isTrigger = true;
                punched.Value = false;
            }
            punched.OnValueChanged += OnPunched;
            ShowPunched(punched.Value);
        }

        void LateUpdate()
        {
            if (!ticket || !arm) return;
            // Keep the printed face visible even when the physical wrist twists.
            ticket.SetPositionAndRotation(arm.TransformPoint(TicketGrip) + transform.forward * .055f,
                Quaternion.LookRotation(transform.forward, transform.up));
        }

        public override void OnNetworkDespawn()
        {
            punched.OnValueChanged -= OnPunched;
            if (shoulder && armRaised) shoulder.slerpDrive = new JointDrive();
            if (ticket) Destroy(ticket.gameObject);
        }

        /// <summary>Server: het kaartje is geknipt.</summary>
        public void ServerPunch()
        {
            if (!IsServer || punched.Value) return;
            punched.Value = true;
        }

        /// <summary>Server: een nieuw (ongeknipt) kaartje, bijvoorbeeld voor de volgende rit.</summary>
        public void ServerNewTicket()
        {
            if (!IsServer) return;
            punched.Value = false;
        }

        [ContextMenu("Nieuw kaartje (server)")]
        void NewTicketFromMenu() => ServerNewTicket();

        void OnPunched(bool before, bool now) => ShowPunched(now);

        void ShowPunched(bool now)
        {
            if (hole) ConductorChaos.Track.RepairPoint.Show(hole, now);
            if (face) face.SetExpression(now ? EyeState.Closed : EyeState.Open, now ? MouthShape.Happy : MouthShape.Neutral);
            if (now && face) Invoke(nameof(Smile), 1.2f);
        }

        void Smile()
        {
            if (face && punched.Value) face.SetExpression(EyeState.Open, MouthShape.Smile);
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || !arm) return;
            if (!shoulder) shoulder = arm.GetComponent<ConfigurableJoint>();   // PlaygroundPassenger maakt hem op de server
            if (!shoulder) return;
            bool raise = holdUp && !punched.Value;
            if (raise == armRaised) return;
            armRaised = raise;
            if (raise)
            {
                // arm naar voren en iets omhoog, gemeten in de ruimte van het lichaam (los van de modelassen)
                Quaternion rest = arm.parent.rotation * armRest;
                Vector3 hang = rest * Vector3.down;
                Vector3 reach = (transform.forward * 0.85f + Vector3.up * 0.35f).normalized;
                Quaternion world = Quaternion.FromToRotation(hang, reach);
                Quaternion local = Quaternion.Inverse(rest) * world * rest;
                shoulder.rotationDriveMode = RotationDriveMode.Slerp;
                shoulder.slerpDrive = new JointDrive { positionSpring = armSpring, positionDamper = 3f, maximumForce = 400f };
                shoulder.targetRotation = Quaternion.Inverse(local);
            }
            else
            {
                shoulder.slerpDrive = new JointDrive();
                shoulder.targetRotation = Quaternion.identity;
            }
        }
    }
}
