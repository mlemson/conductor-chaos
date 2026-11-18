// Conductor Chaos — resultaat na een gehaalde dienst (issue #239): verdiende kaartjes, wat er vrijkwam en het volgende doel.
// Elke speler krijgt de kaartjes in zijn eigen profiel; de host kiest wat er daarna gebeurt.
using ConductorChaos.Depot;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI.Depot
{
    internal sealed class DepotResults
    {
        private readonly Canvas canvas;
        private readonly RectTransform stage;
        public bool Visible => canvas.gameObject.activeSelf;

        public DepotResults(Transform parent)
        {
            canvas = ChaosUI.MakeCanvas(parent, "Dienst gehaald", true);
            canvas.sortingOrder = 51;
            var veil = ChaosUI.Panel(canvas.transform, "Sluier", Vector2.zero, Vector2.one, new Color(.08f, .055f, .067f, .62f));
            veil.GetComponent<Image>().raycastTarget = true;
            stage = ChaosUI.Rect(canvas.transform, "Resultaatvlak", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            stage.gameObject.AddComponent<StageFitter>();
            canvas.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
        }

        /// <param name="next">Volgende level-index of -1.</param>
        public void Show(RunReward reward, LevelInfo level, string from, string to, int crew, bool isHost, int next,
            System.Action goNext, System.Action again, System.Action leave)
        {
            canvas.gameObject.SetActive(true);
            DepotUi.Clear(stage);

            var left = DepotUi.Panel(stage, "Kaartjes", 48, 40, 720, 820, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Txt(left, "Dienst gehaald!", 30, 22, 440, 64, 54, ChaosUI.Paper, true);
            DepotUi.Chip(left, string.IsNullOrEmpty(to) ? "Training af" : $"Op tijd in {to}", 690, 36, ChaosUI.Green, DepotUi.GreenText, "ready", 16, 38, true);
            string train = DepotCatalog.Train(DepotLoadout.Active.Train).Name;
            DepotUi.Txt(left, $"{level.Label} · {level.Name} · crew van {crew} · {train}", 32, 92, 660, 28, 17, ChaosUI.Muted);

            float y = 140;
            Line(left, ref y, "route", reward.FirstWin && level.Index > 0 ? "Dienst gehaald · eerste keer" : "Dienst gehaald", null, reward.Base);
            if (level.Index > 0) Line(left, ref y, "speed-gauge", $"Tijd over: {DepotUi.Time(reward.SecondsLeft)}", $"{DepotProgress.TicketsPerSecond} per seconde", reward.TimeBonus);
            Line(left, ref y, "ticket", $"Opdrachten gehaald: {reward.ObjectivesDone}", $"{DepotProgress.TicketsPerObjective} per opdracht", reward.ObjectiveBonus);
            if (level.Index > 0) Line(left, ref y, "wrench", reward.RepairBonus > 0 ? "Alle storingen gerepareerd" : "Nog iets kapot bij aankomst", null, reward.RepairBonus);
            if (reward.ExtraPassengers > 0)
                Line(left, ref y, "player-group", $"Extra reizigers · {reward.ExtraPassengers}", $"{DepotProgress.TicketsPerExtraPassenger} per reiziger", reward.PassengerBonus);
            y += 10;
            DepotUi.Icon(left, "ticket", 30, y, 50);
            DepotUi.Txt(left, "Totaal", 92, y, 200, 50, 30, ChaosUI.Paper, true);
            DepotUi.Txt(left, "+" + DepotCatalog.FormatTickets(reward.Total), 420, y - 6, 270, 60, 46, ChaosUI.Gold, true, TextAnchor.MiddleRight);
            y += 70;
            var balance = DepotUi.Panel(left, "Saldo", 30, y, 660, 54, DepotUi.Background);
            DepotUi.Txt(balance, "Je kaartjes", 16, 0, 140, 54, 16, ChaosUI.Muted);
            DepotUi.Txt(balance, DepotCatalog.FormatTickets(reward.BalanceBefore), 150, 0, 120, 54, 22, ChaosUI.Paper, true);
            DepotUi.Txt(balance, "→", 262, 0, 30, 54, 22, ChaosUI.Muted, true, TextAnchor.MiddleCenter);
            DepotUi.Txt(balance, DepotCatalog.FormatTickets(reward.BalanceAfter), 300, 0, 140, 54, 26, ChaosUI.Gold, true);
            DepotUi.Txt(balance, "Iedereen in de crew krijgt dit", 420, 0, 228, 54, 13, DepotUi.DisabledText, false, TextAnchor.MiddleRight);

            float by = 730;
            if (isHost)
            {
                DepotUi.Btn(left, "Hoofdmenu", 30, by, 170, 60, ChaosUI.Paper, () => leave?.Invoke(), 20);
                DepotUi.Btn(left, "Opnieuw", 212, by, 150, 60, ChaosUI.Paper, () => again?.Invoke(), 20);
                if (next > 0)
                    DepotUi.Btn(left, $"Naar level {next}", 374, by, 316, 60, ChaosUI.Gold, () => goNext?.Invoke(), 25);
                else
                    DepotUi.Btn(left, "Terug naar levels", 374, by, 316, 60, ChaosUI.Gold, () => leave?.Invoke(), 22);
            }
            else
            {
                DepotUi.Btn(left, "Hoofdmenu", 30, by, 170, 60, ChaosUI.Paper, () => leave?.Invoke(), 20);
                DepotUi.Txt(left, "Wachten tot de host het volgende level kiest…", 220, by, 470, 60, 17, ChaosUI.Muted);
            }

            float rx = 792, rw = 760;
            float ry = 40;
            var goals = DepotUi.Panel(stage, "Volgende doelen", rx, ry, rw, 260, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Txt(goals, "Volgende doelen", 24, 16, 400, 36, 24, ChaosUI.Paper, true);
            float gy = 64;
            int goalsShown = 0;
            foreach (var info in DepotCatalog.Trains)
            {
                if (DepotProgress.Owns(info.Kind) || goalsShown >= 2) continue;
                Goal(goals, ref gy, info.Sprite, info.Name, info.Cost);
                goalsShown++;
            }
            foreach (var info in DepotCatalog.Wagons)
            {
                if (info.HasSet || DepotProgress.Owns(info.Kind) || goalsShown >= 2) continue;
                Goal(goals, ref gy, info.Sprite, info.Name, info.Cost);
                goalsShown++;
            }
            if (goalsShown == 0) DepotUi.Txt(goals, "Alles staat in je depot. Ga voor de beste tijd per level.", 24, 64, 700, 40, 16, ChaosUI.Muted);
        }

        private static void Line(Transform parent, ref float y, string icon, string title, string note, int amount)
        {
            DepotUi.Icon(parent, icon, 30, y + 10, 34);
            DepotUi.Txt(parent, title, 76, y, 380, 54, 18, ChaosUI.Paper);
            if (note != null) DepotUi.Txt(parent, note, 380, y, 200, 54, 13, ChaosUI.Muted, false, TextAnchor.MiddleRight);
            DepotUi.Txt(parent, "+" + DepotCatalog.FormatTickets(amount), 560, y, 130, 54, 24, ChaosUI.Gold, true, TextAnchor.MiddleRight);
            DepotUi.Panel(parent, "Lijn", 30, y + 54, 660, 2, DepotUi.Tab);
            y += 60;
        }

        private static void Goal(Transform parent, ref float y, string sprite, string name, int cost)
        {
            DepotUi.Vehicle(parent, sprite, 24, y, 150, 70, true);
            DepotUi.Txt(parent, name, 196, y, 300, 26, 17, ChaosUI.Paper, true);
            int tickets = DepotProgress.Tickets;
            bool afford = tickets >= cost;
            DepotUi.Txt(parent, afford ? "Nu te koop in het depot" : $"{DepotCatalog.FormatTickets(tickets)} / {DepotCatalog.FormatTickets(cost)}", 440, y, 296, 26, 15,
                afford ? ChaosUI.Gold : ChaosUI.Paper, true, TextAnchor.MiddleRight);
            DepotUi.Panel(parent, "Balk", 196, y + 32, 540, 16, ChaosUI.Slate);
            float fill = Mathf.Clamp01(cost <= 0 ? 1f : tickets / (float)cost);
            if (fill > 0f) DepotUi.Panel(parent, "Voortgang", 196, y + 32, Mathf.Max(16f, 540f * fill), 16, ChaosUI.Gold);
            y += 90;
        }
    }
}
