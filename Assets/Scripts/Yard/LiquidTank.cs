// Conductor Chaos — ketelwagon die je vult met water of olie (issue #237). Zit op de wagon (naast zijn NetworkObject,
// toegevoegd door Tools > Conductor Chaos > Train Sets > Prepare Selected Vehicles op elk voertuig met een knoop
// TankLiquid). De server houdt het niveau en de soort bij; iedereen schuift de vloeistofspiegel TankLiquid mee
// (0 = vol tot de hals, -1,2 m = leeg) en kleurt hem. Het vulluik TankHatch is een echte klep: pak hem bij de beugel
// en trek hem open (scharnier om lokale X, 0 tot -105 graden), dan zie je het niveau en kan er een straal in.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Yard
{
    public enum LiquidKind : byte { Water, Oil }

    public sealed class LiquidTank : NetworkBehaviour
    {
        [SerializeField] float capacityLiters = 3000f;
        [Range(0f, 1f)] [SerializeField] float startLevel = 0.15f;
        [SerializeField] LiquidKind startKind = LiquidKind.Water;
        [Tooltip("Zo ver zakt de vloeistofspiegel van vol naar leeg (m, langs lokale Y).")]
        [SerializeField] float emptyDrop = 1.2f;
        [SerializeField] float inletRadius = 0.32f;

        static readonly List<LiquidTank> all = new();
        public static IReadOnlyList<LiquidTank> All => all;

        readonly NetworkVariable<float> level = new(0.15f);
        readonly NetworkVariable<byte> kind = new((byte)LiquidKind.Water);
        readonly NetworkVariable<float> hatchAngle = new(0f);

        Transform liquid, hatch;
        Vector3 fullPosition;
        Quaternion hatchClosed;
        Rigidbody hatchBody;
        HingeJoint hatchHinge;
        Renderer liquidRenderer;
        MaterialPropertyBlock block;
        float localLevel, pouredAt = -10f;
        LiquidKind localKind;
        byte shownKind = 255;

        public float Level => IsSpawned ? level.Value : localLevel;
        public LiquidKind Kind => IsSpawned ? (LiquidKind)kind.Value : localKind;
        public bool HatchOpen => hatch == null || Mathf.Abs(HatchAngle) > 25f;
        float HatchAngle => hatch ? HandLever.TwistAngle(Quaternion.Inverse(hatchClosed) * hatch.localRotation, Vector3.right) : -90f;

        /// <summary>Middelpunt van de vulopening (bovenkant van de hals).</summary>
        public Vector3 Inlet => liquid ? (liquid.parent ? liquid.parent.TransformPoint(fullPosition) : fullPosition) + liquid.up * 0.70f : transform.position;
        public float InletRadius => inletRadius;

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "TankLiquid") liquid = t;
                if (t.name == "TankHatch") hatch = t;
            }
            if (liquid)
            {
                fullPosition = liquid.localPosition;
                liquidRenderer = liquid.GetComponent<Renderer>();
            }
            if (hatch) hatchClosed = hatch.localRotation;
            localLevel = startLevel;
            localKind = startKind;
            block = new MaterialPropertyBlock();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                level.Value = startLevel;
                kind.Value = (byte)startKind;
                if (hatch && GetComponent<Rigidbody>()) MakeHatchPhysical();
            }
        }

        /// <summary>Server (of zonder netwerk): liters erbij. Een andere soort vloeistof wint pas als de tank bijna leeg is.</summary>
        public void Pour(float liters, LiquidKind what)
        {
            if (IsSpawned && !IsServer) return;
            float add = liters / Mathf.Max(1f, capacityLiters);
            float now = Mathf.Clamp01(Level + add);
            pouredAt = Time.time;
            if (IsSpawned)
            {
                if (Level < 0.05f) kind.Value = (byte)what;
                level.Value = now;
            }
            else
            {
                if (localLevel < 0.05f) localKind = what;
                localLevel = now;
            }
        }

        void MakeHatchPhysical()
        {
            // het luik is een klepje met eigen gewicht aan een scharnier: dicht valt het vanzelf dicht
            var col = hatch.GetComponent<MeshCollider>();
            if (col) col.convex = true;
            hatchBody = hatch.gameObject.AddComponent<Rigidbody>();
            hatchBody.mass = 3f;
            hatchBody.angularDamping = 2f;
            hatchBody.interpolation = RigidbodyInterpolation.Interpolate;
            hatchHinge = hatch.gameObject.AddComponent<HingeJoint>();
            hatchHinge.axis = Vector3.right;
            hatchHinge.anchor = Vector3.zero;
            hatchHinge.connectedBody = GetComponent<Rigidbody>();
            hatchHinge.useLimits = true;
            hatchHinge.limits = new JointLimits { min = -105f, max = 0f };
            hatchHinge.enableCollision = false;
            foreach (var other in GetComponentsInChildren<Collider>(true))
                if (col && other != col) Physics.IgnoreCollision(col, other, true);
        }

        void FixedUpdate()
        {
            if (IsSpawned && IsServer && hatchBody) hatchAngle.Value = HatchAngle;
        }

        void LateUpdate()
        {
            if (hatch && IsSpawned && !IsServer)
                hatch.localRotation = hatchClosed * Quaternion.AngleAxis(hatchAngle.Value, Vector3.right);
            if (!liquid) return;
            liquid.localPosition = fullPosition + Vector3.up * (-emptyDrop * (1f - Level));
            if (liquidRenderer) liquidRenderer.enabled = Level > 0.005f;
            var k = Kind;
            bool pouring = Time.time - pouredAt < 0.4f;
            if ((byte)k != shownKind || pouring != (block.GetFloat(FlowId) > 0f))
            {
                shownKind = (byte)k;
                if (liquidRenderer)
                {
                    liquidRenderer.GetPropertyBlock(block);
                    LiquidLook.Apply(block, k, pouring ? 0.8f : 0f);
                    liquidRenderer.SetPropertyBlock(block);
                }
            }
        }

        static readonly int FlowId = Shader.PropertyToID("_Flow");
    }

    /// <summary>Kleuren uit palette.json (water, ui_bg voor olie) voor de StylizedRamp-shader.</summary>
    public static class LiquidLook
    {
        static readonly Color[] Water = { Hex(0x6B8CB6), Hex(0x83C2EB), Hex(0x8EC7EC), Hex(0x9ACCEC), Hex(0xADD7ED) };
        static readonly Color[] Oil = { Hex(0x2A2730), Hex(0x302629), Hex(0x33303B), Hex(0x403E4B), Hex(0x4A4852) };
        static readonly int[] RampIds =
        {
            Shader.PropertyToID("_Ramp0"), Shader.PropertyToID("_Ramp1"), Shader.PropertyToID("_Ramp2"),
            Shader.PropertyToID("_Ramp3"), Shader.PropertyToID("_Ramp4"),
        };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        public static Color Base(LiquidKind kind) => (kind == LiquidKind.Oil ? Oil : Water)[2];

        public static void Apply(MaterialPropertyBlock block, LiquidKind kind, float flow)
        {
            var ramp = kind == LiquidKind.Oil ? Oil : Water;
            for (int i = 0; i < 5; i++) block.SetColor(RampIds[i], ramp[i]);
            block.SetColor("_BaseColor", ramp[2]);
            block.SetFloat("_Flow", flow);
            block.SetFloat("_Spec", kind == LiquidKind.Oil ? 1.1f : 0.7f);
            block.SetFloat("_Rim", kind == LiquidKind.Oil ? 0.55f : 0.35f);
            block.SetFloat("_Cells", 0f);
            block.SetFloat("_Grime", 0f);
        }

        static Material shared;

        /// <summary>Materiaal voor stralen en spetters (StylizedRamp; kleur per renderer via Apply).</summary>
        public static Material Material
        {
            get
            {
                if (shared) return shared;
                var shader = Shader.Find("ConductorChaos/StylizedRamp");
                shared = new Material(shader ? shader : Shader.Find("Universal Render Pipeline/Lit")) { name = "Liquid (runtime)" };
                shared.SetFloat("_FlowStreaks", 0.8f);
                return shared;
            }
        }
    }
}
