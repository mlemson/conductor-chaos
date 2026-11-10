using ConductorChaos.Playground;
using ConductorChaos.Stations;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    internal sealed class ServicePresentation
    {
        private readonly GameObject modal, hud, prompt;
        private readonly Image modalBadge, objectiveIcon;
        private readonly Text kicker, heading, detail, route, actionText, waitText;
        private readonly Text hudTitle, hudDetail, timerText;
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
            var bell = ChaosUI.Panel(inGame.transform, "Interactietip", V(.405f, .07f), V(.595f, .126f), ChaosUI.Paper);
            ChaosUI.Label(bell, "Passagier", "[E]  Help oma", 18, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            prompt = bell.gameObject;
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

            if (showHud)
            {
                var player = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
                bool canAssist = player && run.CanAssistFrom(player.transform.position);
                if (prompt.activeSelf != canAssist) prompt.SetActive(canAssist);
                if (canAssist && Cursor.lockState == CursorLockMode.Locked && Keyboard.current?.eKey.wasPressedThisFrame == true)
                    run.AssistPassenger();
            }
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
            kicker.text = loading ? "SAMEN OP REIS" : briefing ? "BRIEFING  /  " + (intro ? "HOOFDSTATION" : "LEVEL 1") :
                won ? "DIENST GESLAAGD" : "DIENST AFGEBROKEN";
            heading.text = loading ? "Even geduld…" : briefing && intro ? "Je eerste werkdag" :
                briefing ? run.Objective?.Title ?? "Eerste dienst" : won ? "Goed gewerkt, conducteurs!" :
                run.OffTrack ? "De trein is ontspoord" : "De tijd is om";
            route.text = loading ? "De crew laadt dezelfde scène." : intro ? "Whistleton  ·  Oefenrit zonder tijdslimiet" :
                $"{run.Departure.StationName}   →   {run.Arrival.StationName}";
            detail.text = loading ? $"Verbonden crew  ·  {host.PlayerCount}" : briefing
                ? intro ? "Loop naar het perron, oefen met bagage en de treindeur en rijd een klein stukje. Eén geslaagde handeling telt voor de hele crew."
                    : $"{run.Objective?.Instructions}\n\nTijdslimiet {FormatTime(ServiceRun.TimeLimit(host.PlayerCount))}. De klok start zodra een teamgenoot het vertrekperron bereikt."
                : intro ? won ? "De training is afgerond. Jullie eerste dienst kan beginnen." : "Start de training opnieuw."
                : $"{run.Objective?.Title}   {run.Progress}/{run.Objective?.Target}\nTijd over  {FormatTime(run.TimeLeft)}";
            advance.gameObject.SetActive(!loading && host.IsHost);
            advance.interactable = !loading && run.CanContinue;
            actionText.text = briefing ? (intro ? "Begin training" : "Begin dienst") : won && intro ? "Naar level 1" : "Opnieuw spelen";
            waitText.text = loading ? "Wachten op de crew…" : "Wachten op de host…";
            waitText.gameObject.SetActive(!advance.gameObject.activeSelf);
            leave.gameObject.SetActive(true);
        }

        private void PresentHud(ServiceRun run)
        {
            if (run.Introduction)
            {
                hudTitle.text = "INTRODUCTIE  /  " + ((int)run.CurrentLesson + 1);
                hudDetail.text = run.CurrentLesson switch
                {
                    ServiceRun.Lesson.Walk => "WASD · Loop de hal uit naar het vertrekperron.",
                    ServiceRun.Lesson.Carry => "Houd een muisknop ingedrukt en draag de koffer naar het gemarkeerde vak.",
                    ServiceRun.Lesson.Ticket => "Houd de tang vast met één hand. Druk met je vrije hand op het handvat terwijl de bek het kaartje raakt.",
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

            hudTitle.text = run.Objective?.Title ?? "Eerste dienst";
            if (objectiveIcon && run.Objective)
                objectiveIcon.sprite = ChaosUISpriteCatalog.Find(run.Objective.Icon) ?? ChaosUISpriteCatalog.Find("route");
            hudDetail.text = run.Phase switch
            {
                ServiceRun.RunPhase.Departure when !run.TimerRunning => "Loop naar het vertrekperron om de dienstklok te starten.",
                ServiceRun.RunPhase.Departure when !run.BellRung => "Druk de gele belknop op het vertrekperron omlaag.",
                ServiceRun.RunPhase.Departure => $"{run.Objective?.Instructions}\nDeuren {Mark(run.DoorsClosed)}   Stoom {Mark(run.PowerOn)}   Bel {Mark(run.BellRung)}",
                ServiceRun.RunPhase.Arrival => $"{run.Arrival.StationName} · {run.Progress}/{run.Objective?.Target}\n{run.Objective?.Instructions}",
                _ => $"{run.Arrival.StationName} · Rijd naar de groene stopmarkering.\n{run.Objective?.Instructions}"
            };
            timerText.text = run.TimerRunning ? FormatTime(run.TimeLeft) : "Klaar?";
            timerText.color = run.TimerRunning && run.TimeLeft <= 30 ? ChaosUI.GoldLight : ChaosUI.Paper;
        }

        private static string Mark(bool done) => done ? "✓" : "–";
        private static string FormatTime(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private static Text Label(Transform parent, string name, int size, Color color, Vector2 min, Vector2 max,
            FontStyle style = FontStyle.Bold) =>
            ChaosUI.Label(parent, name, "", size, color, TextAnchor.MiddleLeft, style, min, max);
    }
}
