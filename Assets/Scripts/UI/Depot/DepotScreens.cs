// Conductor Chaos — levelkeuze en depot vóór een dienst (issue #239), volgens het ontwerp-canvas
// "Depot: trein & wagons kiezen". Schermen worden opnieuw opgebouwd uit de toestand (keuze, voortgang, netwerk),
// zodat wat je ziet altijd overeenkomt met wat de server straks neerzet. Bediening met muis/controller in het menu;
// in de wereld blijft alles fysiek.
using System.Collections.Generic;
using ConductorChaos.Depot;
using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI.Depot
{
    public sealed class DepotScreens : MonoBehaviour
    {
        public enum Mode { Hidden, Levels, Train, Wagons, Depart }

        public Mode Current { get; private set; } = Mode.Hidden;
        public System.Action OnBack;
        public System.Action<string> OnStart;

        private PlaygroundHost host;
        private Canvas canvas;
        private RectTransform stage;
        private int level = 1, selectedLevel = 1;
        private TrainKind viewedTrain;
        private string wagonFilter;
        private bool afterRun;
        private System.Action modal;
        private string toast;
        private bool toastWarning;
        private float toastUntil, nextPoll;
        private string pollSignature;
        private bool dirty;

        private static readonly string[] Filters =
            { null, DepotCatalog.CategoryPassengers, DepotCatalog.CategoryGoods, DepotCatalog.CategoryAnimals, DepotCatalog.CategoryLiquid };

        public static DepotScreens Create(Transform parent, PlaygroundHost host)
        {
            var go = new GameObject("Depot en levelkeuze");
            go.transform.SetParent(parent, false);
            var screens = go.AddComponent<DepotScreens>();
            screens.host = host;
            screens.Build();
            return screens;
        }

        private void Build()
        {
            canvas = ChaosUI.MakeCanvas(transform, "Depot", true);
            canvas.sortingOrder = 45;
            stage = ChaosUI.Rect(canvas.transform, "Depotvlak", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            stage.gameObject.AddComponent<StageFitter>();
            canvas.gameObject.SetActive(false);
            DepotProgress.Changed += MarkDirty;
            DepotSync.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            DepotProgress.Changed -= MarkDirty;
            DepotSync.Changed -= MarkDirty;
        }

        private void MarkDirty() => dirty = true;

        private static bool IsCrew
        {
            get
            {
                var network = NetworkManager.Singleton;
                return network && network.IsListening && !network.IsServer;
            }
        }

        // ------------------------------------------------------------------ openen en sluiten

        public void ShowLevels()
        {
            afterRun = false;
            DepotSync.Close();
            SetMode(Mode.Levels);
        }

        /// <summary>Host: open het depot voor een level. afterRun = na een gewonnen dienst (scène laadt direct door).</summary>
        public void ShowDepot(int levelIndex, bool fromRun)
        {
            level = Mathf.Clamp(levelIndex, 1, DepotCatalog.Levels.Length - 1);
            afterRun = fromRun;
            viewedTrain = DepotLoadout.Selection.Train;
            DepotSync.Open(level);
            SetMode(Mode.Train);
        }

        /// <summary>Crew: kijk mee met het depot van de host.</summary>
        public void ShowCrewDepot()
        {
            if (Current is Mode.Train or Mode.Wagons or Mode.Depart) return;
            SetMode(StepMode(DepotSync.Step));
        }

        public void Hide()
        {
            if (Current == Mode.Hidden) return;
            modal = null;
            SetMode(Mode.Hidden);
        }

        private void SetMode(Mode mode)
        {
            Current = mode;
            canvas.gameObject.SetActive(mode != Mode.Hidden);
            if (mode is Mode.Train or Mode.Wagons or Mode.Depart && !IsCrew) DepotSync.SetStep(ModeStep(mode));
            Render();
        }

        private static int ModeStep(Mode mode) => mode == Mode.Wagons ? 1 : mode == Mode.Depart ? 2 : 0;
        private static Mode StepMode(int step) => step == 1 ? Mode.Wagons : step == 2 ? Mode.Depart : Mode.Train;

        /// <summary>Esc/terug vanuit het menu. Geeft false als het depot niets meer te sluiten heeft.</summary>
        public bool Back()
        {
            if (modal != null) { modal = null; Render(); return true; }
            switch (Current)
            {
                case Mode.Depart: SetMode(Mode.Wagons); return true;
                case Mode.Wagons: SetMode(Mode.Train); return true;
                case Mode.Train:
                    if (IsCrew) return false;
                    if (afterRun) { DepotSync.Close(); Hide(); return false; }
                    ShowLevels();
                    return true;
                default: return false;
            }
        }

        private void Update()
        {
            if (Current == Mode.Hidden) return;
            if (IsCrew && DepotSync.IsOpen && StepMode(DepotSync.Step) != Current) { Current = StepMode(DepotSync.Step); dirty = true; }
            if (toast != null && Time.unscaledTime > toastUntil) { toast = null; dirty = true; }
            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + .5f;
                string signature = $"{(host ? host.PlayerCount : 0)}|{(host ? host.Phase.ToString() : "")}|{DepotSync.ReadyClients.Count}";
                if (signature != pollSignature) { pollSignature = signature; dirty = true; }
            }
            if (dirty) Render();
        }

        private void Flash(string message, bool warning = false)
        {
            toast = message;
            toastWarning = warning;
            toastUntil = Time.unscaledTime + 2.4f;
            dirty = true;
        }

        // ------------------------------------------------------------------ opbouw

        private void Render()
        {
            dirty = false;
            DepotUi.Clear(stage);
            if (Current == Mode.Hidden) return;
            var backdrop = DepotUi.Panel(stage, "Achtergrond", -400, -200, 2400, 1300, Current == Mode.Levels
                ? new Color(DepotUi.Background.r, DepotUi.Background.g, DepotUi.Background.b, .80f) : DepotUi.Background);
            backdrop.GetComponent<Image>().raycastTarget = true;
            switch (Current)
            {
                case Mode.Levels: RenderLevels(); break;
                case Mode.Train: RenderTrain(); break;
                case Mode.Wagons: RenderWagons(); break;
                case Mode.Depart: RenderDepart(); break;
            }
            if (modal != null) modal();
            if (toast != null) RenderToast();
        }

        private void TopBar(string title, string backLabel, UnityEngine.Events.UnityAction back, bool steps)
        {
            var bar = DepotUi.Panel(stage, "Bovenbalk", -10, -10, 1620, 86, DepotUi.PanelFace, DepotUi.Edge);
            if (back != null) DepotUi.Btn(bar, backLabel, 34, 25, 126, 46, ChaosUI.Paper, back, 18);
            DepotUi.Txt(bar, title, back != null ? 178 : 34, 10, 220, 76, 30, ChaosUI.Paper, true);
            if (steps)
            {
                string[] names = { "Trein", "Wagons", "Vertrek" };
                int active = ModeStep(Current);
                float x = 330;
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    bool on = i == active, done = i < active;
                    float w = DepotUi.Measure(names[i], 20, true) + 74;
                    var tab = DepotUi.Panel(bar, "Stap " + names[i], x, 36, w, 50, on ? ChaosUI.Paper : DepotUi.Tab);
                    var circle = DepotUi.Panel(tab, "Nummer", 16, 13, 24, 24, done ? ChaosUI.Green : on ? ChaosUI.Ink : ChaosUI.Hex("#5A4F54"));
                    if (done) DepotUi.Icon(circle, "ready", 1, 1, 22);
                    else DepotUi.Txt(circle, (i + 1).ToString(), 0, 0, 24, 24, 14, on ? ChaosUI.Paper : ChaosUI.Paper, true, TextAnchor.MiddleCenter);
                    DepotUi.Txt(tab, names[i], 48, 0, w - 50, 50, 20, on ? ChaosUI.Ink : ChaosUI.Muted, true);
                    if (on) DepotUi.Panel(tab, "Actieve rand", 0, 46, w, 4, ChaosUI.TrainRed);
                    if (!on && !IsCrew) DepotUi.Hit(tab, () => SetMode(index == 0 ? Mode.Train : index == 1 ? Mode.Wagons : Mode.Depart));
                    x += w + 6;
                }
            }
            float right = 1586;
            right = TicketsChip(bar, right, 25);
            if (steps)
            {
                var info = DepotCatalog.Levels[level];
                string from = ServiceStationName(info.Index, false), to = ServiceStationName(info.Index, true);
                float w = Mathf.Max(DepotUi.Measure($"{info.Label} · {info.Name}", 13, false), DepotUi.Measure($"{from} → {to}", 15, false)) + 66;
                var route = DepotUi.Panel(bar, "Route", right - 12 - w, 25, w, 46, DepotUi.Background, DepotUi.Edge);
                DepotUi.Icon(route, "route", 8, 7, 32);
                DepotUi.Txt(route, $"{info.Label} · {info.Name}", 48, 4, w - 52, 20, 13, ChaosUI.Muted);
                DepotUi.Txt(route, $"{from} → {to}", 48, 21, w - 52, 22, 15, ChaosUI.Paper);
            }
            else
            {
                string crew = host && host.Phase == PlaygroundHost.SessionPhase.Lobby ? $"Online · {host.PlayerCount} spelers" : "Solo";
                float w = DepotUi.Measure(crew, 15, false) + 62;
                var chip = DepotUi.Panel(bar, "Crew", right - 12 - w, 25, w, 46, DepotUi.Background, DepotUi.Edge);
                DepotUi.Icon(chip, "player-group", 8, 6, 34);
                DepotUi.Txt(chip, crew, 48, 0, w - 50, 46, 15, ChaosUI.Paper);
            }
        }

        private float TicketsChip(Transform bar, float right, float y)
        {
            string amount = DepotCatalog.FormatTickets(DepotProgress.Tickets);
            float w = DepotUi.Measure(amount, 26, true) + 66;
            var chip = DepotUi.Panel(bar, "Kaartjes", right - w, y, w, 46, DepotUi.Background, DepotUi.Edge);
            DepotUi.Icon(chip, "ticket", 6, 4, 38);
            DepotUi.Txt(chip, amount, 50, 0, w - 52, 46, 26, ChaosUI.Gold, true);
            return right - w;
        }

        private RectTransform BottomBar()
        {
            return DepotUi.Panel(stage, "Onderbalk", -10, 816, 1620, 100, DepotUi.PanelFace, DepotUi.Edge);
        }

        private static string ServiceStationName(int index, bool arrival)
        {
            string scene = DepotCatalog.Levels[index].Scene;
            return Stations.ServiceRun.PlannedStationName(scene, arrival ? Stations.ServiceStation.StationRole.Aankomst
                : Stations.ServiceStation.StationRole.Vertrek) ?? "";
        }

        // ------------------------------------------------------------------ levelkeuze

        private void RenderLevels()
        {
            TopBar("Kies je dienst", "Terug", () => { DepotSync.Close(); Hide(); OnBack?.Invoke(); }, false);
            var levels = DepotCatalog.Levels;
            int next = 1;
            while (next < levels.Length - 1 && DepotProgress.LevelEverWon(next)) next++;
            for (int i = 0; i < levels.Length; i++)
            {
                var info = levels[i];
                int index = i;
                bool available = Application.CanStreamedLevelBeLoaded(info.Scene);
                bool selected = selectedLevel == i;
                float x = 24 + i * 392;
                var card = DepotUi.Panel(stage, "Level " + info.Label, x, 98, 376, 700, DepotUi.PanelFace, selected ? ChaosUI.Gold : DepotUi.Edge, selected ? 3 : 2);
                var photo = DepotUi.Panel(card, "Foto", 12, 12, 352, 200, DepotUi.Inset);
                photo.gameObject.AddComponent<RectMask2D>();
                var texture = Resources.Load<Texture2D>("Routes/" + info.Photo);
                if (texture)
                {
                    var raw = DepotUi.Box(photo, "Stationsfoto", 0, 0, 352, 200).gameObject.AddComponent<RawImage>();
                    raw.texture = texture;
                    raw.raycastTarget = false;
                    float aspect = (float)texture.width / texture.height, target = 352f / 200f;
                    raw.uvRect = aspect > target ? new Rect((1f - target / aspect) / 2f, 0f, target / aspect, 1f)
                        : new Rect(0f, (1f - aspect / target) / 2f, 1f, aspect / target);
                    if (!available) raw.color = new Color(.45f, .43f, .45f, 1f);
                }
                DepotUi.Chip(card, info.Label, 22, 22, ChaosUI.Paper, ChaosUI.Ink, null, 14, 28);
                if (!available) DepotUi.Chip(card, "Niet in deze build", 354, 22, ChaosUI.Slate, ChaosUI.Paper, "warning", 13, 30, true);
                else if (i > 0 && DepotProgress.LevelEverWon(i)) DepotUi.Chip(card, "Gehaald", 354, 22, ChaosUI.Green, DepotUi.GreenText, "ready", 13, 30, true);
                else if (i == next) DepotUi.Chip(card, "Volgende", 354, 22, ChaosUI.Gold, ChaosUI.Ink, null, 13, 30, true);

                DepotUi.Txt(card, info.Name, 20, 222, 340, 40, 30, ChaosUI.Paper, true);
                string from = ServiceStationName(i, false), to = ServiceStationName(i, true);
                DepotUi.Txt(card, string.IsNullOrEmpty(to) ? $"{from} · oefenrit" : $"{from} → {to}", 20, 262, 340, 24, 17, ChaosUI.Paper);
                DepotUi.Icon(card, "route", 18, 296, 28);
                DepotUi.Txt(card, info.Distance, 52, 296, 300, 28, 15, ChaosUI.Muted);
                DepotUi.Txt(card, "Moeilijkheid", 20, 334, 130, 20, 14, ChaosUI.Muted);
                DepotUi.Segments(card, 140, 338, 120, 12, info.Difficulty, 4);
                var summary = DepotUi.Txt(card, info.Summary, 20, 366, 336, 110, 15, ChaosUI.Muted);
                summary.alignment = TextAnchor.UpperLeft;
                float hy = 486;
                foreach (string highlight in info.Highlights)
                {
                    DepotUi.Chip(card, highlight, 20, hy, ChaosUI.Slate, ChaosUI.Paper, null, 13, 28);
                    hy += 36;
                }
                int best = DepotProgress.BestSecondsLeft(i);
                if (i > 0 && best >= 0)
                {
                    DepotUi.Icon(card, "speed-gauge", 18, 590, 28);
                    DepotUi.Txt(card, $"Beste tijd over: {DepotUi.Time(best)}", 52, 590, 300, 28, 15, ChaosUI.Paper);
                }
                if (i > 0)
                {
                    var unlock = UnlockForLevel(i);
                    if (unlock != null)
                    {
                        DepotUi.Panel(card, "Vrijspelen", 12, 628, 352, 60, DepotUi.Background);
                        DepotUi.Icon(card, "key", 22, 640, 34);
                        var text = DepotUi.Txt(card, unlock, 64, 628, 290, 60, 13, ChaosUI.Muted);
                        text.alignment = TextAnchor.MiddleLeft;
                    }
                }
                if (available) DepotUi.Hit(card, () => { selectedLevel = index; Render(); });
            }

            var bottom = BottomBar();
            DepotUi.Icon(bottom, "train", 34, 26, 40);
            DepotUi.Txt(bottom, selectedLevel == 0
                ? "De introductie rijdt altijd met de stoomloc en zijn regionale wagon."
                : "Na het kiezen stel je in het depot de trein en wagons samen.", 84, 10, 900, 84, 16, ChaosUI.Muted);
            bool canStart = Application.CanStreamedLevelBeLoaded(DepotCatalog.Levels[selectedLevel].Scene);
            if (selectedLevel == 0)
                DepotUi.Btn(bottom, "Start oefenrit", 1314, 22, 272, 58, ChaosUI.Gold, () => StartScene(DepotCatalog.Levels[0].Scene), 24, canStart, "play");
            else
                DepotUi.Btn(bottom, "Naar het depot", 1314, 22, 272, 58, ChaosUI.Gold, () => ShowDepot(selectedLevel, false), 24, canStart);
        }

        private static string UnlockForLevel(int index)
        {
            var parts = new List<string>();
            foreach (var train in DepotCatalog.Trains) if (train.UnlockLevel == index) parts.Add(train.Name);
            foreach (var wagon in DepotCatalog.Wagons) if (!wagon.HasSet && wagon.UnlockLevel == index) parts.Add(wagon.Name.ToLowerInvariant());
            if (parts.Count == 0) return null;
            return (DepotProgress.LevelEverWon(index) ? "Vrijgespeeld: " : "Haal dit level voor: ") + string.Join(", ", parts);
        }

        private void StartScene(string scene)
        {
            DepotProgress.SaveLastLoadout(DepotLoadout.Selection);
            OnStart?.Invoke(scene);
        }

        // ------------------------------------------------------------------ stap 1: trein

        private void RenderTrain()
        {
            bool crew = IsCrew;
            var loadout = crew ? DepotLoadout.Received : DepotLoadout.Selection;
            if (crew) viewedTrain = loadout.Train;
            TopBar("Depot", crew ? null : afterRun ? "Terug" : "Levels", crew ? null : (UnityEngine.Events.UnityAction)(() => Back()), true);
            var info = DepotCatalog.Train(viewedTrain);
            bool owned = crew || DepotProgress.Owns(viewedTrain);
            if (!crew && owned && DepotProgress.IsNew(viewedTrain)) DepotProgress.MarkSeen(viewedTrain);
            float top = crew ? 142 : 94;
            if (crew) CrewBanner();

            // Podium met de render van de trein
            float stageHeight = crew ? 404 : 452;
            var podium = DepotUi.Panel(stage, "Podium", 24, top, 1062, stageHeight, DepotUi.Inset, DepotUi.Edge);
            var heroArea = DepotUi.Box(podium, "Foto", 2, 2, 1058, stageHeight - 4);
            heroArea.gameObject.AddComponent<RectMask2D>();
            Cover(heroArea, Resources.Load<Sprite>("Depot/hero_" + info.Sprite), 1058, stageHeight - 4, !owned);
            DepotUi.Chip(podium, crew ? "Trein van de host" : "Kies je trein", 20, 18, ChaosUI.Paper, ChaosUI.Ink, null, 26, 46);
            float plateW = Mathf.Max(DepotUi.Measure(info.Name, 34, true), DepotUi.Measure(info.Era, 15, false)) + 96;
            var plate = DepotUi.Panel(podium, "Naambord", 20, stageHeight - 96, plateW, 76, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Icon(plate, "train", 12, 14, 48);
            DepotUi.Txt(plate, info.Name, 72, 4, plateW - 76, 44, 34, ChaosUI.Paper, true);
            DepotUi.Txt(plate, info.Era, 72, 44, plateW - 76, 24, 15, ChaosUI.Muted);
            if (!owned) DepotUi.Chip(podium, "Nog op slot", 1040, 18, DepotUi.PanelFace, ChaosUI.Paper, "key", 18, 46, true);
            else if (!crew && info.UnlockLevel > 0 && DepotProgress.LevelEverWon(info.UnlockLevel) && info.UnlockLevel == HighestWon())
                DepotUi.Chip(podium, "Nieuw in je depot", 1040, 18, ChaosUI.Gold, ChaosUI.Ink, null, 18, 40, true);

            // Gegevens
            var panel = DepotUi.Panel(stage, "Gegevens", 1104, top, 472, stageHeight, DepotUi.PanelFace, DepotUi.Edge);
            var description = DepotUi.Txt(panel, info.Description, 22, 16, 428, 52, 16, ChaosUI.Muted);
            description.alignment = TextAnchor.UpperLeft;
            var stats = DepotUi.Panel(panel, "Waarden", 22, 76, 428, 122, DepotUi.Background);
            DepotUi.Txt(stats, "Topsnelheid", 16, 12, 130, 26, 16, ChaosUI.Paper);
            DepotUi.Segments(stats, 150, 18, 260, 14, info.Speed);
            DepotUi.Txt(stats, "Optrekken", 16, 48, 130, 26, 16, ChaosUI.Paper);
            DepotUi.Segments(stats, 150, 54, 260, 14, info.Acceleration);
            DepotUi.Txt(stats, "Max. wagons", 16, 84, 130, 26, 16, ChaosUI.Paper);
            for (int i = 0; i < info.MaxWagons; i++)
            {
                DepotUi.Panel(stats, "Wagonplek", 150 + i * 36, 90, 30, 16, DepotUi.GoldDark);
                DepotUi.Panel(stats, "Wagonplek", 150 + i * 36, 90, 30, 13, ChaosUI.Gold);
            }
            DepotUi.Txt(stats, info.MaxWagons.ToString(), 370, 80, 40, 34, 22, ChaosUI.Gold, true, TextAnchor.MiddleRight);
            DepotUi.Icon(panel, info.SpecialIcon, 22, 212, 40);
            DepotUi.Txt(panel, info.SpecialTitle, 72, 206, 380, 24, 15, ChaosUI.Paper, true);
            DepotUi.Txt(panel, info.SpecialText, 72, 228, 380, 26, 14, ChaosUI.Muted);
            DepotUi.Icon(panel, "wrench", 22, 270, 26);
            DepotUi.Txt(panel, "Kan onderweg kapot", 54, 268, 300, 30, 15, ChaosUI.Paper, true);
            float cx = 22, cy = 304;
            foreach (string part in info.Breakdowns)
            {
                float w = DepotUi.Measure(part, 14, false) + 22;
                if (cx + w > 450) { cx = 22; cy += 36; }
                DepotUi.Chip(panel, part, cx, cy, ChaosUI.Slate, ChaosUI.Paper, null, 14, 30);
                cx += w + 6;
            }
            if (!owned)
            {
                var box = DepotUi.Panel(panel, "Vrijspelen", 22, stageHeight - 96, 428, 74, DepotUi.Background);
                DepotUi.Txt(box, $"Gratis na level {info.UnlockLevel}", 14, 8, 220, 28, 15, ChaosUI.Paper, true);
                DepotUi.Txt(box, "of nu kopen met kaartjes", 14, 34, 220, 24, 14, ChaosUI.Muted);
                if (DepotProgress.CanAfford(info.Cost))
                    DepotUi.Btn(box, DepotCatalog.FormatTickets(info.Cost), 238, 12, 176, 50, ChaosUI.Gold, () => ConfirmTrain(info.Kind), 20, true, "ticket");
                else
                    DepotUi.Btn(box, $"nog {DepotCatalog.FormatTickets(info.Cost - DepotProgress.Tickets)} tekort", 238, 12, 176, 50, ChaosUI.Gold, null, 15, false);
            }

            // Kaarten
            float cardTop = top + stageHeight + 16;
            float cardHeight = 816 - 16 - cardTop;
            for (int i = 0; i < DepotCatalog.Trains.Length; i++)
            {
                var train = DepotCatalog.Trains[i];
                bool mine = crew || DepotProgress.Owns(train.Kind);
                bool viewing = train.Kind == viewedTrain;
                bool chosen = train.Kind == loadout.Train;
                float x = 24 + i * 392;
                var card = DepotUi.Panel(stage, "Trein " + train.Name, x, cardTop, 376, cardHeight, DepotUi.PanelFace,
                    viewing ? ChaosUI.Gold : DepotUi.Edge, viewing ? 3 : 2);
                float imageHeight = cardHeight - 100;
                DepotUi.Panel(card, "Treinvak", 3, 3, 370, imageHeight, DepotUi.Inset);
                DepotUi.Vehicle(card, train.Sprite, 20, 18, 336, imageHeight - 28, !mine);
                DepotUi.Track(card, 3, imageHeight - 10, 370);
                DepotUi.Txt(card, train.Name, 14, imageHeight + 8, 240, 32, 23, ChaosUI.Paper, true);
                DepotUi.Txt(card, train.Short, 14, imageHeight + 40, 240, 20, 13, ChaosUI.Muted);
                if (crew)
                {
                    bool suggested = DepotSync.Suggestions.TryGetValue(NetworkManager.Singleton.LocalClientId, out var mineSuggestion) &&
                                     mineSuggestion.Type == DepotSync.SuggestTrain && mineSuggestion.Value == (byte)train.Kind;
                    var kind = train.Kind;
                    DepotUi.Btn(card, suggested ? "Voorgesteld" : "Stel voor", 220, imageHeight + 14, 142, 40,
                        suggested ? ChaosUI.Green : ChaosUI.Paper,
                        () => DepotSync.Suggest(suggested ? DepotSync.SuggestNone : DepotSync.SuggestTrain, (byte)kind), 16);
                }
                else if (mine)
                {
                    bool fresh = DepotProgress.IsNew(train.Kind);
                    DepotUi.Chip(card, fresh ? "Nieuw" : "In depot", 362, imageHeight + 12, fresh ? ChaosUI.Gold : ChaosUI.Green,
                        fresh ? ChaosUI.Ink : DepotUi.GreenText, "ready", 13, 30, true);
                }
                else if (DepotProgress.CanAfford(train.Cost))
                    DepotUi.Chip(card, DepotCatalog.FormatTickets(train.Cost), 362, imageHeight + 12, ChaosUI.Paper, ChaosUI.Ink, "ticket", 13, 30, true);
                else
                    DepotUi.Chip(card, "Level " + train.UnlockLevel, 362, imageHeight + 12, ChaosUI.Slate, ChaosUI.Paper, "key", 13, 30, true);
                if (chosen) DepotUi.Chip(card, "Gekozen", 10, 10, ChaosUI.Gold, ChaosUI.Ink, null, 14, 26);
                if (!crew && DepotSync.TryGetSuggestion(DepotSync.SuggestTrain, (byte)train.Kind, out ulong by))
                    DepotUi.Chip(card, $"{DepotSync.PlayerName(by)} stelt voor", 366, 10, ChaosUI.Paper, ChaosUI.Ink, "player", 13, 28, true);
                if (!crew)
                {
                    var kind = train.Kind;
                    DepotUi.Hit(card, () => ViewTrain(kind));
                }
            }

            var bottom = BottomBar();
            if (crew)
            {
                CrewReady(bottom);
                return;
            }
            DepotUi.Icon(bottom, "player-group", 34, 24, 40);
            DepotUi.Txt(bottom, host && host.Phase != PlaygroundHost.SessionPhase.Offline && host.PlayerCount > 1
                ? "Jij bent host: jouw keuze geldt voor de hele crew" : "Solo-dienst: je kiest alleen voor jezelf", 84, 10, 760, 84, 16, ChaosUI.Muted);
            if (owned)
                DepotUi.Btn(bottom, "Wagons koppelen", 1286, 22, 300, 58, ChaosUI.Gold, () => SetMode(Mode.Wagons), 24);
            else
                DepotUi.Btn(bottom, "Eerst ontgrendelen", 1286, 22, 300, 58, ChaosUI.Gold, null, 22, false);
        }

        private static int HighestWon()
        {
            int best = 0;
            for (int i = 1; i < DepotCatalog.Levels.Length; i++) if (DepotProgress.LevelEverWon(i)) best = i;
            return best;
        }

        private void ViewTrain(TrainKind kind)
        {
            viewedTrain = kind;
            if (DepotProgress.Owns(kind) && DepotLoadout.Selection.Train != kind)
            {
                var loadout = DepotLoadout.Selection.Clone();
                loadout.Train = kind;
                var set = DepotCatalog.Train(kind).SetCoach;
                if (DepotCatalog.Wagon(set).HasSet && !loadout.Wagons.Contains(set)) loadout.Wagons.Insert(0, set);
                DepotLoadout.SetSelection(loadout);
            }
            Render();
        }

        private static void Cover(Transform parent, Sprite sprite, float w, float h, bool locked)
        {
            if (!sprite) return;
            float aspect = sprite.rect.width / sprite.rect.height;
            float iw = w, ih = w / aspect;
            if (ih < h) { ih = h; iw = h * aspect; }
            var image = DepotUi.Pic(parent, sprite, (w - iw) / 2f, (h - ih) / 2f, iw, ih, false);
            if (locked) image.color = new Color(.55f, .53f, .56f, 1f);
        }

        private void CrewBanner()
        {
            var banner = DepotUi.Panel(stage, "Crewbanner", 24, 88, 1552, 46, ChaosUI.Navy, ChaosUI.Hex("#4B4A7E"));
            DepotUi.Icon(banner, "player", 8, 5, 34);
            DepotUi.Txt(banner, "De host stelt de trein samen. Je ziet het live; wil je iets anders, doe dan een voorstel.", 52, 0, 1100, 44, 16, ChaosUI.Paper);
        }

        private void CrewReady(Transform bottom)
        {
            DepotUi.Icon(bottom, "player-group", 34, 24, 40);
            DepotUi.Txt(bottom, "Kaartjes en unlocks zijn per speler; de trein komt uit het depot van de host.", 84, 10, 900, 84, 16, ChaosUI.Muted);
            bool ready = DepotSync.LocalReady;
            DepotUi.Btn(bottom, ready ? "Klaar · wacht op host" : "Ik ben klaar", 1246, 22, 340, 58, ready ? ChaosUI.Green : ChaosUI.Gold,
                () => DepotSync.SetReady(!DepotSync.LocalReady), 22, true, ready ? "ready" : null);
        }

        // ------------------------------------------------------------------ stap 2: wagons

        private void RenderWagons()
        {
            bool crew = IsCrew;
            var loadout = crew ? DepotLoadout.Received : DepotLoadout.Selection;
            var train = DepotCatalog.Train(loadout.Train);
            TopBar("Depot", crew ? null : "Trein", crew ? null : (UnityEngine.Events.UnityAction)(() => SetMode(Mode.Train)), true);
            float top = 92;
            if (crew) { CrewBanner(); top = 142; }

            // Treinstel
            float stripHeight = crew ? 218 : 236;
            var strip = DepotUi.Panel(stage, "Treinstel", 24, top, 1552, stripHeight, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Chip(strip, "Je treinstel", 16, 12, ChaosUI.Paper, ChaosUI.Ink, null, 22, 36);
            DepotUi.Txt(strip, train.Name, 196, 12, 220, 36, 16, ChaosUI.Paper, true);
            DepotUi.Txt(strip, "Wagon 1 hangt direct achter de loc · rijrichting →", 380, 12, 520, 36, 14, ChaosUI.Muted);
            DepotUi.Txt(strip, $"{loadout.Wagons.Count} / {train.MaxWagons} gekoppeld", 1050, 12, 200, 36, 20, ChaosUI.Paper, true, TextAnchor.MiddleRight);
            for (int i = 0; i < train.MaxWagons; i++)
                DepotUi.Panel(strip, "Bezetting", 1258 + i * 24, 24, 20, 12, i < loadout.Wagons.Count ? ChaosUI.Gold : ChaosUI.Slate);
            if (!crew) DepotUi.Btn(strip, "Alles los", 1420, 8, 116, 40, ChaosUI.Paper, ClearOptional, 16);

            float slotsTop = 58, slotHeight = stripHeight - slotsTop - 14;
            float locoX = 1552 - 16 - 262;
            var loco = DepotUi.Panel(strip, "Loc", locoX, slotsTop, 262, slotHeight, DepotUi.Inset, DepotUi.Edge);
            DepotUi.Chip(loco, "LOC", 8, 8, ChaosUI.TrainRed, DepotUi.RedText, null, 13, 24);
            DepotUi.Vehicle(loco, train.Sprite, 10, 26, 242, slotHeight - 64);
            DepotUi.Track(loco, 2, slotHeight - 38, 258);
            DepotUi.Txt(loco, train.Name, 10, slotHeight - 28, 240, 24, 14, ChaosUI.Paper, true);

            int slots = Mathf.Min(6, train.MaxWagons + 1);
            float slotWidth = (locoX - 16 - 12 * slots) / slots;
            for (int i = 0; i < slots; i++)
            {
                int index = i;
                float x = locoX - 12 - (i + 1) * slotWidth - i * 12;
                if (i < loadout.Wagons.Count)
                {
                    var wagon = DepotCatalog.Wagon(loadout.Wagons[i]);
                    var slot = DepotUi.Panel(strip, "Plek " + (i + 1), x, slotsTop, slotWidth, slotHeight, DepotUi.Inset, DepotUi.Edge);
                    Badge(slot, (i + 1).ToString(), 8, 8, ChaosUI.Paper, ChaosUI.Ink);
                    float nameRight = slotWidth - 8;
                    if (wagon.Breakdowns.Length > 0)
                    {
                        DepotUi.Icon(slot, "wrench", slotWidth - 50, 8, 22);
                        DepotUi.Txt(slot, wagon.Breakdowns.Length.ToString(), slotWidth - 28, 8, 22, 22, 13, ChaosUI.Muted);
                        nameRight = slotWidth - 54;
                    }
                    FitName(slot, wagon.Name, 40, 8, nameRight - 40, 26);
                    DepotUi.Vehicle(slot, wagon.Sprite, 10, 38, slotWidth - 20, slotHeight - 94);
                    DepotUi.Track(slot, 2, slotHeight - 52, slotWidth - 4);
                    if (!crew)
                    {
                        float by = slotHeight - 42;
                        float bw = (slotWidth - 20 - 12) / 3f;
                        DepotUi.Btn(slot, "‹", 10, by, bw, 32, ChaosUI.Paper, () => MoveWagon(index, 1), 20, index < loadout.Wagons.Count - 1);
                        DepotUi.Btn(slot, "›", 16 + bw, by, bw, 32, ChaosUI.Paper, () => MoveWagon(index, -1), 20, index > 0);
                        DepotUi.Btn(slot, "×", 22 + bw * 2, by, bw, 32, ChaosUI.TrainRed, () => RemoveWagon(index), 20);
                    }
                }
                else if (i < train.MaxWagons)
                {
                    var slot = DepotUi.Panel(strip, "Lege plek " + (i + 1), x, slotsTop, slotWidth, slotHeight, DepotUi.PanelFace, DepotUi.Dim);
                    Badge(slot, (i + 1).ToString(), 8, 8, DepotUi.Dim, ChaosUI.Paper);
                    DepotUi.Txt(slot, "+", 0, slotHeight / 2 - 50, slotWidth, 50, 40, ChaosUI.Muted, true, TextAnchor.MiddleCenter);
                    DepotUi.Txt(slot, "Lege plek", 0, slotHeight / 2, slotWidth, 26, 18, ChaosUI.Muted, true, TextAnchor.MiddleCenter);
                    DepotUi.Txt(slot, crew ? "De host kiest" : "Kies een wagon hieronder", 0, slotHeight / 2 + 26, slotWidth, 22, 13, ChaosUI.Muted, false, TextAnchor.MiddleCenter);
                }
                else
                {
                    var slot = DepotUi.Panel(strip, "Plek op slot", x, slotsTop, slotWidth, slotHeight, DepotUi.Locked, DepotUi.Tab);
                    DepotUi.Icon(slot, "key", slotWidth / 2 - 20, slotHeight / 2 - 52, 40, new Color(1, 1, 1, .8f));
                    DepotUi.Txt(slot, $"Plek {i + 1}", 0, slotHeight / 2 - 8, slotWidth, 26, 17, ChaosUI.Muted, true, TextAnchor.MiddleCenter);
                    var note = DepotUi.Txt(slot, $"Deze loc trekt er max. {train.MaxWagons}. Een snellere trein trekt meer.", 12, slotHeight / 2 + 20, slotWidth - 24, 44, 13, DepotUi.DisabledText, false, TextAnchor.UpperCenter);
                    note.horizontalOverflow = HorizontalWrapMode.Wrap;
                }
            }

            // Wagonkeuze
            float catalogTop = top + stripHeight + 16;
            float tx = 24;
            for (int i = 0; i < Filters.Length; i++)
            {
                string filter = Filters[i];
                string label = filter ?? "Alle";
                bool on = filter == wagonFilter;
                float w = DepotUi.Measure(label, 18, true) + 32;
                var tab = DepotUi.Panel(stage, "Tab " + label, tx, catalogTop, w, 42, on ? ChaosUI.Paper : DepotUi.Tab);
                DepotUi.Txt(tab, label, 0, 0, w, 42, 18, on ? ChaosUI.Ink : ChaosUI.Muted, true, TextAnchor.MiddleCenter);
                DepotUi.Hit(tab, () => { wagonFilter = filter; Render(); });
                tx += w + 6;
            }
            DepotUi.Txt(stage, crew ? "Stel een wagon voor aan de host" : "Klik een wagon om hem achteraan te koppelen", tx + 10, catalogTop, 1050 - tx - 10, 42, 14, ChaosUI.Muted, false, TextAnchor.MiddleRight);
            DepotUi.Panel(stage, "Tablijn", 24, catalogTop + 42, 1036, 4, ChaosUI.TrainRed);

            var shown = new List<WagonInfo>();
            foreach (var wagon in DepotCatalog.Wagons) if (wagonFilter == null || wagon.Category == wagonFilter) shown.Add(wagon);
            int columns = Mathf.Max(4, (shown.Count + 1) / 2);
            float stride = 1048f / columns;
            float gridTop = catalogTop + 56;
            float cardHeight = Mathf.Min(196, (816 - 12 - gridTop - 12) / 2f);
            for (int i = 0; i < shown.Count; i++)
            {
                var wagon = shown[i];
                float x = 24 + (i % columns) * stride, y = gridTop + (i / columns) * (cardHeight + 12);
                WagonCard(wagon, loadout, x, y, stride - 12, cardHeight, crew);
            }

            // Samenvatting
            var summary = DepotUi.Panel(stage, "Deze dienst", 1076, catalogTop, 500, 816 - 12 - catalogTop, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Txt(summary, "Laadruimte", 20, 12, 300, 32, 23, ChaosUI.Paper, true);
            int seats = 0, crates = 0, animals = 0, tanks = 0, weight = 0, breakdowns = train.Breakdowns.Length;
            foreach (var kind in loadout.Wagons)
            {
                var w = DepotCatalog.Wagon(kind);
                seats += w.Seats; crates += w.Crates; animals += w.Animals; tanks += w.Tanks; weight += w.Weight; breakdowns += w.Breakdowns.Length;
            }
            int coachIndex = DepotCatalog.PassengerCoachIndex(loadout.Wagons);
            bool coach = coachIndex >= 0;
            InfoRow(summary, 50, "train", "Alles mag overal mee",
                "Oma, koffers en kratten passen in of op elke wagon.", null);
            InfoRow(summary, 112, "ticket", "Reizigers met kaartjes",
                coach ? $"Zitten in wagon {coachIndex + 1}." : "Kies een regionaal rijtuig of een dieselrijtuig met zijdeuren.", coach ? "Mee" : "Niet mee");
            string[] labels = { "Zitplaatsen", "Kratvakken", "Dieren", "Tanks" };
            int[] values = { seats, crates, animals, tanks };
            for (int i = 0; i < 4; i++)
            {
                var box = DepotUi.Panel(summary, labels[i], 20 + i * 117, 176, 109, 66, DepotUi.Background);
                DepotUi.Txt(box, values[i].ToString(), 0, 4, 109, 34, 26, ChaosUI.Paper, true, TextAnchor.MiddleCenter);
                DepotUi.Txt(box, labels[i], 0, 38, 109, 22, 12, ChaosUI.Muted, false, TextAnchor.MiddleCenter);
            }
            int effective = Mathf.Max(1, train.Acceleration - weight / 3);
            DepotUi.Txt(summary, "Optrekken", 20, 254, 120, 26, 15, ChaosUI.Paper, true);
            DepotUi.Segments(summary, 140, 261, 220, 13, effective, 5, train.Acceleration - effective);
            DepotUi.Txt(summary, effective < train.Acceleration ? $"{train.Acceleration} → {effective} (zwaar)" : "normaal", 364, 254, 116, 26, 13, ChaosUI.Muted, false, TextAnchor.MiddleRight);
            DepotUi.Icon(summary, "wrench", 20, 292, 28);
            var note2 = DepotUi.Txt(summary, $"{breakdowns} onderhoudspunten kunnen onderweg kapot. Meer wagons is meer werk voor de monteur.", 56, 288, 424, 44, 14, ChaosUI.Muted);
            note2.alignment = TextAnchor.UpperLeft;

            var bottom = BottomBar();
            if (crew) { CrewReady(bottom); return; }
            DepotUi.Icon(bottom, "player-group", 34, 24, 40);
            DepotUi.Txt(bottom, "Geen verplichte wagons. Kies wat jullie handig vinden; alles moet alleen fysiek van perron naar perron.", 84, 10, 900, 84, 16, ChaosUI.Muted);
            DepotUi.Btn(bottom, "Terug", 1114, 22, 150, 58, ChaosUI.Paper, () => SetMode(Mode.Train), 22);
            DepotUi.Btn(bottom, "Naar vertrek", 1286, 22, 300, 58, ChaosUI.Gold, () => SetMode(Mode.Depart), 24);
        }

        private void WagonCard(WagonInfo wagon, DepotLoadout loadout, float x, float y, float w, float h, bool crew)
        {
            int count = 0;
            foreach (var kind in loadout.Wagons) if (kind == wagon.Kind) count++;
            bool fits = DepotCatalog.Fits(wagon.Kind, loadout.Train);
            bool owned = crew || DepotProgress.Owns(wagon.Kind);
            bool afford = !owned && !wagon.HasSet && DepotProgress.CanAfford(wagon.Cost);
            var card = DepotUi.Panel(stage, "Wagon " + wagon.Name, x, y, w, h, DepotUi.PanelFace, count > 0 ? ChaosUI.Gold : DepotUi.Edge, count > 0 ? 3 : 2);
            if (!fits) card.GetComponent<Image>().color = DepotUi.Edge;
            float imageHeight = h - 102;
            DepotUi.Panel(card, "Wagonvak", 3, 3, w - 6, imageHeight, DepotUi.Inset);
            DepotUi.Vehicle(card, wagon.Sprite, 12, 14, w - 24, imageHeight - 22, !owned || !fits);
            DepotUi.Track(card, 3, imageHeight - 8, w - 6);
            var title = DepotUi.Txt(card, wagon.Name, 12, imageHeight + 6, w - 24, 26, 18, fits ? ChaosUI.Paper : ChaosUI.Muted, true);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 13;
            title.resizeTextMaxSize = 18;
            DepotUi.Txt(card, wagon.Capacity, 12, imageHeight + 32, w - 24, 20, 12, ChaosUI.Muted);
            float chipY = h - 40;
            if (crew)
            {
                bool suggested = DepotSync.Suggestions.TryGetValue(NetworkManager.Singleton.LocalClientId, out var mine) &&
                                 mine.Type == DepotSync.SuggestWagon && mine.Value == (byte)wagon.Kind;
                var kind = wagon.Kind;
                DepotUi.Btn(card, suggested ? "Voorgesteld" : "Stel voor", 10, chipY - 2, w - 20, 34, suggested ? ChaosUI.Green : ChaosUI.Paper,
                    () => DepotSync.Suggest(suggested ? DepotSync.SuggestNone : DepotSync.SuggestWagon, (byte)kind), 15, fits);
                return;
            }
            if (!fits) DepotUi.Chip(card, "Alleen " + DepotCatalog.Train(wagon.Set).Name, 10, chipY, ChaosUI.Slate, ChaosUI.Paper, "train", 12, 28);
            else if (owned) DepotUi.Chip(card, wagon.HasSet ? "Hoort bij je trein" : "In depot", 10, chipY, ChaosUI.Green, DepotUi.GreenText, "ready", 12, 28);
            else if (afford) DepotUi.Chip(card, $"{DepotCatalog.FormatTickets(wagon.Cost)} · of level {wagon.UnlockLevel}", 10, chipY, ChaosUI.Paper, ChaosUI.Ink, "ticket", 12, 28);
            else DepotUi.Chip(card, $"Level {wagon.UnlockLevel} · {DepotCatalog.FormatTickets(wagon.Cost)}", 10, chipY, ChaosUI.Slate, ChaosUI.Paper, "key", 12, 28);
            if (count > 0) DepotUi.Chip(card, $"{count}× in trein", w - 10, chipY, ChaosUI.Gold, ChaosUI.Ink, null, 12, 28, true);
            if (DepotSync.TryGetSuggestion(DepotSync.SuggestWagon, (byte)wagon.Kind, out ulong by))
                DepotUi.Chip(card, $"{DepotSync.PlayerName(by)} stelt voor", w - 8, 8, ChaosUI.Paper, ChaosUI.Ink, "player", 12, 26, true);
            var target = wagon;
            DepotUi.Hit(card, () => ClickWagon(target));
        }

        private static void Badge(Transform parent, string caption, float x, float y, Color face, Color ink)
        {
            var badge = DepotUi.Panel(parent, "Nummer", x, y, 26, 26, face);
            DepotUi.Txt(badge, caption, 0, 0, 26, 26, 15, ink, true, TextAnchor.MiddleCenter);
        }

        private static void InfoRow(Transform parent, float y, string icon, string title, string sub, string status)
        {
            var row = DepotUi.Panel(parent, title, 20, y, 460, 54, DepotUi.Background);
            DepotUi.Icon(row, icon, 10, 9, 36);
            float textWidth = status == null ? 390 : 300;
            DepotUi.Txt(row, title, 56, 4, textWidth, 24, 15, ChaosUI.Paper, true);
            var line = DepotUi.Txt(row, sub, 56, 26, textWidth, 22, 13, ChaosUI.Muted);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (status == "Mee") DepotUi.Chip(row, status, 448, 12, ChaosUI.Green, DepotUi.GreenText, "ready", 13, 30, true);
            else if (status != null) DepotUi.Chip(row, status, 448, 12, ChaosUI.Slate, ChaosUI.Paper, null, 13, 30, true);
        }

        /// <summary>Naam op één regel; wordt kleiner of ingekort als hij niet past.</summary>
        private static void FitName(Transform parent, string name, float x, float y, float w, float h)
        {
            int size = 14;
            while (size > 11 && DepotUi.Measure(name, size, true) > w) size--;
            string shown = name;
            while (shown.Length > 3 && DepotUi.Measure(shown + "…", size, true) > w) shown = shown.Substring(0, shown.Length - 1);
            if (shown != name) shown = shown.TrimEnd() + "…";
            var text = DepotUi.Txt(parent, shown, x, y, w, h, size, ChaosUI.Paper, true);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void ClickWagon(WagonInfo wagon)
        {
            var loadout = DepotLoadout.Selection;
            if (!DepotCatalog.Fits(wagon.Kind, loadout.Train))
            {
                Flash($"Dit rijtuig hoort bij de {DepotCatalog.Train(wagon.Set).Name}", true);
                return;
            }
            if (!DepotProgress.Owns(wagon.Kind))
            {
                if (DepotProgress.CanAfford(wagon.Cost)) ConfirmWagon(wagon.Kind);
                else Flash($"Nog {DepotCatalog.FormatTickets(wagon.Cost - DepotProgress.Tickets)} kaartjes nodig, of haal level {wagon.UnlockLevel}", true);
                return;
            }
            if (loadout.Wagons.Count >= loadout.MaxWagons)
            {
                Flash($"Trein vol: de {DepotCatalog.Train(loadout.Train).Name} trekt max. {loadout.MaxWagons} wagons", true);
                return;
            }
            if (DepotCatalog.IsUnique(wagon.Kind) && loadout.Wagons.Contains(wagon.Kind))
            {
                Flash($"Er is maar één {wagon.Name.ToLowerInvariant()} en die zit al in je treinstel");
                return;
            }
            var next = loadout.Clone();
            next.Wagons.Add(wagon.Kind);
            DepotLoadout.SetSelection(next);
            Flash($"{wagon.Name} gekoppeld op plek {next.Wagons.Count}");
            Render();
        }

        private void RemoveWagon(int index)
        {
            var next = DepotLoadout.Selection.Clone();
            if (index < 0 || index >= next.Wagons.Count) return;
            string name = DepotCatalog.Wagon(next.Wagons[index]).Name;
            next.Wagons.RemoveAt(index);
            DepotLoadout.SetSelection(next);
            Flash($"{name} losgekoppeld");
            Render();
        }

        private void MoveWagon(int index, int direction)
        {
            var next = DepotLoadout.Selection.Clone();
            int other = index + direction;
            if (other < 0 || other >= next.Wagons.Count) return;
            (next.Wagons[index], next.Wagons[other]) = (next.Wagons[other], next.Wagons[index]);
            DepotLoadout.SetSelection(next);
            Render();
        }

        private void ClearOptional()
        {
            var next = DepotLoadout.Selection.Clone();
            next.Wagons.Clear();
            DepotLoadout.SetSelection(next);
            Flash("Alle wagons losgekoppeld: alleen de loc staat nog klaar");
            Render();
        }

        // ------------------------------------------------------------------ stap 3: vertrek

        private void RenderDepart()
        {
            bool crew = IsCrew;
            var loadout = crew ? DepotLoadout.Received : DepotLoadout.Selection;
            var train = DepotCatalog.Train(loadout.Train);
            var info = DepotCatalog.Levels[level];
            TopBar("Depot", crew ? null : "Wagons", crew ? null : (UnityEngine.Events.UnityAction)(() => SetMode(Mode.Wagons)), true);

            var consist = DepotUi.Panel(stage, "Klaar voor vertrek", 24, 94, 1040, 330, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Chip(consist, "Klaar voor vertrek", 18, 14, ChaosUI.Paper, ChaosUI.Ink, null, 24, 40);
            DepotUi.Txt(consist, $"{train.Name} · {loadout.Wagons.Count} van {train.MaxWagons} wagons", 264, 14, 520, 40, 15, ChaosUI.Muted);
            if (!crew) DepotUi.Btn(consist, "Treinstel aanpassen", 820, 14, 202, 40, ChaosUI.Paper, () => SetMode(Mode.Wagons), 16);
            var shelf = DepotUi.Panel(consist, "Perron", 18, 66, 1004, 196, DepotUi.Inset);
            ConsistPicture(shelf, loadout, 1004, 196);
            int seats = 0, crates = 0, breakdowns = train.Breakdowns.Length, weight = 0;
            foreach (var kind in loadout.Wagons)
            {
                var w = DepotCatalog.Wagon(kind);
                seats += w.Seats; crates += w.Crates; breakdowns += w.Breakdowns.Length; weight += w.Weight;
            }
            float cx = 18;
            cx += DepotUi.Chip(consist, $"{seats} zitplaatsen", cx, 278, DepotUi.Background, ChaosUI.Paper, "ticket", 14, 36) + 10;
            cx += DepotUi.Chip(consist, $"{crates} kratvakken", cx, 278, DepotUi.Background, ChaosUI.Paper, "train", 14, 36) + 10;
            cx += DepotUi.Chip(consist, $"{breakdowns} onderhoudspunten", cx, 278, DepotUi.Background, ChaosUI.Paper, "wrench", 14, 36) + 10;
            DepotUi.Chip(consist, $"Optrekken {Mathf.Max(1, train.Acceleration - weight / 3)} / 5", cx, 278, DepotUi.Background, ChaosUI.Paper, "speed-gauge", 14, 36);

            var route = DepotUi.Panel(stage, "Route", 24, 440, 380, 362, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Chip(route, "Route · " + info.Label, 18, 16, ChaosUI.Paper, ChaosUI.Ink, null, 18, 34);
            DepotUi.Txt(route, info.Name, 20, 58, 340, 40, 30, ChaosUI.Paper, true);
            DepotUi.Txt(route, $"{ServiceStationName(level, false)} → {ServiceStationName(level, true)}", 20, 98, 340, 24, 16, ChaosUI.Paper, true);
            DepotUi.Icon(route, "route", 18, 134, 28);
            DepotUi.Txt(route, info.Distance, 54, 134, 310, 28, 15, ChaosUI.Muted);
            float hy = 168;
            foreach (string highlight in info.Highlights)
            {
                DepotUi.Icon(route, "warning", 18, hy, 28);
                DepotUi.Txt(route, highlight, 54, hy, 310, 28, 15, ChaosUI.Muted);
                hy += 34;
            }
            var startNote = DepotUi.Txt(route, $"Na start: briefing op het perron van {ServiceStationName(level, false)}. De klok loopt vanaf het vertrekperron.", 20, 290, 340, 56, 13, DepotUi.DisabledText);
            startNote.alignment = TextAnchor.UpperLeft;

            var tasks = DepotUi.Panel(stage, "Opdrachten", 420, 440, 644, 362, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Txt(tasks, level == 1 ? "Opdrachten · één per rit" : "Opdrachten · alles tegelijk", 20, 12, 600, 34, 23, ChaosUI.Paper, true);
            int coach = DepotCatalog.PassengerCoachIndex(loadout.Wagons) + 1;
            var rows = new List<(string, string)>
            {
                ("Kaartjes controleren", coach > 0 ? $"Wagon {coach}" : "Niet deze rit"), ("Oma begeleiden", "Elke wagon"),
                ("De rode koffer bezorgen", "Elke wagon"), ("Drie kratten vervoeren", "Elke wagon")
            };
            if (level == 1) rows.Add(("De wissel repareren", "Spoor"));
            else { rows.Add(("Herstel de brugrail", "Spoor")); rows.Add(("Zet de wissels goed", "Spoor")); }
            float ry = 54;
            foreach (var (title, tag) in rows)
            {
                var row = DepotUi.Panel(tasks, title, 20, ry, 604, 42, DepotUi.Background);
                DepotUi.Panel(row, "Vakje", 12, 11, 20, 20, ChaosUI.Muted);
                DepotUi.Panel(row, "Vakje binnen", 14, 13, 16, 16, DepotUi.Background);
                DepotUi.Txt(row, title, 44, 0, 420, 42, 16, ChaosUI.Paper);
                DepotUi.Chip(row, tag, 592, 7, tag == "Spoor" ? DepotUi.Sleeper : ChaosUI.Slate, ChaosUI.Paper, null, 13, 28, true);
                ry += 48;
            }

            var crewPanel = DepotUi.Panel(stage, "Crew", 1080, 94, 496, 708, DepotUi.PanelFace, DepotUi.Edge);
            int players = host ? Mathf.Max(1, host.PlayerCount) : 1;
            int ready = 1 + DepotSync.ReadyClients.Count;
            DepotUi.Txt(crewPanel, "Crew", 20, 12, 200, 36, 26, ChaosUI.Paper, true);
            DepotUi.Icon(crewPanel, "player-group", 300, 12, 32);
            DepotUi.Txt(crewPanel, $"{players} / {PlaygroundHost.Capacity} · {Mathf.Min(ready, players)} klaar", 336, 12, 140, 34, 14, ChaosUI.Muted, false, TextAnchor.MiddleRight);
            var ids = new List<ulong>();
            var network = NetworkManager.Singleton;
            if (network && network.IsListening && network.IsServer) ids.AddRange(network.ConnectedClientsIds);
            float py = 58;
            for (int i = 0; i < players && i < PlaygroundHost.Capacity; i++)
            {
                bool isHost = i == 0;
                bool me = crew ? false : isHost;
                bool isReady = isHost || (i < ids.Count ? DepotSync.ReadyClients.Contains(ids[i]) : (crew && DepotSync.LocalReady && i == 1));
                string name = isHost ? (crew ? "Host" : "Jij") : (i < ids.Count ? DepotSync.PlayerName(ids[i]) : $"Speler {i + 1}");
                var seat = DepotUi.Panel(crewPanel, "Speler", 20, py, 456, 58, DepotUi.Background, DepotUi.Tab);
                var avatar = DepotUi.Panel(seat, "Avatar", 8, 6, 46, 46, ChaosUI.Slate);
                DepotUi.Icon(avatar, "player", 4, 4, 38);
                DepotUi.Txt(seat, name + (isHost ? "  · host" : ""), 66, 4, 260, 28, 17, ChaosUI.Paper, true);
                DepotUi.Txt(seat, me ? "Dat ben jij" : "Verbonden", 66, 30, 260, 22, 13, ChaosUI.Muted);
                if (isReady) DepotUi.Chip(seat, "Klaar", 444, 14, ChaosUI.Green, DepotUi.GreenText, "ready", 14, 30, true);
                else DepotUi.Chip(seat, "Kiest nog", 444, 14, ChaosUI.Slate, ChaosUI.Paper, null, 14, 30, true);
                py += 66;
            }
            if (players < PlaygroundHost.Capacity && host && host.Phase == PlaygroundHost.SessionPhase.Lobby && !string.IsNullOrEmpty(host.JoinCode))
            {
                var empty = DepotUi.Panel(crewPanel, "Lege plek", 20, py, 456, 58, DepotUi.PanelFace, DepotUi.Dim);
                DepotUi.Txt(empty, "Lege plek", 66, 4, 260, 28, 17, ChaosUI.Muted, true);
                DepotUi.Txt(empty, "Lobbycode: " + host.JoinCode, 66, 30, 300, 22, 13, DepotUi.DisabledText);
                py += 66;
            }
            var risks = DepotUi.Panel(crewPanel, "Kapot", 20, Mathf.Max(py + 6, 420), 456, 150, DepotUi.Background);
            DepotUi.Icon(risks, "wrench", 12, 10, 26);
            DepotUi.Txt(risks, "Wat kan er kapot?", 46, 8, 300, 30, 15, ChaosUI.Paper, true);
            var parts = new List<string>();
            foreach (string part in DepotCatalog.BreakdownsOf(loadout.Train, loadout.Wagons)) if (!parts.Contains(part)) parts.Add(part);
            var partText = DepotUi.Txt(risks, string.Join(" · ", parts), 14, 42, 428, 100, 14, ChaosUI.Muted);
            partText.alignment = TextAnchor.UpperLeft;

            if (crew)
            {
                bool mine = DepotSync.LocalReady;
                DepotUi.Btn(crewPanel, mine ? "Klaar · wacht op host" : "Ik ben klaar", 20, 600, 456, 66, mine ? ChaosUI.Green : ChaosUI.Gold,
                    () => DepotSync.SetReady(!DepotSync.LocalReady), 24);
            }
            else
            {
                DepotUi.Btn(crewPanel, "Start dienst", 20, 600, 456, 66, ChaosUI.Gold, () => StartScene(info.Scene), 28, true, "play");
                DepotUi.Txt(crewPanel, "Alleen de host kan starten · iedereen laadt tegelijk", 20, 670, 456, 30, 13, DepotUi.DisabledText, false, TextAnchor.MiddleCenter);
            }

            var bottom = BottomBar();
            if (crew) { CrewReady(bottom); return; }
            DepotUi.Txt(bottom, "Je keuze wordt bewaard voor de volgende dienst.", 34, 10, 800, 84, 16, ChaosUI.Muted);
            DepotUi.Btn(bottom, "Terug naar wagons", 1336, 22, 250, 58, ChaosUI.Paper, () => SetMode(Mode.Wagons), 20);
        }

        /// <summary>Het hele treinstel naast elkaar op één spoor, loc rechts.</summary>
        private static void ConsistPicture(Transform parent, DepotLoadout loadout, float w, float h)
        {
            var sprites = new List<(string sprite, string label)>();
            for (int i = loadout.Wagons.Count - 1; i >= 0; i--)
            {
                var wagon = DepotCatalog.Wagon(loadout.Wagons[i]);
                sprites.Add((wagon.Sprite, $"{i + 1} · {wagon.Name}"));
            }
            var train = DepotCatalog.Train(loadout.Train);
            sprites.Add((train.Sprite, "Loc · " + train.Name));
            float height = 100f, gap = 4f, total = 0f;
            var sizes = new List<float>();
            foreach (var (name, _) in sprites)
            {
                var sprite = Resources.Load<Sprite>("Depot/" + name);
                float width = sprite ? height * sprite.rect.width / sprite.rect.height : height * 2.4f;
                sizes.Add(width);
                total += width + gap;
            }
            float scale = Mathf.Min(1f, (w - 40f) / Mathf.Max(1f, total));
            float x = (w - total * scale) / 2f;
            float trackY = 30f + height * scale + 2f;
            DepotUi.Track(parent, 0, trackY, w);
            for (int i = 0; i < sprites.Count; i++)
            {
                float width = sizes[i] * scale;
                DepotUi.Vehicle(parent, sprites[i].sprite, x, 30f, width, height * scale);
                var label = DepotUi.Txt(parent, sprites[i].label, x - 6, trackY + 16, width + 12, 40, 13, i == sprites.Count - 1 ? ChaosUI.Paper : ChaosUI.Muted, true, TextAnchor.UpperCenter);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                x += width + gap * scale;
            }
        }

        // ------------------------------------------------------------------ ontgrendelen

        private void ConfirmTrain(TrainKind kind)
        {
            var info = DepotCatalog.Train(kind);
            modal = () => UnlockDialog(info.Name, info.Sprite, info.Cost, $"Na level {info.UnlockLevel} komt deze trein ook gratis vrij.",
                $"Max. {info.MaxWagons} wagons · snelheid {info.Speed} / 5", () =>
                {
                    if (DepotProgress.Buy(kind))
                    {
                        modal = null;
                        ViewTrain(kind);
                        Flash($"{info.Name} staat in je depot");
                    }
                });
            Render();
        }

        private void ConfirmWagon(WagonKind kind)
        {
            var info = DepotCatalog.Wagon(kind);
            modal = () => UnlockDialog(info.Name, info.Sprite, info.Cost, $"Na level {info.UnlockLevel} komt deze wagon ook gratis vrij.",
                info.Capacity, () =>
                {
                    if (DepotProgress.Buy(kind))
                    {
                        modal = null;
                        Flash($"{info.Name} ontgrendeld voor {DepotCatalog.FormatTickets(info.Cost)} kaartjes");
                        Render();
                    }
                });
            Render();
        }

        private void UnlockDialog(string name, string sprite, int cost, string note, string facts, UnityEngine.Events.UnityAction confirm)
        {
            var veil = DepotUi.Panel(stage, "Sluier", -400, -200, 2400, 1300, new Color(.08f, .055f, .067f, .72f));
            veil.GetComponent<Image>().raycastTarget = true;
            var dialog = DepotUi.Panel(stage, "Ontgrendelen", 500, 160, 600, 560, DepotUi.PanelFace, DepotUi.Edge);
            DepotUi.Icon(dialog, "key", 24, 22, 44);
            DepotUi.Txt(dialog, name + " ontgrendelen?", 78, 18, 500, 52, 30, ChaosUI.Paper, true);
            DepotUi.Panel(dialog, "Treinvak", 24, 84, 552, 150, DepotUi.Inset);
            DepotUi.Vehicle(dialog, sprite, 60, 100, 480, 110);
            DepotUi.Track(dialog, 24, 214, 552);
            DepotUi.Chip(dialog, facts, 24, 248, ChaosUI.Slate, ChaosUI.Paper, null, 14, 30);
            var sums = DepotUi.Panel(dialog, "Saldo", 24, 290, 552, 124, DepotUi.Background);
            int balance = DepotProgress.Tickets;
            DepotUi.Txt(sums, "Je saldo", 18, 10, 300, 30, 17, ChaosUI.Muted);
            DepotUi.Txt(sums, DepotCatalog.FormatTickets(balance), 300, 10, 234, 30, 17, ChaosUI.Paper, true, TextAnchor.MiddleRight);
            DepotUi.Txt(sums, "Prijs", 18, 44, 300, 30, 17, ChaosUI.Muted);
            DepotUi.Txt(sums, "− " + DepotCatalog.FormatTickets(cost), 300, 44, 234, 30, 17, DepotUi.NegativeText, true, TextAnchor.MiddleRight);
            DepotUi.Panel(sums, "Lijn", 18, 80, 516, 2, DepotUi.Edge);
            DepotUi.Txt(sums, "Daarna over", 18, 84, 300, 34, 17, ChaosUI.Paper, true);
            DepotUi.Txt(sums, DepotCatalog.FormatTickets(balance - cost), 300, 84, 234, 34, 22, ChaosUI.Gold, true, TextAnchor.MiddleRight);
            DepotUi.Txt(dialog, note + " Kopen kan niet ongedaan worden.", 24, 420, 552, 44, 14, ChaosUI.Muted);
            DepotUi.Btn(dialog, "Annuleren", 236, 478, 160, 56, ChaosUI.Paper, () => { modal = null; Render(); }, 20);
            DepotUi.Btn(dialog, "Ontgrendel", 408, 478, 168, 56, ChaosUI.Gold, confirm, 20, true, "ticket");
        }

        private void RenderToast()
        {
            float w = DepotUi.Measure(toast, 16, false) + 64;
            var panel = DepotUi.Panel(stage, "Melding", 800 - w / 2, 92, w, 46, ChaosUI.Paper, ChaosUI.Hex("#BFA58A"));
            DepotUi.Icon(panel, toastWarning ? "warning" : "ready", 8, 7, 32);
            DepotUi.Txt(panel, toast, 48, 0, w - 52, 46, 16, ChaosUI.Ink);
        }
    }
}
