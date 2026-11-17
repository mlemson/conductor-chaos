// Conductor Chaos — compacte opdrachtkaart linksboven (issue #239). Rode kop met level en klok, één korte zin met de
// volgende stap en daaronder een afvinklijst. De kaart is zo hoog als de inhoud; geen lege vlakken, geen toetsprompts.
// Puur lokale presentatie van wat de server via ServiceRun doorgeeft.
using System.Collections.Generic;
using ConductorChaos.UI.Depot;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    internal sealed class ObjectiveCard
    {
        internal readonly struct Row
        {
            public readonly string Title;
            public readonly int Done, Target;
            public Row(string title, int done, int target) { Title = title; Done = done; Target = target; }
            public bool Complete => Done >= Target;
        }

        private const float Left = 16f, Top = 16f, Width = 320f, Pad = 14f, HeaderHeight = 40f, RowHeight = 24f;
        private const int StepSize = 15, RowSize = 14;
        private static readonly Color Divider = new(ChaosUI.Ink.r, ChaosUI.Ink.g, ChaosUI.Ink.b, .14f);

        private readonly Transform canvas;
        private RectTransform root;
        private Text timer;
        private string signature;

        internal ObjectiveCard(Transform canvas) => this.canvas = canvas;

        internal void Hide()
        {
            if (root && root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }

        internal void Show(string label, string icon, string time, bool urgent, string step, IReadOnlyList<Row> rows)
        {
            var key = new System.Text.StringBuilder(label).Append('|').Append(icon).Append('|').Append(step);
            if (rows != null)
                foreach (var row in rows) key.Append('|').Append(row.Title).Append(row.Done).Append('/').Append(row.Target);
            string next = key.ToString();
            if (!root || next != signature)
            {
                signature = next;
                Build(label, icon, step, rows);
            }
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            timer.text = time;
            timer.color = urgent ? ChaosUI.TrainRed : ChaosUI.Gold;
        }

        private void Build(string label, string icon, string step, IReadOnlyList<Row> rows)
        {
            if (root) Object.Destroy(root.gameObject);
            float textWidth = Width - Pad * 2f;
            float stepHeight = string.IsNullOrEmpty(step) ? 0f : Mathf.Ceil(DepotUi.MeasureHeight(step, StepSize, false, textWidth));
            int count = rows?.Count ?? 0;
            float height = HeaderHeight + (stepHeight > 0f ? 10f + stepHeight : 0f) +
                           (count > 0 ? 11f + count * RowHeight : 0f) + 10f;

            root = DepotUi.Box(canvas, "Opdrachtkaart", Left, Top, Width, height);
            var card = DepotUi.Panel(root, "Kaart", 0, 0, Width, height, ChaosUI.Paper, ChaosUI.TrainRedDark);
            var header = DepotUi.Panel(card, "Kop", 2, 2, Width - 4, HeaderHeight, ChaosUI.TrainRed);
            DepotUi.Icon(header, icon, 9, 8, 24);
            DepotUi.Txt(header, label, 40, 0, Width - 140, HeaderHeight, 17, ChaosUI.Paper, true);
            var clock = DepotUi.Panel(header, "Dienstklok", Width - 4 - 88, 6, 80, HeaderHeight - 12, ChaosUI.Dark);
            timer = DepotUi.Txt(clock, "", 0, 0, 80, HeaderHeight - 12, 18, ChaosUI.Gold, true, TextAnchor.MiddleCenter);

            float y = HeaderHeight + 10f;
            if (stepHeight > 0f)
            {
                var text = DepotUi.Txt(card, step, Pad, y, textWidth, stepHeight, StepSize, ChaosUI.Ink, false, TextAnchor.UpperLeft);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                y += stepHeight;
            }
            if (count == 0) return;
            y += 5f;
            DepotUi.Panel(card, "Lijn", Pad, y, textWidth, 1, Divider);
            y += 6f;
            foreach (var row in rows)
            {
                bool complete = row.Complete;
                if (complete)
                {
                    var box = DepotUi.Panel(card, "Klaar", Pad, y + 5f, 14, 14, ChaosUI.Green);
                    DepotUi.Icon(box, "ready", 1, 1, 12, Color.white);
                }
                else DepotUi.Panel(card, "Open", Pad, y + 5f, 14, 14, ChaosUI.Paper, ChaosUI.Muted);
                bool showCount = row.Target > 1 && !complete;
                DepotUi.Txt(card, row.Title, Pad + 24, y, textWidth - 24 - (showCount ? 44 : 0), RowHeight, RowSize,
                    complete ? ChaosUI.Muted : ChaosUI.Ink);
                if (showCount)
                    DepotUi.Txt(card, $"{row.Done}/{row.Target}", Width - Pad - 44, y, 44, RowHeight, 13, ChaosUI.Muted, true, TextAnchor.MiddleRight);
                y += RowHeight;
            }
        }
    }
}
