// Conductor Chaos — de stationschef in de briefingruimte: geeft uitleg in tekstballonnen boven zijn hoofd.
// Puur lokale presentatie (geen netwerkstate): elke speler ziet dezelfde vaste volgorde, die op de scènetijd loopt.
// De ballon draait naar de camera, de tekst verschijnt letter voor letter en de mond beweegt mee (CharacterFace).
using ConductorChaos.Characters;
using ConductorChaos.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.Stations
{
    public sealed class BriefingSpeaker : MonoBehaviour
    {
        [SerializeField, TextArea] private string[] lines =
        {
            "Goedemorgen, crew! Welkom op Grand Whistleton.",
            "Ik ben de stationschef. Luister even goed, dan gaat er vandaag (bijna) niets mis.",
            "Bij het kledingrek kies je je tenue: conducteur, monteur of stoker. Pak er gewoon een vast!",
            "Op de tafel en in de kluisjes liggen extraatjes. Pak er een en hij gaat meteen op. Afzetten? Pech!",
            "Alles doe je met je handen: grijpen, trekken, duwen en tillen. Samen gaat het een stuk makkelijker.",
            "Op perron 1 oefenen we: koffers inladen, kaartjes knippen en de treindeur bedienen.",
            "Klaar? Loop door de hal naar perron 1. De trein staat al op jullie te wachten!"
        };
        [SerializeField] private float secondsPerLine = 6.5f;
        [SerializeField] private float lettersPerSecond = 38f;
        [SerializeField] private Vector3 bubbleOffset = new(0f, 1.75f, 0f);
        [SerializeField] private float pause = 1.2f;

        private Transform bubble;
        private Text label;
        private CharacterFace face;
        private int shownLine = -1;
        private float lineStart;

        private void Start()
        {
            face = GetComponentInChildren<CharacterFace>();
            BuildBubble();
        }

        private void BuildBubble()
        {
            var root = new GameObject("Tekstballon", typeof(RectTransform));
            bubble = root.transform;
            bubble.SetParent(transform, false);
            bubble.localPosition = bubbleOffset;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            var rt = (RectTransform)bubble;
            rt.sizeDelta = new Vector2(520f, 190f);
            rt.localScale = Vector3.one * 0.0042f;
            var body = ChaosUI.Panel(bubble, "Ballon", Vector2.zero, Vector2.one, ChaosUI.Paper, 4, ChaosUI.Ink);
            // staartje naar de spreker
            var tail = ChaosUI.Panel(bubble, "Staart", new Vector2(0.46f, -0.13f), new Vector2(0.54f, 0.06f), ChaosUI.Paper, 4, ChaosUI.Ink);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.SetSiblingIndex(0);
            label = ChaosUI.Label(body, "Tekst", "", 30, ChaosUI.Ink, TextAnchor.MiddleCenter);
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var name = ChaosUI.Label(body, "Naam", "STATIONSCHEF", 20, ChaosUI.Navy, TextAnchor.MiddleLeft, FontStyle.Bold,
                new Vector2(0.03f, 0.76f), new Vector2(0.7f, 0.97f));
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void LateUpdate()
        {
            if (!bubble || !label || lines == null || lines.Length == 0) return;
            float cycle = secondsPerLine + pause;
            float t = Time.timeSinceLevelLoad;
            int line = (int)(t / cycle) % lines.Length;
            float local = t % cycle;
            if (line != shownLine)
            {
                shownLine = line;
                lineStart = t - local;
            }
            bool speaking = local < secondsPerLine;
            bubble.gameObject.SetActive(speaking);
            if (!speaking)
            {
                if (face) face.SetMouth(MouthShape.Smile);
                return;
            }
            string text = lines[line];
            int count = Mathf.Clamp(Mathf.FloorToInt((t - lineStart) * lettersPerSecond), 0, text.Length);
            label.text = text.Substring(0, count);
            // korte 'pop' bij een nieuwe zin
            float pop = Mathf.Clamp01((t - lineStart) * 6f);
            bubble.localScale = Vector3.one * (0.0042f * Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0f, 1f, pop)));
            if (face)
            {
                bool talking = count < text.Length && Mathf.Repeat(t * 7f, 1f) < 0.5f;
                face.SetMouth(talking ? MouthShape.Surprised : MouthShape.Happy);
            }
            var cam = Camera.main;
            if (cam)
            {
                Vector3 toCamera = bubble.position - cam.transform.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 1e-4f) bubble.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            }
        }
    }
}
