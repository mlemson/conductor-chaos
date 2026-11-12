// Conductor Chaos — iets dat kapot kan zijn en met de moersleutel wordt gerepareerd (issue #215).
// Werkt op knoopnamen uit de spoorkit: Repair_Bolt (bout die uitsteekt) en Repair_Bolt_Home (bout helemaal erin),
// Broken_Bits (alleen zichtbaar als het kapot is) en Fixed_Rail (alleen zichtbaar als het heel is).
// Onderhoudspunten op treinen (knoop RepairPoint_<Naam>) gebruiken dezelfde namen met achtervoegsel __<Naam>.
// Lomp en fysiek: elke harde tik met de kop van de sleutel tegen de bout ramt hem een stukje verder erin; na genoeg
// tikken is het heel. De server telt de tikken en beslist; clients tonen alleen de stand.
using System;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Track
{
    [DisallowMultipleComponent]
    public sealed class RepairPoint : NetworkBehaviour
    {
        [Tooltip("Kapot bij het starten van de sessie.")]
        [SerializeField] bool startBroken = true;
        [Tooltip("Aantal harde tikken met de moersleutel om het te repareren.")]
        [SerializeField, Range(1, 12)] int requiredHits = 4;
        [Tooltip("Minimale botssnelheid van de sleutel tegen de bout (m/s).")]
        [SerializeField] float minImpactSpeed = 1.4f;
        [Tooltip("Rust tussen twee tikken (s), zodat één klap niet dubbel telt.")]
        [SerializeField] float hitCooldown = 0.2f;

        readonly NetworkVariable<bool> broken = new NetworkVariable<bool>(true);
        readonly NetworkVariable<int> hits = new NetworkVariable<int>(0);

        Transform bolt, bitsBroken, bitsFixed;
        Vector3 boltOut, boltHome;
        SphereCollider boltCollider;
        float shownProgress, nextHit;

        /// <summary>Gaat af (op server en clients) als het kapot gaat of gerepareerd wordt.</summary>
        public event Action<bool> BrokenChanged;
        /// <summary>Gaat af bij elke getelde tik (voor geluid/effect), met de voortgang 0..1.</summary>
        public event Action<float> Hit;

        public bool IsBroken => IsSpawned ? broken.Value : startBroken;
        public bool StartsBroken => startBroken;

        /// <summary>Door de prefabbouwer: kapot beginnen of niet.</summary>
        public void Configure(bool brokenAtStart) => startBroken = brokenAtStart;
        public float Progress => requiredHits <= 0 ? 1f : Mathf.Clamp01(hits.Value / (float)requiredHits);
        public Collider BoltCollider => boltCollider;
        public int RequiredHits => requiredHits;
        public float MinImpactSpeed => minImpactSpeed;

        void Awake()
        {
            bolt = FindPart(transform, "Repair_Bolt");
            var home = FindPart(transform, "Repair_Bolt_Home");
            bitsBroken = FindPart(transform, "Broken_Bits");
            bitsFixed = FindPart(transform, "Fixed_Rail");
            if (bolt)
            {
                boltOut = bolt.localPosition;
                boltHome = home ? bolt.parent.InverseTransformPoint(home.position) : boltOut;
                boltCollider = bolt.GetComponent<SphereCollider>();
                if (!boltCollider) boltCollider = bolt.gameObject.AddComponent<SphereCollider>();
                boltCollider.radius = 0.09f;
                boltCollider.center = Vector3.up * 0.02f;
            }
            ShowState(startBroken, 0f, instant: true);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                broken.Value = startBroken;
                hits.Value = 0;
            }
            broken.OnValueChanged += OnBrokenChanged;
            ShowState(broken.Value, Progress, instant: true);
        }

        public override void OnNetworkDespawn() => broken.OnValueChanged -= OnBrokenChanged;

        void OnBrokenChanged(bool before, bool now)
        {
            ShowState(now, Progress, instant: false);
            BrokenChanged?.Invoke(now);
        }

        /// <summary>Server: een klap met de sleutel tegen de bout. Telt alleen als hij hard genoeg is.</summary>
        public bool ServerHit(float impactSpeed)
        {
            if (!IsServer || !broken.Value || impactSpeed < minImpactSpeed || Time.time < nextHit) return false;
            nextHit = Time.time + hitCooldown;
            hits.Value = Mathf.Min(requiredHits, hits.Value + 1);
            HitClientRpc(Progress);
            if (hits.Value >= requiredHits) broken.Value = false;
            return true;
        }

        /// <summary>Server: maak het (weer) kapot, bijvoorbeeld bij het begin van een missie.</summary>
        public void ServerBreak()
        {
            if (!IsServer) return;
            hits.Value = 0;
            broken.Value = true;
        }

        [ContextMenu("Kapot maken (server)")]
        void BreakFromMenu() => ServerBreak();

        [ContextMenu("Repareren (server)")]
        void RepairFromMenu()
        {
            if (!IsServer) return;
            hits.Value = requiredHits;
            broken.Value = false;
        }

        [ClientRpc]
        void HitClientRpc(float progress) => Hit?.Invoke(progress);

        void Update()
        {
            if (!bolt) return;
            float target = IsBroken ? Progress : 1f;
            // de bout zakt snel bij elke tik (een beetje verend), zodat je ziet dat de klap telde
            shownProgress = Mathf.MoveTowards(shownProgress, target, Time.deltaTime * 3f);
            bolt.localPosition = Vector3.LerpUnclamped(boltOut, boltHome, shownProgress);
        }

        void ShowState(bool isBroken, float progress, bool instant)
        {
            Show(bitsBroken, isBroken);
            Show(bitsFixed, !isBroken);
            if (instant)
            {
                shownProgress = isBroken ? progress : 1f;
                if (bolt) bolt.localPosition = Vector3.LerpUnclamped(boltOut, boltHome, shownProgress);
            }
        }

        // Verborgen FBX-knopen komen in Unity binnen met een uitgeschakelde renderer: object én renderers aanzetten.
        internal static void Show(Transform node, bool on)
        {
            if (!node) return;
            if (node.gameObject.activeSelf != on) node.gameObject.SetActive(on);
            if (on)
                foreach (var r in node.GetComponentsInChildren<Renderer>(true))
                    if (!r.enabled) r.enabled = true;
        }

        // Een trein heeft meerdere onderhoudspunten in één model en knoopnamen in een FBX zijn uniek: daar heten de
        // onderdelen Repair_Bolt__Fan, Broken_Bits__Fan, ... (train_kit.py). Eerst de kale naam, dan die met achtervoegsel.
        internal static Transform FindPart(Transform root, string name)
        {
            var exact = FindDeep(root, name);
            return exact ? exact : FindPrefixed(root, name + "__");
        }

        static Transform FindPrefixed(Transform root, string prefix)
        {
            if (root.name.StartsWith(prefix, StringComparison.Ordinal)) return root;
            foreach (Transform child in root)
            {
                var hit = FindPrefixed(child, prefix);
                if (hit) return hit;
            }
            return null;
        }

        internal static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = FindDeep(child, name);
                if (hit) return hit;
            }
            return null;
        }
    }
}
