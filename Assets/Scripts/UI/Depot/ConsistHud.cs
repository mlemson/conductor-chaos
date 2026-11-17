// Conductor Chaos — compact treinstel in de HUD (issue #239): de gekozen loc en wagons in volgorde, een moersleutel
// bij elk voertuig met een storing en een stip per crewlid op het voertuig waar hij staat. Alleen lezen: de server
// beslist over storingen; dit toont RepairPoint-status en spelersposities zoals ze lokaal binnenkomen.
// Geen toetsprompts: repareren, deuren en bediening blijven fysiek met de handen.
using System.Collections.Generic;
using ConductorChaos.Depot;
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI.Depot
{
    internal sealed class ConsistHud
    {
        private static readonly Color[] CrewColors =
        {
            ChaosUI.Hex("#3E3D70"), ChaosUI.Hex("#E59C2F"), ChaosUI.Hex("#A370D2"), ChaosUI.Hex("#8B9F3B"), ChaosUI.Hex("#C54D3E"), ChaosUI.Hex("#DAC3B2")
        };

        private static readonly Dictionary<string, string> PartNames = new()
        {
            { "Fan", "dakventilator" }, { "Vent", "dakventilator" }, { "EngineHatch", "motorluik" }, { "AxleBox", "aspot" },
            { "BrakeHose", "remslang" }, { "Aircon", "aircon" }, { "Cooler", "koeler" }, { "Pantograph", "stroomafnemer" },
            { "Converter", "omvormer" }, { "PowerCell", "energiecel" }, { "DoorLatch", "grendelkast" }, { "Valve", "afsluiter" }
        };

        private sealed class Vehicle
        {
            public Rigidbody Body;
            public string Sprite, Label;
            public RepairPoint[] Repairs;
            public Collider[] Colliders;
            public bool Locomotive;
        }

        private readonly RectTransform root;
        private readonly List<Vehicle> vehicles = new();
        private string signature;
        private float nextUpdate;

        /// <summary>Waarschuwing voor de bovenhoek, of null als alles heel is.</summary>
        public string BreakdownWarning { get; private set; }

        public ConsistHud(Transform canvas)
        {
            root = ChaosUI.Rect(canvas, "Treinstel", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(24f, 24f);
            root.sizeDelta = new Vector2(10f, 112f);
        }

        public void SetVisible(bool visible)
        {
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        public void Present(ServiceRun run)
        {
            if (!run || !run.Train) { SetVisible(false); return; }
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .25f;
            SetVisible(true);
            Collect(run);

            var crew = new List<(int vehicle, Color color, bool me)>();
            int index = 0;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
            {
                if (!player) continue;
                int on = VehicleUnder(player.transform.position);
                crew.Add((on, CrewColors[index % CrewColors.Length], player.IsOwner));
                index++;
            }

            var text = new System.Text.StringBuilder();
            string warning = null;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var vehicle = vehicles[i];
                bool broken = IsBroken(vehicle, out string part);
                if (vehicle.Locomotive && run.SteamWarning != null) broken = true;
                text.Append(vehicle.Sprite).Append(broken ? '!' : '.');
                if (broken && warning == null && part != null)
                    warning = $"Storing: {part} {(vehicle.Locomotive ? "op de loc" : $"op wagon {vehicle.Label}")}. Tik met de moersleutel op de gele bout.";
            }
            foreach (var (vehicle, _, me) in crew) text.Append(vehicle).Append(me ? 'm' : 'p');
            BreakdownWarning = warning;
            string next = text.ToString();
            if (next == signature) return;
            signature = next;
            Rebuild(run, crew);
        }

        private void Collect(ServiceRun run)
        {
            var train = run.Train;
            var wagons = train.CoupledWagons;
            bool same = vehicles.Count > 0 && vehicles[0].Body == train.LocomotiveBody;
            int expected = 1;
            foreach (var wagon in wagons) if (wagon && !RepairPoint.FindDeep(wagon.transform, "CoalStock")) expected++;
            if (same && vehicles.Count == expected) return;

            vehicles.Clear();
            var loco = train.LocomotiveBody;
            var trainInfo = DepotCatalog.Train(DepotVehicles.AppliesToScene(run.gameObject.scene.name) ? DepotLoadout.Active.Train : TrainKind.Steam);
            var lead = new Vehicle { Body = loco, Sprite = trainInfo.Sprite, Label = "Loc", Locomotive = true };
            Fill(lead, loco ? loco.gameObject : train.gameObject);
            // De kolenwagen hoort bij de stoomloc en telt mee voor storingen en spelers op de loc.
            foreach (var wagon in wagons)
                if (wagon && RepairPoint.FindDeep(wagon.transform, "CoalStock"))
                {
                    var extra = new List<Collider>(lead.Colliders);
                    extra.AddRange(wagon.GetComponentsInChildren<Collider>());
                    lead.Colliders = extra.ToArray();
                }
            int number = 1;
            var list = new List<Vehicle>();
            foreach (var wagon in wagons)
            {
                if (!wagon || RepairPoint.FindDeep(wagon.transform, "CoalStock")) continue;
                string sprite = "regional";
                if (wagon.TryGetComponent<ServiceFreightWagon>(out var freight)) sprite = DepotCatalog.Wagon(freight.Kind).Sprite;
                var vehicle = new Vehicle { Body = wagon, Sprite = sprite, Label = (number++).ToString() };
                Fill(vehicle, wagon.gameObject);
                list.Add(vehicle);
            }
            vehicles.Add(lead);
            vehicles.AddRange(list);
            signature = null;
        }

        private static void Fill(Vehicle vehicle, GameObject go)
        {
            vehicle.Repairs = go.GetComponentsInChildren<RepairPoint>(true);
            vehicle.Colliders = go.GetComponentsInChildren<Collider>();
        }

        private static bool IsBroken(Vehicle vehicle, out string part)
        {
            part = null;
            if (vehicle.Repairs == null) return false;
            foreach (var repair in vehicle.Repairs)
            {
                if (!repair || !repair.IsSpawned || !repair.IsBroken) continue;
                string key = repair.name.StartsWith("RepairPoint_", System.StringComparison.Ordinal) ? repair.name.Substring(12) : repair.name;
                part = PartNames.TryGetValue(key, out string label) ? label : key.ToLowerInvariant();
                return true;
            }
            return false;
        }

        private int VehicleUnder(Vector3 position)
        {
            for (int i = 0; i < vehicles.Count; i++)
                foreach (var collider in vehicles[i].Colliders)
                {
                    if (!collider || !collider.enabled) continue;
                    var bounds = collider.bounds;
                    bounds.Expand(new Vector3(.4f, 1.6f, .4f));
                    if (bounds.Contains(position)) return i;
                }
            return -1;
        }

        private void Rebuild(ServiceRun run, List<(int vehicle, Color color, bool me)> crew)
        {
            DepotUi.Clear(root);
            float x = 14f, height = 40f;
            var widths = new float[vehicles.Count];
            float total = 0f;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var sprite = Resources.Load<Sprite>("Depot/" + vehicles[i].Sprite);
                widths[i] = sprite ? Mathf.Clamp(height * sprite.rect.width / sprite.rect.height, 60f, 140f) : 90f;
                total += widths[i] + 14f;
            }
            float panelWidth = Mathf.Max(220f, total + 20f);
            root.sizeDelta = new Vector2(panelWidth, 116f);
            var panel = ChaosUI.Panel(root, "Paneel", Vector2.zero, Vector2.one, new Color(DepotUi.PanelFace.r, DepotUi.PanelFace.g, DepotUi.PanelFace.b, .94f), 2, DepotUi.Edge);
            DepotUi.Icon(panel, "train", 12, 8, 24);
            DepotUi.Txt(panel, "Treinstel · rijrichting →", 40, 6, panelWidth - 50, 26, 13, ChaosUI.Muted);

            // Achterste wagon links, loc rechts: zoals de trein door het beeld rijdt.
            for (int order = vehicles.Count - 1; order >= 0; order--)
            {
                var vehicle = vehicles[order];
                bool broken = IsBroken(vehicle, out _) || (vehicle.Locomotive && run.SteamWarning != null);
                float w = widths[order] + 10f;
                Color frame = broken ? ChaosUI.TrainRed : vehicle.Locomotive ? ChaosUI.Gold : DepotUi.Edge;
                var box = DepotUi.Panel(panel, vehicle.Label, x, 52, w, height + 8, broken ? ChaosUI.Hex("#4A2622") : DepotUi.Background, frame);
                DepotUi.Vehicle(box, vehicle.Sprite, 5, 4, w - 10, height);
                if (broken)
                {
                    var badge = DepotUi.Panel(box, "Storing", w - 18, -12, 28, 28, ChaosUI.TrainRed, ChaosUI.TrainRedDark);
                    DepotUi.Icon(badge, "wrench", 4, 4, 20);
                }
                DepotUi.Txt(panel, vehicle.Label, x, 90, w, 22, 13, ChaosUI.Paper, true, TextAnchor.MiddleCenter);
                float dx = x + 4;
                foreach (var (on, color, me) in crew)
                {
                    if (on != order) continue;
                    var dot = DepotUi.Panel(panel, me ? "Jij" : "Crewlid", dx, 34, 16, 16, me ? ChaosUI.Gold : ChaosUI.Paper);
                    DepotUi.Panel(dot, "Kleur", 2, 2, 12, 12, color);
                    dx += 20;
                }
                x += w + 4;
            }
        }
    }
}
