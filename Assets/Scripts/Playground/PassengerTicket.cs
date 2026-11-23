// Conductor Chaos — reiziger met een treinkaartje (issue #216). De reiziger houdt het kaartje omhoog in zijn
// (fysieke, wiebelende) rechterhand; met de kaartjestang (TicketPunch) tik je er een gaatje in. Daarna laat hij de arm
// zakken. De server beslist of het kaartje geldig is en verwerkt een knip voor de hele ploeg.
// Werkt naast PlaygroundPassenger (die maakt de armen fysiek op de server).
using ConductorChaos.Characters;
using ConductorChaos.Stations;
using ConductorChaos.Track;
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

        readonly NetworkVariable<bool> wrong = new(false);
        readonly NetworkVariable<byte> fare = new();
        private TextMesh[] fareMarks;
        internal int Fare => fare.Value - 1;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        Transform arm, ticket, modelTicket, hole;
        BoxCollider ticketCollider, modelTicketCollider;
        ConfigurableJoint shoulder;
        CharacterFace face;
        bool armRaised;
        Quaternion armRest;

        internal bool IsWrong => wrong.Value;
        public bool IsPunched => punched.Value && !IsWrong;

        public void Configure(GameObject prefab) => ticketPrefab = prefab;

        internal bool CanCut(Collider candidate) => IsSpawned && !punched.Value &&
            (candidate == ticketCollider || candidate == modelTicketCollider);

        internal void ServerSetWrong()
        {
            if (IsServer && IsSpawned) wrong.Value = true;
        }

        internal void ServerSetFare(int color, bool rejected)
        {
            if (!IsServer || !IsSpawned) return;
            fare.Value = (byte)(color + 1);
            wrong.Value = rejected;
        }

        public override void OnNetworkSpawn()
        {
            Transform model = transform.childCount > 0 ? transform.GetChild(0) : null;
            arm = model ? RepairPoint.FindDeep(model, "Arm_R") : null;
            if (!arm || !ticketPrefab) { Debug.LogWarning("PassengerTicket: Arm_R of kaartjesmodel ontbreekt.", this); return; }
            armRest = arm.localRotation;
            face = CharacterFace.OnModel(model);

            modelTicket = RepairPoint.FindDeep(arm, "Prop_Ticket");
            Transform hand = RepairPoint.FindDeep(arm, "Hand_R");
            Quaternion hold = Quaternion.Euler(8f, 0f, -32f);
            ticket = Instantiate(ticketPrefab, hand ? hand : arm).transform;
            ticket.name = "Ticket";
            ticket.localRotation = hold;
            // The inner edge is in the palm, with the printed end and punch area sticking out.
            ticket.localPosition = (hand ? Vector3.zero : Vector3.down * .21f) +
                new Vector3(0f, -.035f, .05f) + hold * Vector3.right * .035f;
            foreach (var c in ticket.GetComponentsInChildren<Collider>(true)) Destroy(c);
            hole = RepairPoint.FindDeep(ticket, "Ticket_Hole");
            if (IsServer)
            {
                punched.Value = false;
                wrong.Value = false;
                fare.Value = 0;
                ticketCollider = AddTicketCollider(ticket);
                if (modelTicket) modelTicketCollider = AddTicketCollider(modelTicket);
            }
            wrong.OnValueChanged += OnWrongChanged;
            fare.OnValueChanged += OnFareChanged;
            punched.OnValueChanged += OnPunched;
            ShowTicket();
            ShowPunched(punched.Value);
        }

        static BoxCollider AddTicketCollider(Transform card)
        {
            var collider = card.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            if (card.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                collider.center = bounds.center;
                collider.size = Vector3.Max(bounds.size, Vector3.one * .012f);
            }
            else collider.size = new Vector3(.14f, .075f, .012f);
            return collider;
        }

        void OnWrongChanged(bool before, bool now) => ShowTicket();
        void OnFareChanged(byte before, byte now) => ShowTicket();

        void ShowTicket()
        {
            if (!ticket) return;
            if (Fare >= 0)
            {
                if (modelTicket) modelTicket.gameObject.SetActive(false);
                ticket.gameObject.SetActive(true);
                ShowFareMark();
                return;
            }
            if (modelTicket)
            {
                modelTicket.gameObject.SetActive(IsWrong);
                ticket.gameObject.SetActive(!IsWrong);
                return;
            }

            // The dark-blue stub remains the visual cue for valid tickets.
            var renderer = ticket.GetComponentInChildren<MeshRenderer>();
            if (!renderer) return;
            renderer.SetPropertyBlock(null);
            if (!IsWrong) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, new Color32(197, 77, 62, 255));
            renderer.SetPropertyBlock(block);
        }

        private void ShowFareMark()
        {
            var renderer = ticket.GetComponentInChildren<MeshRenderer>();
            if (!renderer) return;
            Color color = ServiceManifest.FareColor(Fare);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            var materials = renderer.sharedMaterials;
            int stub = System.Array.FindIndex(materials, material => material && material.name.Contains("Navy"));
            if (stub >= 0) renderer.SetPropertyBlock(block, stub);
            else renderer.SetPropertyBlock(block);
            if (fareMarks == null)
            {
                fareMarks = new TextMesh[2];
                for (int i = 0; i < fareMarks.Length; i++)
                {
                    var label = new GameObject("Kaartcode").AddComponent<TextMesh>();
                    label.transform.SetParent(ticket, false);
                    label.transform.SetLocalPositionAndRotation(new Vector3(.046f, 0f, i == 0 ? -.005f : .005f),
                        Quaternion.Euler(0f, 180f * i, 0f));
                    label.anchor = TextAnchor.MiddleCenter;
                    label.alignment = TextAlignment.Center;
                    label.fontSize = 42;
                    label.characterSize = .007f;
                    label.color = new Color32(255, 241, 213, 255);
                    fareMarks[i] = label;
                }
            }
            foreach (var label in fareMarks)
                label.text = ServiceManifest.FareMark(Fare);
        }

        public override void OnNetworkDespawn()
        {
            wrong.OnValueChanged -= OnWrongChanged;
            fare.OnValueChanged -= OnFareChanged;
            fareMarks = null;
            punched.OnValueChanged -= OnPunched;
            CancelInvoke(nameof(Smile));
            if (shoulder && armRaised) shoulder.slerpDrive = new JointDrive();
            armRaised = false;
            if (ticket) Destroy(ticket.gameObject);
            if (modelTicketCollider) Destroy(modelTicketCollider);
            if (modelTicket) modelTicket.gameObject.SetActive(true);
        }

        internal bool ServerPunch()
        {
            if (!IsServer || !IsSpawned || punched.Value) return false;
            var run = ServiceRun.Active;
            if (IsWrong && run && run.gameObject.scene == gameObject.scene && !run.TryApplyWrongTicketPenalty()) return false;
            punched.Value = true;
            return true;
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
            if (hole) RepairPoint.Show(hole, now && !IsWrong);
            if (!face) return;
            CancelInvoke(nameof(Smile));
            face.SetExpression(now && !IsWrong ? EyeState.Closed : EyeState.Open,
                now ? (IsWrong ? MouthShape.Flat : MouthShape.Happy) : MouthShape.Neutral);
            if (now && !IsWrong) Invoke(nameof(Smile), 1.2f);
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
                shoulder.targetRotation = Quaternion.Inverse(Quaternion.RotateTowards(Quaternion.identity, local, 65f));
            }
            else
            {
                shoulder.slerpDrive = new JointDrive();
                shoulder.targetRotation = Quaternion.identity;
            }
        }
    }
}
