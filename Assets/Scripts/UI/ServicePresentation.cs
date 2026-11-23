using ConductorChaos.Depot;
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using ConductorChaos.UI.Depot;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    internal sealed class ServicePresentation
    {
        private readonly GameObject modal, hud, warningPanel;
        private readonly Text warningText;
        private readonly DepotResults results;
        private readonly ObjectiveCard objectiveCard;
        private readonly System.Collections.Generic.List<ObjectiveCard.Row> rows = new();
        private readonly PlaygroundHost host;
        private ServiceRun rewardedRun;
        private RunReward reward;
        /// <summary>Host na een gehaalde dienst: open het depot voor het volgende level.</summary>
        internal System.Action<int> OpenDepot;
        private readonly Text engineMeters;
        private readonly Image modalBadge;
        private readonly RectTransform modalCard, modalPaper;
        private readonly ManifestBoard manifestBoard;
        private readonly Text kicker, heading, detail, route, actionText, waitText;
        private readonly Button advance, leave;
        private float nextUpdate;

        private static Vector2 V(float x, float y) => new(x, y);

        internal ServicePresentation(Transform parent, PlaygroundHost host)
        {
            var overlay = ChaosUI.MakeCanvas(parent, "Dienstbriefing en resultaat", true);
            overlay.sortingOrder = 50;
            var scrim = ChaosUI.Dark;
            scrim.a = .28f;
            ChaosUI.Panel(overlay.transform, "Scrim", V(0, 0), V(1, 1), scrim);
            var stage = ChaosUI.Rect(overlay.transform, "Dienstcompositie", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
            var fit = stage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9f;
            var card = modalCard = ChaosUI.MenuPanel(stage, "Dienstkaart", V(.245f, .175f), V(.755f, .825f));
            var badge = ChaosUI.Panel(card, "Sectie", V(.05f, .84f), V(.95f, .95f), ChaosUI.Paper);
            modalBadge = badge.GetComponent<Image>();
            kicker = Label(badge, "Sectie", 21, ChaosUI.Ink, V(.03f, 0), V(.97f, 1));
            heading = Label(card, "Titel", 38, ChaosUI.Paper, V(.055f, .67f), V(.95f, .83f));
            route = Label(card, "Route", 20, ChaosUI.GoldLight, V(.055f, .56f), V(.95f, .67f));
            var paper = modalPaper = ChaosUI.Panel(card, "Opdracht", V(.05f, .25f), V(.95f, .55f), ChaosUI.Paper);
            detail = Label(paper, "Details", 23, ChaosUI.Ink, V(.04f, .08f), V(.96f, .93f), FontStyle.Normal);
            detail.resizeTextForBestFit = true;          // lange briefing (level 2) krimpt mee in plaats van af te kappen
            detail.resizeTextMinSize = 14;
            detail.resizeTextMaxSize = 23;
            manifestBoard = new ManifestBoard(paper);
            advance = ChaosUI.Action(card, "Verder", "Verder", V(.05f, .055f), V(.59f, .195f),
                ChaosUI.Gold, ChaosUI.Ink, () => ServiceRun.Active?.Continue(), 25, iconKey: "play");
            actionText = advance.GetComponentInChildren<Text>();
            leave = ChaosUI.Action(card, "Verlaten", "Terug naar menu", V(.62f, .055f), V(.95f, .195f),
                ChaosUI.Paper, ChaosUI.Ink, () => host?.Leave(), 19, iconKey: "back");
            waitText = Label(card, "Wachten", 17, ChaosUI.Muted, V(.055f, .065f), V(.59f, .19f));
            modal = overlay.gameObject;
            modal.SetActive(false);

            var inGame = ChaosUI.MakeCanvas(parent, "Dienst-HUD", false);
            inGame.sortingOrder = 22;
            // Linksboven: compacte opdrachtkaart met rode kop, klok, volgende stap en afvinklijst (#239).
            objectiveCard = new ObjectiveCard(inGame.transform);
            // Rechtsboven: hooguit één waarschuwing tegelijk.
            var warning = ChaosUI.Panel(inGame.transform, "Waarschuwing", V(.66f, .895f), V(.985f, .975f), ChaosUI.TrainRed, 2, ChaosUI.TrainRedDark);
            ChaosUISpriteCatalog.Add(warning, "Waarschuwingsicoon", "warning", V(.02f, .12f), V(.13f, .88f), Color.white);
            warningText = ChaosUI.Label(warning, "Melding", "", 17, DepotUi.RedText, TextAnchor.MiddleLeft, FontStyle.Bold, V(.14f, .04f), V(.98f, .96f));
            warningText.resizeTextForBestFit = true;
            warningText.resizeTextMinSize = 11;
            warningText.resizeTextMaxSize = 17;
            warningPanel = warning.gameObject;
            warningPanel.SetActive(false);
            var boiler = ChaosUI.Panel(inGame.transform, "Ketel en kolen", V(.80f, .835f), V(.985f, .885f),
                ChaosUI.Dark, 2, DepotUi.Edge);
            engineMeters = ChaosUI.Label(boiler, "Ketel en kolen", "", 17, ChaosUI.Paper, TextAnchor.MiddleCenter);
            boiler.gameObject.SetActive(false);
            hud = inGame.gameObject;
            hud.SetActive(false);
            results = new DepotResults(parent);
            this.host = host;
        }

        internal static bool IsModal(PlaygroundHost host, ServiceRun run) => host &&
            (host.Phase == PlaygroundHost.SessionPhase.Loading ||
             host.Phase == PlaygroundHost.SessionPhase.Playing && run &&
             run.Phase is ServiceRun.RunPhase.Briefing or ServiceRun.RunPhase.Won or ServiceRun.RunPhase.Failed);

        internal void Present(PlaygroundHost host, ServiceRun run, bool showModal, bool showHud, bool depotOpen = false)
        {
            bool won = run && host && host.Phase == PlaygroundHost.SessionPhase.Playing && run.Phase == ServiceRun.RunPhase.Won;
            if (won && rewardedRun != run) Reward(run);
            if (!won) rewardedRun = null;
            if (won && showModal && !depotOpen)
            {
                if (!results.Visible) ShowResults(host, run);
                showModal = false;
            }
            else results.Hide();
            if (depotOpen) showModal = false;
            bool modalChanged = modal.activeSelf != showModal;
            bool hudChanged = hud.activeSelf != showHud;
            if (modalChanged) modal.SetActive(showModal);
            if (!showModal) manifestBoard.Hide();
            if (hudChanged) hud.SetActive(showHud);
            if (!showModal && !showHud) return;

            if (!modalChanged && !hudChanged && Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .10f;
            if (showModal)
            {
                PresentModal(host, run);
                if (EventSystem.current && !EventSystem.current.currentSelectedGameObject)
                    EventSystem.current.SetSelectedGameObject(advance.isActiveAndEnabled && advance.interactable
                        ? advance.gameObject : leave.gameObject);
            }
            if (showHud) PresentHud(run);
        }

        // ---------------------------------------------------------------- resultaat en kaartjes (#239)

        private void Reward(ServiceRun run)
        {
            rewardedRun = run;
            var level = DepotCatalog.LevelForScene(run.gameObject.scene.name);
            int done = 0;
            if (run.Introduction) done = 0;
            else if (run.CombinesObjectives)
            {
                foreach (var objective in run.ActiveObjectives) if (run.ProgressOf(objective) >= objective.Target) done++;
            }
            else if (run.Objective && run.Progress >= run.Objective.Target) done = 1;
            bool repaired = run.SteamWarning == null;
            if (run.Train)
                foreach (var point in run.Train.GetComponentsInChildren<Track.RepairPoint>())
                    if (point.IsSpawned && point.IsBroken) repaired = false;
            if (run.Train)
                foreach (var wagon in run.Train.CoupledWagons)
                    if (wagon)
                        foreach (var point in wagon.GetComponentsInChildren<Track.RepairPoint>())
                            if (point.IsSpawned && point.IsBroken) repaired = false;
            reward = DepotProgress.AwardWin(level != null ? level.Index : 0, run.TimeLeft, done, repaired, run.BonusPassengers, run.BonusPost);
        }

        private void ShowResults(PlaygroundHost host, ServiceRun run)
        {
            if (reward == null) return;
            var level = DepotCatalog.LevelForScene(run.gameObject.scene.name) ?? DepotCatalog.Levels[0];
            string next = ServiceRun.NextScene(run.gameObject.scene.name);
            var nextLevel = next != null && Application.CanStreamedLevelBeLoaded(next) ? DepotCatalog.LevelForScene(next) : null;
            string scene = run.gameObject.scene.name;
            results.Show(reward, level, run.Departure ? run.Departure.StationName : "", run.Arrival && !run.Introduction ? run.Arrival.StationName : "",
                host.PlayerCount, host.IsHost, nextLevel != null ? nextLevel.Index : -1,
                () => { if (nextLevel != null) OpenDepot?.Invoke(nextLevel.Index); },
                () => host.LoadServiceScene(scene),
                () => host.Leave());
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        private void PresentModal(PlaygroundHost host, ServiceRun run)
        {
            bool loading = host.Phase == PlaygroundHost.SessionPhase.Loading || !run;
            bool briefing = !loading && run.Phase == ServiceRun.RunPhase.Briefing;
            bool won = !loading && run.Phase == ServiceRun.RunPhase.Won;
            bool intro = !loading && run.Introduction;
            bool onBoard = briefing && run.HasStops && run.Manifest.IsReady;
            modalCard.anchorMin = onBoard ? V(.095f, .075f) : V(.245f, .175f);
            modalCard.anchorMax = onBoard ? V(.905f, .925f) : V(.755f, .825f);
            modalPaper.anchorMin = onBoard ? V(.05f, .207f) : V(.05f, .25f);
            detail.rectTransform.anchorMin = onBoard ? V(.04f, .725f) : V(.04f, .08f);
            if (onBoard) manifestBoard.Show(run.Manifest, run.PickupStation);
            else manifestBoard.Hide();
            modalBadge.color = loading || briefing ? ChaosUI.Paper : won ? ChaosUI.Green : ChaosUI.TrainRed;
            kicker.color = !loading && !briefing && !won ? ChaosUI.Paper : ChaosUI.Ink;
            kicker.text = loading ? "SAMEN OP REIS" : briefing ? "BRIEFING  /  " + (intro ? "HOOFDSTATION" : run.LevelLabel) :
                won ? "DIENST GESLAAGD" : "DIENST AFGEBROKEN";
            heading.text = loading ? "Even geduld…" : briefing && intro ? "Je eerste werkdag" :
                briefing ? (run.CombinesObjectives ? DepotCatalog.LevelForScene(run.gameObject.scene.name)?.Name ?? "Dienst" : run.Objective?.Title ?? "Eerste dienst") :
                won ? "Goed gewerkt, conducteurs!" :
                run.OffTrack ? "De trein is ontspoord" : run.LostForForbiddenFreight ? "Verboden vracht meegenomen" : "De tijd is om";
            route.text = loading ? "De crew laadt dezelfde scène." : intro ? $"{run.Departure.StationName}  ·  Oefenrit zonder tijdslimiet" :
                $"{run.Departure.StationName}   →   {run.Arrival.StationName}";
            detail.text = loading ? $"Verbonden crew  ·  {host.PlayerCount}" : briefing
                ? intro ? "Loop naar het perron, oefen met bagage en de treindeur en rijd een klein stukje. Eén geslaagde handeling telt voor de hele crew."
                    : onBoard ? MemoryBriefing(run, host.PlayerCount)
                    : run.CombinesObjectives
                        ? $"{CombinedBriefing(run)}\n\nTijdslimiet {FormatTime(run.TimeLimit(host.PlayerCount))}. De klok start op het vertrekperron."
                        : $"{(run.Objective is ServiceTickets ? TicketInstructions : run.Objective?.Instructions)}\n\nTijdslimiet {FormatTime(run.TimeLimit(host.PlayerCount))}. De klok start zodra een teamgenoot het vertrekperron bereikt."
                : intro ? won ? "De training is afgerond. Jullie eerste dienst kan beginnen." : "Start de training opnieuw."
                : run.CombinesObjectives
                    ? $"Opdrachten   {run.Progress}/{run.TotalTarget}\nTijd over  {FormatTime(run.TimeLeft)}"
                    : $"{run.Objective?.Title}   {run.Progress}/{run.Objective?.Target}\nTijd over  {FormatTime(run.TimeLeft)}";
            advance.gameObject.SetActive(!loading && host.IsHost);
            advance.interactable = !loading && run.CanContinue;
            string next = won ? ServiceRun.NextScene(run.gameObject.scene.name) : null;
            bool hasNext = next != null && Application.CanStreamedLevelBeLoaded(next);
            actionText.text = briefing ? (intro ? "Begin training" : "Begin dienst") :
                hasNext ? (next == ServiceRun.FirstLevelScene ? "Naar level 1" : next == ServiceRun.SecondLevelScene ? "Naar level 2" :
                    next == ServiceRun.ThirdLevelScene ? "Naar level 3" : next == ServiceRun.FourthLevelScene ? "Naar level 4" : next == ServiceRun.FifthLevelScene ? "Naar level 5" : "Volgende dienst") :
                "Opnieuw spelen";
            waitText.text = loading ? "Wachten op de crew…" : "Wachten op de host…";
            waitText.gameObject.SetActive(!advance.gameObject.activeSelf);
            leave.gameObject.SetActive(true);
        }

        private void PresentHud(ServiceRun run)
        {
            bool showEngine = false;
            if (run.Train && (run.Train.HasCoalFurnace || run.Train.HasDieselTank))
                foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                    if (player && player.IsOwner && run.Train.TryGetCabin(player.transform.position, .12f, out _))
                    {
                        showEngine = true;
                        break;
                    }
            engineMeters.transform.parent.gameObject.SetActive(showEngine);
            if (showEngine)
                engineMeters.text = run.Train.HasDieselTank ? $"Diesel {Mathf.RoundToInt(run.Train.FuelFraction * 100f)}%"
                    : $"Kolen {Mathf.RoundToInt(run.Train.FuelFraction * 100f)}%   ·   Hitte {Mathf.RoundToInt(run.Train.HeatFraction * 100f)}%";
            rows.Clear();
            if (run.Introduction)
            {
                string lesson = run.CurrentLesson switch
                {
                    ServiceRun.Lesson.Walk => "Loop de hal uit naar het vertrekperron.",
                    ServiceRun.Lesson.Carry => "Houd een muisknop ingedrukt en draag de koffer naar het gemarkeerde vak.",
                    ServiceRun.Lesson.Ticket => "Houd de tang vast met één muisknop. Raak het kaartje met de bek en knijp met de andere.",
                    ServiceRun.Lesson.OpenDoor => "Grijp de handgreep en schuif de deur open.",
                    ServiceRun.Lesson.CloseDoor => "Schuif de deur weer dicht.",
                    ServiceRun.Lesson.Power => "Druk in de cabine op de startknop.",
                    ServiceRun.Lesson.Bell => "Druk de gele belknop op het perron omlaag.",
                    ServiceRun.Lesson.Drive when !run.DoorsClosed => "De deur staat nog open. Schuif hem dicht.",
                    ServiceRun.Lesson.Drive when !run.PowerOn => "Druk in de cabine op de startknop.",
                    _ => $"Houd de rijhendel vast en rijd een stukje. ({run.Progress}/5 m)"
                };
                objectiveCard.Show("Training " + ((int)run.CurrentLesson + 1), "route", "Vrij", false, lesson, rows);
                if (warningPanel.activeSelf) warningPanel.SetActive(false);
                return;
            }

            string warning = run.SteamWarning ?? (ServiceIncidents.Active ? ServiceIncidents.Active.Warning : null) ?? SuitcaseWarning(run) ??
                             ColdWarning() ?? BreakdownNotice.For(run) ?? StopNotice(run);
            if (warningPanel.activeSelf != (warning != null)) warningPanel.SetActive(warning != null);
            if (warning != null) warningText.text = warning;
            foreach (var objective in run.ActiveObjectives)
                rows.Add(new ObjectiveCard.Row(objective is ServiceWaterDelivery ? $"{objective.Title} · {run.WaterPercent}% / 40%" : objective.Title,
                    run.ProgressOf(objective), objective.Target));
            string icon = run.Objective ? run.Objective.Icon : "route";
            if (!ChaosUISpriteCatalog.Find(icon)) icon = "route";
            string step = run.CombinesObjectives ? CombinedStep(run) : NextServiceStep(run);
            objectiveCard.Show(run.LevelLabel, icon, run.TimerRunning ? FormatTime(run.TimeLeft) : "Klaar?",
                run.TimerRunning && run.TimeLeft <= 30, step, rows);
        }

        private static string StopNotice(ServiceRun run)
        {
            var stop = run.NearbyStop();
            if (!stop) return null;
            return run.Train.ServiceCoachVisible
                ? $"{stop.name} · reizigers en vracht"
                : $"{stop.name} · vracht";
        }

        // Winter (#243): alleen melden dat het koud is of dat iemand bevroren is, niet wat je moet doen.
        private static readonly System.Collections.Generic.List<PlaygroundPassenger> passengers = new();
        private static float nextPassengerScan;

        private static string ColdWarning()
        {
            if (!ConductorChaos.Nature.WinterLevel.Active) return null;
            if (Time.unscaledTime > nextPassengerScan)
            {
                nextPassengerScan = Time.unscaledTime + 1f;
                passengers.Clear();
                passengers.AddRange(Object.FindObjectsByType<PlaygroundPassenger>());
            }
            bool chilly = false;
            foreach (var passenger in passengers)
            {
                if (!passenger || !passenger.IsSpawned) continue;
                if (passenger.IsFrozen) return "Reiziger bevroren";
                chilly |= passenger.Cold >= .7f;
            }
            return chilly ? "Reizigers krijgen het koud" : null;
        }

        private static string SuitcaseWarning(ServiceRun run)
        {
            if (run.SuitcaseStatus == 2) return "De koffer viel onder het spoor en ligt weer in de trein.";
            if (run.SuitcaseStatus != 1) return null;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && player.IsOwner)
                {
                    Vector3 direction = run.SuitcasePosition - player.transform.position;
                    string heading = Vector3.Dot(direction, player.transform.forward) < 0f ? "achter" : "voor";
                    return $"Koffer uit de trein, {Mathf.RoundToInt(direction.magnitude)} m {heading} je.";
                }
            return "Koffer uit de trein gevallen.";
        }

        // Korte stappen: wat nu fysiek moet gebeuren. Geen toetsen, alleen handen.
        private static string Count(int done, int target) => target > 1 ? $" ({Mathf.Max(0, done)}/{target})" : "";

        private static string BoardEscort(ServiceRun run) => !run.HasPassengerCoach ? "Help oma met je handen de trein in."
            : run.EscortDoorOpen ? "Help oma met je handen de wagon in." : "Open de wagondeur voor oma.";

        private static string AlightEscort(ServiceRun run) => run.HasPassengerCoach && !run.EscortDoorOpen
            ? "Open de wagondeur voor oma." : "Help oma naar het perron.";

        private static string LoadCargo(ServiceObjective objective, int aboard) => objective.Target == 1
            ? "Zet de rode koffer in de trein." : "Zet de kratten in of op de trein." + Count(aboard, objective.Target);

        private static string UnloadCargo(ServiceObjective objective, int done) => objective.Target == 1
            ? "Zet de koffer in het groene vak op het perron." : "Zet de kratten in het gele vak." + Count(done, objective.Target);

        private static string NextServiceStep(ServiceRun run)
        {
            var objective = run.Objective;
            if (run.Phase == ServiceRun.RunPhase.Departure)
            {
                if (!run.TimerRunning) return "Loop naar het vertrekperron.";
                if (objective is ServiceTickets && run.Progress < objective.Target)
                    return "Knip de kaartjes." + Count(run.Progress, objective.Target);
                if (objective is ServiceTickets tickets && run.AboardOf(tickets) < tickets.BoardingTarget)
                    return "Houd de deur open voor de reizigers." + Count(run.AboardOf(tickets), tickets.BoardingTarget);
                if (objective is ServiceCargo && run.CargoLoaded < objective.Target) return LoadCargo(objective, run.CargoLoaded);
                if (objective is ServiceEscort && run.Progress == 0) return BoardEscort(run);
                if (!run.BellRung) return "Druk op de vertrekbel.";
            }
            if (run.Phase == ServiceRun.RunPhase.Arrival)
            {
                if (objective is ServiceCargo && run.Progress < objective.Target)
                    return run.CargoTravelled < objective.Target ? "Er reisde nog vracht niet mee. Laad in en rijd een stukje."
                        : UnloadCargo(objective, run.Progress);
                if (objective is ServiceEscort && run.Progress < objective.Target) return AlightEscort(run);
            }
            if (objective is ServiceRepair repair && run.Progress < objective.Target && !repair.IsBroken)
                return "Duw de wisselhendel naar rechtdoor.";
            if (!run.DoorsClosed) return "Schuif de wagondeuren dicht.";
            if (!run.PowerOn) return "Druk op de startknop in de cabine.";
            if (run.NeedsCoal) return "Schep kolen in de vuurkist.";
            if (run.NeedsDiesel) return "Tank leeg: tank bij met de pompslang op het perron of een jerrycan (gele vuldop).";
            if (objective is ServiceRepair broken && run.Progress < objective.Target)
                return run.Phase == ServiceRun.RunPhase.Departure ? "Neem de moersleutel mee naar de kapotte wissel."
                    : "Tik met de moersleutel op de gele bout." + Count(broken.Hits, broken.RequiredHits);
            return $"Stop langs het perron van {run.Arrival.StationName}.";
        }

        // ---------------------------------------------------------------- level 2+: alle opdrachten tegelijk
        private static string TicketInstructions => $"Knip de crème kaartjes met de donkerblauwe strook. " +
            $"Een verkeerd kaartje knippen kost {ServiceRun.WrongTicketPenaltySeconds} seconden. Open de perrondeur voor de reizigers.";

        private static string MemoryBriefing(ServiceRun run, int crew) =>
            $"Onthoud het bord samen. Bij ‘Begin dienst’ verdwijnt het.\n" +
            $"Tijd {FormatTime(run.TimeLimit(crew))} · Verkeerd knippen kost {ServiceRun.WrongTicketPenaltySeconds} s · Verboden vracht beëindigt de rit.";

        private static string CombinedBriefing(ServiceRun run)
        {
            var text = new System.Text.StringBuilder("Opdrachten voor deze rit: ");
            bool first = true, checksTickets = false;
            foreach (var objective in run.ActiveObjectives)
            {
                if (!first) text.Append("  ·  ");
                text.Append(objective.Title);
                checksTickets |= objective is ServiceTickets;
                first = false;
            }
            bool switches = false;
            foreach (var objective in run.ActiveObjectives) switches |= objective is ServiceSwitchRoute;
            text.Append(switches
                ? ".\nOnderweg kan het misgaan: wissels die naar een zijspoor staan, rotsen op het spoor, spoorschade en kolen die opraken."
                : ".\nOnderweg kan het misgaan: rotsen op het spoor, spoorschade en kolen die opraken.");
            text.Append(run.gameObject.scene.name switch
            {
                ServiceRun.SecondLevelScene => "\nLet op de gele waarschuwingsborden voor steenslag en spoorschade.",
                ServiceRun.ThirdLevelScene => "\nVolg het hoofdspoor of neem de gemarkeerde doorsteek. Neem op de doorsteek gas terug.",
                ServiceRun.FourthLevelScene => "\nPak de gratis sneeuwschep op het vertrekperron of rijd stapvoets met de gekochte sneeuwploeg.",
                ServiceRun.FifthLevelScene => "\nStop met de ketelwagon onder de vulpijp in de beukenlaan. Open het tankluik, laat de pijp zakken en draai de kraan open. Breng minstens 40% water naar Fernwood. Rem voor boomstammen en neem gas terug in scherpe bochten.",
                _ => string.Empty
            });
            if (checksTickets) text.AppendLine().Append(TicketInstructions);
            if (run.HasStops) text.Append("\nTussenhaltes zijn optioneel. Knip geldige kaartjes en open de perrondeur of neem de postkist mee voor bonuskaartjes.");
            return text.ToString();
        }

        private static bool Done(ServiceRun run, ServiceObjective objective) => run.ProgressOf(objective) >= objective.Target;

        // De belangrijkste volgende stap over alle opdrachten heen, in de volgorde waarin je ze tegenkomt.
        private static string CombinedStep(ServiceRun run)
        {
            var objectives = run.ActiveObjectives;
            if (run.Phase == ServiceRun.RunPhase.Departure)
            {
                if (!run.TimerRunning) return "Loop naar het vertrekperron.";
                foreach (var objective in objectives)
                {
                    if (objective is ServiceTickets tickets)
                    {
                        if (!Done(run, objective)) return "Knip de kaartjes." + Count(run.ProgressOf(objective), objective.Target);
                        if (run.AboardOf(objective) < tickets.BoardingTarget)
                            return "Houd de deur open voor de reizigers." + Count(run.AboardOf(objective), tickets.BoardingTarget);
                    }
                    if (objective is ServiceCargo && run.AboardOf(objective) < objective.Target)
                        return LoadCargo(objective, run.AboardOf(objective));
                    if (objective is ServiceEscort && run.ProgressOf(objective) == 0) return BoardEscort(run);
                }
                if (!run.BellRung) return "Alles aan boord? Druk op de vertrekbel.";
            }
            if (run.Phase == ServiceRun.RunPhase.Arrival)
                foreach (var objective in objectives)
                {
                    if (objective is ServiceCargo && !Done(run, objective)) return UnloadCargo(objective, run.ProgressOf(objective));
                    if (objective is ServiceEscort && !Done(run, objective)) return AlightEscort(run);
                }
            if (run.HasStops && run.Phase == ServiceRun.RunPhase.Arrival && run.Progress >= run.TotalTarget)
                return "Controleer samen of jullie de afspraken uit de briefing hebben uitgevoerd.";
            if (!run.DoorsClosed) return "Schuif de wagondeuren dicht.";
            if (!run.PowerOn) return "Druk op de startknop in de cabine.";
            if (run.NeedsCoal) return "Het vuur is uit: schep kolen in de vuurkist.";
            if (run.NeedsDiesel) return "De diesel is op: giet een jerrycan leeg boven de gele vuldop.";
            if (run.LowDiesel) return "Diesel bijna op: giet een jerrycan leeg boven de gele vuldop.";
            foreach (var objective in objectives)
            {
                if (objective is ServiceRepair repair && !Done(run, objective))
                    return repair.IsBroken ? "Tik met de moersleutel op de gele bout van de wissel." + Count(repair.Hits, repair.RequiredHits)
                        : "Duw de wisselhendel naar rechtdoor.";
                if (objective is ServiceTrackRepair rail && !Done(run, objective) && rail.IsBroken)
                    return "Sla de gele bout bij de brug vast." + Count(rail.Hits, rail.RequiredHits);
                if (objective is ServiceWaterDelivery && run.WaterPercent < 40)
                    return "Vul de ketelwagon bij de watertoren in de beukenlaan.";
                if (objective is ServiceSwitchRoute && !Done(run, objective))
                    return "Duw de wisselhendel om." + Count(run.ProgressOf(objective), objective.Target);
            }
            return $"Stop langs het perron van {run.Arrival.StationName}.";
        }

        private static string FormatTime(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private static Text Label(Transform parent, string name, int size, Color color, Vector2 min, Vector2 max,
            FontStyle style = FontStyle.Bold) =>
            ChaosUI.Label(parent, name, "", size, color, TextAnchor.MiddleLeft, style, min, max);
    }
}
