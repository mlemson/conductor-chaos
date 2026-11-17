// Conductor Chaos — storingen aan de gekozen trein (issue #239). Geen zichtbare treinstrook meer in de HUD: tijdens
// het spelen alleen één korte melding rechtsboven als er een onderhoudspunt kapot is. Alleen lezen: de server
// beslist over storingen; dit leest de RepairPoint-status zoals die lokaal binnenkomt.
// Geen toetsprompts: repareren blijft fysiek met de moersleutel.
using System.Collections.Generic;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using UnityEngine;

namespace ConductorChaos.UI.Depot
{
    internal static class BreakdownNotice
    {
        private static readonly Dictionary<string, string> PartNames = new()
        {
            { "Fan", "Dakventilator" }, { "Vent", "Dakventilator" }, { "EngineHatch", "Motorluik" }, { "AxleBox", "Aspot" },
            { "BrakeHose", "Remslang" }, { "Aircon", "Aircon" }, { "Cooler", "Koeler" }, { "Pantograph", "Stroomafnemer" },
            { "Converter", "Omvormer" }, { "PowerCell", "Energiecel" }, { "DoorLatch", "Grendelkast" }, { "Valve", "Afsluiter" }
        };

        private static readonly List<RepairPoint> points = new();
        private static readonly List<int> owners = new();
        private static ServiceRun cachedRun;
        private static int cachedCount = -1;
        private static float nextCheck;
        private static string warning;

        /// <summary>Korte melding voor de bovenhoek, of null als alles heel is.</summary>
        public static string For(ServiceRun run)
        {
            if (!run || !run.Train) return null;
            if (Time.unscaledTime < nextCheck && run == cachedRun) return warning;
            nextCheck = Time.unscaledTime + .25f;
            Collect(run);
            warning = null;
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!point || !point.IsSpawned || !point.IsBroken) continue;
                string key = point.name.StartsWith("RepairPoint_", System.StringComparison.Ordinal) ? point.name.Substring(12) : point.name;
                string part = PartNames.TryGetValue(key, out string label) ? label : key;
                warning = owners[i] == 0 ? $"{part} van de loc kapot: tik op de gele bout" : $"{part} van wagon {owners[i]} kapot: tik op de gele bout";
                break;
            }
            return warning;
        }

        private static void Collect(ServiceRun run)
        {
            var train = run.Train;
            int count = train.CoupledWagons.Count;
            if (run == cachedRun && count == cachedCount) return;
            cachedRun = run;
            cachedCount = count;
            points.Clear();
            owners.Clear();
            Add(train.LocomotiveBody ? train.LocomotiveBody.gameObject : train.gameObject, 0);
            int number = 1;
            foreach (var wagon in train.CoupledWagons)
            {
                if (!wagon) continue;
                // De kolenwagen hoort bij de stoomloc.
                bool tender = RepairPoint.FindDeep(wagon.transform, "CoalStock");
                Add(wagon.gameObject, tender ? 0 : number);
                if (!tender) number++;
            }
        }

        private static void Add(GameObject go, int owner)
        {
            foreach (var point in go.GetComponentsInChildren<RepairPoint>(true))
            {
                if (points.Contains(point)) continue;
                points.Add(point);
                owners.Add(owner);
            }
        }
    }
}
