using ConductorChaos.Playground;
using ConductorChaos.Stations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    internal sealed class ServicePresentation
    {
        private readonly GameObject modal, hud, checklistPanel;
        private readonly Text engineMeters;
        private readonly Image modalBadge, objectiveIcon;
        private readonly Text kicker, heading, detail, route, actionText, waitText;
        private readonly Text hudTitle, hudDetail, timerText, checklist;
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
            var card = ChaosUI.MenuPanel(stage, "Dienstkaart", V(.245f, .175f), V(.755f, .825f));
            var badge = ChaosUI.Panel(card, "Sectie", V(.05f, .84f), V(.95f, .95f), ChaosUI.Paper);
            modalBadge = badge.GetComponent<Image>();
            kicker = Label(badge, "Sectie", 21, ChaosUI.Ink, V(.03f, 0), V(.97f, 1));
            heading = Label(card, "Titel", 38, ChaosUI.Paper, V(.055f, .67f), V(.95f, .83f));
            route = Label(card, "Route", 20, ChaosUI.GoldLight, V(.055f, .56f), V(.95f, .67f));
            var paper = ChaosUI.Panel(card, "Opdracht", V(.05f, .25f), V(.95f, .55f), ChaosUI.Paper);
            detail = Label(paper, "Details", 23, ChaosUI.Ink, V(.04f, .08f), V(.96f, .93f), FontStyle.Normal);
            detail.resizeTextForBestFit = true;          // lange briefing (level 2) krimpt mee in plaats van af te kappen
            detail.resizeTextMinSize = 14;
            detail.resizeTextMaxSize = 23;
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
            var objective = ChaosUI.Panel(inGame.transform, "Opdrachtkaart", V(.022f, .79f), V(.322f, .966f),
                ChaosUI.Paper, 2, ChaosUI.Dark);
            objectiveIcon = ChaosUISpriteCatalog.Add(objective, "Opdrachticoon", "route", V(.035f, .56f), V(.155f, .91f), ChaosUI.Ink) as Image;
            hudTitle = Label(objective, "Doel", 22, ChaosUI.Ink, V(.17f, .58f), V(.97f, .94f));
            hudDetail = Label(objective, "Huidige stap", 18, ChaosUI.Ink, V(.05f, .065f), V(.955f, .57f), FontStyle.Normal);
            var timer = ChaosUI.Panel(inGame.transform, "Dienstklok", V(.867f, .895f), V(.978f, .966f),
                ChaosUI.Navy, 2, ChaosUI.Dark);
            timerText = ChaosUI.Label(timer, "Resterende tijd", "", 26, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Level 2+: alle opdrachten tegelijk, als afvinklijst onder de opdrachtkaart.
            var list = ChaosUI.Panel(inGame.transform, "Checklist", V(.022f, .515f), V(.322f, .775f), ChaosUI.Paper, 2, ChaosUI.Dark);
            checklist = ChaosUI.Label(list, "Opdrachten", "", 17, ChaosUI.Ink, TextAnchor.UpperLeft, FontStyle.Normal,
                V(.05f, .04f), V(.96f, .95f));
            checklist.supportRichText = true;
            checklistPanel = list.gameObject;
            checklistPanel.SetActive(false);
            var boiler = ChaosUI.Panel(inGame.transform, "Ketel en kolen", V(.76f, .815f), V(.978f, .875f),
                ChaosUI.Navy, 2, ChaosUI.Dark);
            engineMeters = ChaosUI.Label(boiler, "Ketel en kolen", "", 18, ChaosUI.Paper, TextAnchor.MiddleCenter);
            boiler.gameObject.SetActive(false);
            hud = inGame.gameObject;
            hud.SetActive(false);
        }

        internal static bool IsModal(PlaygroundHost host, ServiceRun run) => host &&
            (host.Phase == PlaygroundHost.SessionPhase.Loading ||
             host.Phase == PlaygroundHost.SessionPhase.Playing && run &&
             run.Phase is ServiceRun.RunPhase.Briefing or ServiceRun.RunPhase.Won or ServiceRun.RunPhase.Failed);

        internal void Present(PlaygroundHost host, ServiceRun run, bool showModal, bool showHud)
        {
            bool modalChanged = modal.activeSelf != showModal;
            bool hudChanged = hud.activeSelf != showHud;
            if (modalChanged) modal.SetActive(showModal);
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

        private void PresentModal(PlaygroundHost host, ServiceRun run)
        {
            bool loading = host.Phase == PlaygroundHost.SessionPhase.Loading || !run;
            bool briefing = !loading && run.Phase == ServiceRun.RunPhase.Briefing;
            bool won = !loading && run.Phase == ServiceRun.RunPhase.Won;
            bool intro = !loading && run.Introduction;
            modalBadge.color = loading || briefing ? ChaosUI.Paper : won ? ChaosUI.Green : ChaosUI.TrainRed;
            kicker.color = !loading && !briefing && !won ? ChaosUI.Paper : ChaosUI.Ink;
            kicker.text = loading ? "SAMEN OP REIS" : briefing ? "BRIEFING  /  " + (intro ? "HOOFDSTATION" : run.LevelLabel) :
                won ? "DIENST GESLAAGD" : "DIENST AFGEBROKEN";
            heading.text = loading ? "Even geduld…" : briefing && intro ? "Je eerste werkdag" :
                briefing ? (run.CombinesObjectives ? "Over het ravijn" : run.Objective?.Title ?? "Eerste dienst") :
                won ? "Goed gewerkt, conducteurs!" :
                run.OffTrack ? "De trein is ontspoord" : "De tijd is om";
            route.text = loading ? "De crew laadt dezelfde scène." : intro ? $"{run.Departure.StationName}  ·  Oefenrit zonder tijdslimiet" :
                $"{run.Departure.StationName}   →   {run.Arrival.StationName}";
            detail.text = loading ? $"Verbonden crew  ·  {host.PlayerCount}" : briefing
                ? intro ? "Loop naar het perron, oefen met bagage en de treindeur en rijd een klein stukje. Eén geslaagde handeling telt voor de hele crew."
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
                    next == ServiceRun.ThirdLevelScene ? "Naar level 3" : "Volgende dienst") :
                "Opnieuw spelen";
            waitText.text = loading ? "Wachten op de crew…" : "Wachten op de host…";
            waitText.gameObject.SetActive(!advance.gameObject.activeSelf);
            leave.gameObject.SetActive(true);
        }

        private void PresentHud(ServiceRun run)
        {
            bool showEngine = false;
            if (run.Train && run.Train.HasCoalFurnace)
                foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                    if (player && player.IsOwner && run.Train.TryGetCabin(player.transform.position, .12f, out _))
                    {
                        showEngine = true;
                        break;
                    }
            engineMeters.transform.parent.gameObject.SetActive(showEngine);
            if (showEngine)
                engineMeters.text = $"Kolen {Mathf.RoundToInt(run.Train.FuelFraction * 100f)}%   ·   Hitte {Mathf.RoundToInt(run.Train.HeatFraction * 100f)}%";
            bool combined = run.CombinesObjectives;
            if (checklistPanel.activeSelf != combined) checklistPanel.SetActive(combined);
            if (run.Introduction)
            {
                hudTitle.text = "INTRODUCTIE  /  " + ((int)run.CurrentLesson + 1);
                hudDetail.text = run.CurrentLesson switch
                {
                    ServiceRun.Lesson.Walk => "WASD · Loop de hal uit naar het vertrekperron.",
                    ServiceRun.Lesson.Carry => "Houd een muisknop ingedrukt en draag de koffer naar het gemarkeerde vak.",
                    ServiceRun.Lesson.Ticket => "Houd één muisknop in om de tang vast te houden. Raak het kaartje met de bek en houd de andere muisknop in om te knippen.",
                    ServiceRun.Lesson.OpenDoor => "Grijp de handgreep van de wagon en schuif de deur open.",
                    ServiceRun.Lesson.CloseDoor => "Schuif de wagondeur weer helemaal dicht.",
                    ServiceRun.Lesson.Power => "Ga naar de cabine en druk met je hand op de startknop.",
                    ServiceRun.Lesson.Bell => "Druk met je hand de gele belknop op het perron omlaag.",
                    ServiceRun.Lesson.Drive when !run.DoorsClosed => "De wagondeur staat nog open. Schuif de deur dicht om te rijden.",
                    ServiceRun.Lesson.Drive when !run.PowerOn => "De trein staat uit. Druk op de startknop in de cabine.",
                    _ => $"{run.Progress}/5 meter · Houd de rijhendel vast en kijk omhoog. Zet hem daarna terug naar het midden."
                };
                timerText.text = "Vrije tijd";
                return;
            }

            string warning = run.SteamWarning ?? (ServiceIncidents.Active ? ServiceIncidents.Active.Warning : null) ?? SuitcaseWarning(run);
            if (combined)
            {
                hudTitle.text = $"{run.LevelLabel}  ·  {run.Progress}/{run.TotalTarget}";
                if (objectiveIcon)
                    objectiveIcon.sprite = ChaosUISpriteCatalog.Find(warning != null ? "warning" : run.Objective ? run.Objective.Icon : "route")
                                           ?? ChaosUISpriteCatalog.Find("route");
                hudDetail.text = warning ?? CombinedStep(run);
                hudDetail.color = warning != null ? ChaosUI.TrainRed : ChaosUI.Ink;
                checklist.text = Checklist(run);
            }
            else
            {
                hudTitle.text = run.Objective?.Title ?? "Eerste dienst";
                if (objectiveIcon && run.Objective)
                    objectiveIcon.sprite = ChaosUISpriteCatalog.Find(run.Objective.Icon) ?? ChaosUISpriteCatalog.Find("route");
                hudDetail.text = warning ?? NextServiceStep(run);
                hudDetail.color = warning != null ? ChaosUI.TrainRed : ChaosUI.Ink;
            }
            timerText.text = run.TimerRunning ? FormatTime(run.TimeLeft) : "Klaar?";
            timerText.color = run.TimerRunning && run.TimeLeft <= 30 ? ChaosUI.GoldLight : ChaosUI.Paper;
        }

        private static string SuitcaseWarning(ServiceRun run)
        {
            if (run.SuitcaseStatus == 2) return "De koffer viel onder het speelveld en staat weer in het bagagevak.";
            if (run.SuitcaseStatus != 1) return null;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && player.IsOwner)
                {
                    Vector3 direction = run.SuitcasePosition - player.transform.position;
                    string heading = Vector3.Dot(direction, player.transform.forward) < 0f ? "achter" : "voor";
                    return $"Koffer buiten het bagagevak, circa {Mathf.RoundToInt(direction.magnitude)} m {heading} je.";
                }
            return "Koffer buiten het bagagevak. Kijk in de wagon en langs het spoor.";
        }

        private static string NextServiceStep(ServiceRun run)
        {
            var objective = run.Objective;
            if (run.Phase == ServiceRun.RunPhase.Departure)
            {
                if (!run.TimerRunning) return "Loop naar het vertrekperron om de dienstklok te starten.";
                if (objective is ServiceTickets && run.Progress < objective.Target)
                    return $"Knip de crème kaartjes met de donkerblauwe strook. Open de perrondeur zodat de reizigers kunnen instappen. ({run.Progress}/{objective.Target})";
                if (objective is ServiceTickets tickets && run.AboardOf(tickets) < tickets.BoardingTarget)
                    return $"Houd de perrondeur open tot de reizigers zitten. ({run.AboardOf(tickets)}/{tickets.BoardingTarget})";
                if (objective is ServiceCargo && run.CargoLoaded < objective.Target)
                    return objective.Target == 1 ? "Leg de rode koffer in het bagagevak in de wagon."
                        : $"Zet de kratten in de open vrachtwagon. ({run.CargoLoaded}/{objective.Target} aan boord)";
                if (objective is ServiceEscort && run.Progress == 0)
                    return run.EscortDoorOpen ? "Pak oma met je handen vast en sleep haar de wagon in." : "Open de wagondeur aan het perron voor oma.";
                if (!run.BellRung) return "Druk de gele belknop op het vertrekperron omlaag.";
            }
            if (run.Phase == ServiceRun.RunPhase.Arrival)
            {
                if (objective is ServiceCargo && run.Progress < objective.Target)
                {
                    if (run.CargoTravelled < objective.Target)
                        return "Breng de achtergebleven vracht aan boord en rijd een stukje om die mee te laten tellen.";
                    return objective.Target == 1 ? "Zet de rode koffer in het groene bagagevak op het perron."
                        : $"Zet de kratten in het gele vrachtvak. ({run.Progress}/{objective.Target} gelost)";
                }
                if (objective is ServiceEscort && run.Progress < objective.Target)
                    return run.EscortDoorOpen ? "Pak oma met je handen vast en breng haar naar het eindperron." : "Open de wagondeur aan het perron voor oma.";
            }
            if (objective is ServiceRepair repair && run.Progress < objective.Target && !repair.IsBroken)
                return "Duw de wisselhendel naar de stand rechtdoor.";
            if (!run.DoorsClosed) return "Schuif de zijdeuren aan beide kanten van de wagon dicht.";
            if (!run.PowerOn) return "Druk op de startknop in de cabine.";
            if (run.NeedsCoal) return "Open de vuurkist en schep er kolen in.";
            if (objective is ServiceRepair broken && run.Progress < objective.Target)
                return run.Phase == ServiceRun.RunPhase.Departure
                    ? "Neem de moersleutel mee en rijd tot vóór de kapotte wissel."
                    : $"Stop vóór de wissel en tik met de moersleutel tegen de gele bout. ({broken.Hits}/{broken.RequiredHits})";
            return $"Rijd naar {run.Arrival.StationName} en zet de trein stil langs het eindperron.";
        }

        // ---------------------------------------------------------------- level 2+: alle opdrachten tegelijk
        private static string TicketInstructions => $"Knip de crème kaartjes met de donkerblauwe strook. " +
            $"Een verkeerd kaartje knippen kost {ServiceRun.WrongTicketPenaltySeconds} seconden. Open de perrondeur voor de reizigers.";

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
            if (checksTickets) text.AppendLine().Append(TicketInstructions);
            return text.ToString();
        }

        private static string Checklist(ServiceRun run)
        {
            var text = new System.Text.StringBuilder();
            foreach (var objective in run.ActiveObjectives)
            {
                int done = run.ProgressOf(objective);
                bool complete = done >= objective.Target;
                if (text.Length > 0) text.Append('\n');
                text.Append(complete ? "<color=#6E7F5A>■  " : "□  ").Append(objective.Title);
                if (objective.Target > 1) text.Append($"  {done}/{objective.Target}");
                if (complete) text.Append("</color>");
            }
            return text.ToString();
        }

        private static bool Done(ServiceRun run, ServiceObjective objective) => run.ProgressOf(objective) >= objective.Target;

        // De belangrijkste volgende stap over alle opdrachten heen, in de volgorde waarin je ze tegenkomt.
        private static string CombinedStep(ServiceRun run)
        {
            var objectives = run.ActiveObjectives;
            if (run.Phase == ServiceRun.RunPhase.Departure)
            {
                if (!run.TimerRunning) return "Loop naar het vertrekperron om de dienstklok te starten.";
                foreach (var objective in objectives)
                {
                    if (objective is ServiceTickets tickets)
                    {
                        if (!Done(run, objective))
                            return $"Knip de crème kaartjes met de donkerblauwe strook. ({run.ProgressOf(objective)}/{objective.Target})";
                        if (run.AboardOf(objective) < tickets.BoardingTarget)
                            return $"Laat de perrondeur open tot de reizigers zijn ingestapt. ({Mathf.Max(0, run.AboardOf(objective))}/{tickets.BoardingTarget})";
                    }
                    if (objective is ServiceCargo && run.AboardOf(objective) < objective.Target)
                        return objective.Target == 1 ? "Leg de rode koffer in het bagagevak in de wagon."
                            : $"Zet de kratten in de open vrachtwagon. ({Mathf.Max(0, run.AboardOf(objective))}/{objective.Target} aan boord)";
                    if (objective is ServiceEscort && run.ProgressOf(objective) == 0)
                        return run.EscortDoorOpen ? "Pak oma met je handen vast en sleep haar de wagon in." : "Open de wagondeur aan het perron voor oma.";
                }
                if (!run.BellRung) return "Alles aan boord? Druk de gele belknop op het vertrekperron omlaag.";
            }
            if (run.Phase == ServiceRun.RunPhase.Arrival)
                foreach (var objective in objectives)
                {
                    if (objective is ServiceCargo && !Done(run, objective))
                        return objective.Target == 1 ? "Zet de rode koffer in het groene bagagevak op het perron."
                            : $"Zet de kratten in het gele vrachtvak. ({run.ProgressOf(objective)}/{objective.Target} gelost)";
                    if (objective is ServiceEscort && !Done(run, objective))
                        return run.EscortDoorOpen ? "Pak oma met je handen vast en breng haar naar het eindperron." : "Open de wagondeur aan het perron voor oma.";
                }
            if (!run.DoorsClosed) return "Schuif de zijdeuren aan beide kanten van de wagon dicht.";
            if (!run.PowerOn) return "Druk op de startknop in de cabine.";
            if (run.NeedsCoal) return "Het vuur is uit! Open de vuurkist en schep er kolen in.";
            foreach (var objective in objectives)
            {
                if (objective is ServiceRepair repair && !Done(run, objective))
                    return repair.IsBroken
                        ? $"Stop vóór de kapotte wissel en tik met de moersleutel tegen de gele bout. ({repair.Hits}/{repair.RequiredHits})"
                        : "Duw de wisselhendel naar de stand rechtdoor.";
                if (objective is ServiceTrackRepair rail && !Done(run, objective) && rail.IsBroken)
                    return $"Stop vóór de kapotte rail bij de brug en sla de gele bout vast met de moersleutel. ({rail.Hits}/{rail.RequiredHits})";
                if (objective is ServiceSwitchRoute && !Done(run, objective))
                    return $"Wissels staan naar een zijspoor: stop ervoor en duw de hendel met je hand om. ({run.ProgressOf(objective)}/{objective.Target})";
            }
            return $"Rijd naar {run.Arrival.StationName} en zet de trein stil langs het eindperron.";
        }

        private static string FormatTime(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private static Text Label(Transform parent, string name, int size, Color color, Vector2 min, Vector2 max,
            FontStyle style = FontStyle.Bold) =>
            ChaosUI.Label(parent, name, "", size, color, TextAnchor.MiddleLeft, style, min, max);
    }
}
