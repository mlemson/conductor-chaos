using System.Collections.Generic;
using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Characters
{
    /// <summary>Oogstand van een personage. Open = gewone ogen, Lidded = halve oogleden (Lid_L/R),
    /// Closed = blij dichtgeknepen (EyeClosed_L/R, de ^-boogjes).</summary>
    public enum EyeState { Open, Lidded, Closed }

    /// <summary>Monden die in de personage-FBX's zitten (Mouth_*). Niet elk model heeft ze allemaal;
    /// een ontbrekende mond valt terug op Neutral.</summary>
    public enum MouthShape { Neutral, Surprised, Happy, Flat, Hmm, Smile, Grin }

    /// <summary>
    /// Gezicht van een personage uit ArtSource/Characters: wisselt tussen oogstanden en monden en laat het
    /// personage af en toe knipperen (Blink_L/R). Werkt op knoopnamen, dus op elke personage-FBX (conducteur,
    /// reizigers, crew). Puur cosmetisch en lokaal: er is geen netwerkstate en de server beslist hier niets.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Conductor Chaos/Character Face")]
    public sealed class CharacterFace : MonoBehaviour
    {
        static readonly string[] Sides = { "L", "R" };

        [Header("Expressie")]
        [Tooltip("Oogstand. Bij toevoegen overgenomen van wat het model standaard laat zien.")]
        [SerializeField] EyeState eyes = EyeState.Open;
        [SerializeField] MouthShape mouth = MouthShape.Neutral;

        [Header("Knipperen")]
        [SerializeField] bool blink = true;
        [Tooltip("Willekeurige tijd tussen twee knippers (seconden).")]
        [SerializeField] Vector2 blinkInterval = new Vector2(2.2f, 5.5f);
        [Tooltip("Hoe lang de ogen dicht zijn per knipper (seconden).")]
        [SerializeField, Range(0.05f, 0.3f)] float blinkDuration = 0.11f;
        [Tooltip("Kans dat een knipper meteen nog eens volgt.")]
        [SerializeField, Range(0f, 1f)] float doubleBlinkChance = 0.18f;

        [SerializeField, HideInInspector] bool captured;

        readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        bool indexed;
        bool blinking;
        bool pendingSecond;
        float timer;
        int blinksLeft;
        Transform gazeRoot, gazeEye;
        Transform leftPupil, rightPupil;
        Quaternion leftPupilRest, rightPupilRest;
        RaycastHit[] gazeHits;
        PlaygroundPlayer gazeTarget;
        float nextGazeCheck;

        public EyeState Eyes => eyes;
        public MouthShape Mouth => mouth;
        public bool IsBlinking => blinking;

        internal static CharacterFace OnModel(Transform model)
        {
            var face = model.GetComponentInChildren<CharacterFace>(true);
            return face ? face : model.gameObject.AddComponent<CharacterFace>();
        }

        /// <summary>Knipperen aan of uit (bijvoorbeeld uit tijdens een tussenfilmpje).</summary>
        public bool BlinkEnabled
        {
            get => blink;
            set { blink = value; if (!value) EndBlink(); }
        }

        public void SetEyes(EyeState state)
        {
            eyes = state;
            if (state == EyeState.Closed) EndBlink();
            Apply();
        }

        public void SetMouth(MouthShape shape)
        {
            mouth = shape;
            Apply();
        }

        public void SetExpression(EyeState state, MouthShape shape)
        {
            eyes = state;
            mouth = shape;
            if (state == EyeState.Closed) EndBlink();
            Apply();
        }

        /// <summary>Knippert nu meteen (bijvoorbeeld bij schrikken of een stoot).</summary>
        public void Blink(bool twice = false)
        {
            pendingSecond = false;
            StartBlink(twice ? 1 : 0);
        }

        /// <summary>Neemt de standaardstand van het model over (welke ogen en mond zichtbaar zijn).</summary>
        public void CaptureFromModel()
        {
            Index();
            if (IsShown("EyeClosed_L")) eyes = EyeState.Closed;
            else if (IsShown("Lid_L")) eyes = EyeState.Lidded;
            else eyes = EyeState.Open;
            mouth = MouthShape.Neutral;
            foreach (MouthShape m in System.Enum.GetValues(typeof(MouthShape)))
                if (IsShown("Mouth_" + m)) { mouth = m; break; }
            captured = true;
        }

        /// <summary>Of dit model een knooppunt heeft voor deze stand (bijv. Lid_L, Mouth_Grin).</summary>
        public bool Supports(EyeState state) => state == EyeState.Open || Find(state == EyeState.Lidded ? "Lid_L" : "EyeClosed_L");
        public bool Supports(MouthShape shape) => Find("Mouth_" + shape);

        void Reset()
        {
            CaptureFromModel();
        }

        void Awake()
        {
            Index();
            if (!captured) CaptureFromModel();          // ook bij AddComponent tijdens het spelen
            ScheduleNextBlink(first: true);
            Apply();
            if (!GetComponentInParent<PlaygroundPlayer>())
            {
                leftPupil = Find("Pupil_L");
                rightPupil = Find("Pupil_R");
                if (leftPupil) leftPupilRest = leftPupil.localRotation;
                if (rightPupil) rightPupilRest = rightPupil.localRotation;
                gazeEye = Find("Eye_L");
                if (!gazeEye) gazeEye = Find("Eye_R");
                if (gazeEye && (leftPupil || rightPupil))
                {
                    var owner = GetComponentInParent<NetworkObject>();
                    gazeRoot = owner ? owner.transform : transform;
                    gazeHits = new RaycastHit[12];
                }
            }
        }

        void OnEnable()
        {
            if (indexed) Apply();
        }

        void OnDisable()
        {
            if (blinking) EndBlink();
            if (leftPupil) leftPupil.localRotation = leftPupilRest;
            if (rightPupil) rightPupil.localRotation = rightPupilRest;
            gazeTarget = null;
            nextGazeCheck = 0f;
        }

        void LateUpdate()
        {
            if (gazeHits == null || !gazeEye) return;
            if (Time.time >= nextGazeCheck)
            {
                nextGazeCheck = Time.time + .16f;
                gazeTarget = FindVisiblePlayer();
            }

            Vector3 direction = gazeTarget && gazeTarget.IsSpawned
                ? transform.InverseTransformDirection((Head(gazeTarget) - gazeEye.position).normalized)
                : Vector3.zero;
            Quaternion turn = Quaternion.Euler(-direction.y * 16f, direction.x * 24f, 0f);
            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            if (leftPupil) leftPupil.localRotation = Quaternion.Slerp(leftPupil.localRotation, turn * leftPupilRest, blend);
            if (rightPupil) rightPupil.localRotation = Quaternion.Slerp(rightPupil.localRotation, turn * rightPupilRest, blend);
        }

        PlaygroundPlayer FindVisiblePlayer()
        {
            PlaygroundPlayer closest = null;
            float distanceLimit = 5f;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
            {
                if (!player || !player.IsSpawned || player.gameObject.scene != gameObject.scene) continue;
                Vector3 offset = Head(player) - gazeEye.position;
                float distance = offset.magnitude;
                if (distance < .05f || distance >= distanceLimit || Vector3.Dot(transform.forward, offset) < distance * .5f)
                    continue;
                int hits = Physics.RaycastNonAlloc(gazeEye.position, offset / distance, gazeHits, distance, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = hits == gazeHits.Length;
                for (int i = 0; i < hits && !blocked; i++)
                {
                    Transform hit = gazeHits[i].transform;
                    blocked = !hit.IsChildOf(gazeRoot) && !hit.IsChildOf(player.transform);
                }
                if (blocked) continue;
                closest = player;
                distanceLimit = distance;
            }
            return closest;
        }

        static Vector3 Head(PlaygroundPlayer player) => player.transform.position + player.transform.up * .86f;

        void Update()
        {
            if (!blink && !blinking) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            if (blinking)
            {
                blinking = false;
                Apply();
                if (blinksLeft > 0)
                {
                    blinksLeft--;
                    timer = 0.09f;                 // korte pauze, dan de tweede knipper
                    pendingSecond = true;
                }
                else ScheduleNextBlink(first: false);
                return;
            }

            if (pendingSecond)
            {
                pendingSecond = false;
                StartBlink(0);
                return;
            }

            if (blink) StartBlink(Random.value < doubleBlinkChance ? 1 : 0);
            else ScheduleNextBlink(first: false);
        }

        void StartBlink(int extra)
        {
            if (eyes == EyeState.Closed || !HasBlinkShape()) { ScheduleNextBlink(first: false); return; }
            blinking = true;
            blinksLeft = extra;
            timer = blinkDuration;
            Apply();
        }

        void EndBlink()
        {
            if (!blinking && !pendingSecond) return;
            blinking = false;
            pendingSecond = false;
            blinksLeft = 0;
            ScheduleNextBlink(first: false);
            Apply();
        }

        void ScheduleNextBlink(bool first)
        {
            float lo = Mathf.Max(0.3f, Mathf.Min(blinkInterval.x, blinkInterval.y));
            float hi = Mathf.Max(lo, Mathf.Max(blinkInterval.x, blinkInterval.y));
            // eerste knipper: willekeurig binnen het interval, zodat een groep personages niet tegelijk knippert
            timer = first ? Random.Range(0.2f, hi) : Random.Range(lo, hi);
        }

        bool HasBlinkShape() => Find("Blink_L") || Find("EyeClosed_L");

        /// <summary>Zet de knopen volgens oogstand, mond en knipperstand.</summary>
        public void Apply()
        {
            Index();
            bool useBlinkLid = blinking && Find("Blink_L");
            bool closedBlink = blinking && !useBlinkLid;      // oudere modellen zonder Blink_*: dichte boogjes
            foreach (var s in Sides)
            {
                bool closed = eyes == EyeState.Closed || closedBlink;
                bool lidded = eyes == EyeState.Lidded && !blinking && Find("Lid_" + s);
                Show("Eye_" + s, !closed);                   // het oog blijft onder het knipperooglid zitten
                Show("EyeClosed_" + s, closed);
                Show("Lid_" + s, lidded);
                Show("Blink_" + s, useBlinkLid);
            }
            var shown = Find("Mouth_" + mouth) ? mouth : MouthShape.Neutral;
            foreach (MouthShape m in System.Enum.GetValues(typeof(MouthShape)))
                Show("Mouth_" + m, m == shown);
        }

        void Index()
        {
            if (indexed && nodes.Count > 0) return;
            nodes.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Eye_") || n.StartsWith("Pupil_") || n.StartsWith("EyeClosed_") || n.StartsWith("Lid_") ||
                    n.StartsWith("Blink_") || n.StartsWith("Mouth_"))
                    if (!nodes.ContainsKey(n)) nodes.Add(n, t);
            }
            indexed = true;
        }

        Transform Find(string name)
        {
            Index();
            return nodes.TryGetValue(name, out var t) ? t : null;
        }

        bool IsShown(string name)
        {
            var t = Find(name);
            if (!t || !t.gameObject.activeSelf) return false;
            var r = t.GetComponent<Renderer>();
            return !r || r.enabled;
        }

        // FBX-zichtbaarheid komt in Unity binnen als uitgeschakelde Renderer; de showcase zet knopen inactief.
        // Daarom zetten we beide, en alleen de eigen renderer (Pupil_* volgt via Eye_* als kind-object).
        void Show(string name, bool on)
        {
            var t = Find(name);
            if (!t) return;
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            var r = t.GetComponent<Renderer>();
            if (r && r.enabled != on) r.enabled = on;
            if (on)
                foreach (var child in t.GetComponentsInChildren<Renderer>(true))
                    if (child.transform != t && child.transform.parent == t && !child.enabled) child.enabled = true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) { if (indexed) Apply(); return; }
            // SetActive mag niet tijdens OnValidate: even uitstellen, zodat de Inspector direct het gezicht wisselt
            UnityEditor.EditorApplication.delayCall += () =>
            {
                // alleen in een scène; een prefab- of FBX-asset zelf laten we ongemoeid
                if (this && gameObject.scene.IsValid()) { indexed = false; Apply(); }
            };
        }
#endif
    }
}
