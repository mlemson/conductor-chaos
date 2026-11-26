// Conductor Chaos — woestijnthema voor de kloof-levels (levels 6 en 7, concept: Docs/References/Desert/).
// Aanvulling op CliffLevelBuilder (spec.desert): zandgrond, gelaagde rode kliffen (CliffMass.desert), cactussen en
// droge planten, tafelbergen in de verte, en de woestijnincidenten:
//   * HerdSpec  een kudde bizons (volwassen + kalveren) wacht verborgen onder de grond en stormt, getimed op de trein,
//               in galop met een stofwolk over het spoor; te hard aankomen = dieren die op de rails blijven staan
//               (ServiceIncidents.Herd + PlaygroundAnimal.ServerEmerge/ServerCross/ServerHold).
//   * DropSpec  tumbleweeds in de zandstorm of een losgeslagen mijnkarretje belanden op het spoor (zelfde mechaniek als
//               een steenlawine: kinematisch klaar naast het spoor, de server gooit ze bij de trigger).
// Decor: rotsboog over het spoor, mijningang aan het eind van het mijnspoor, windmolens, hoodoos, schedels, vlaggen,
// bizonbord voor de oversteek en gele bochtborden met pijlen bij scherpe bochten. Alles statisch, behalve de dieren en
// de obstakels (server-autoritatief via NetworkObject/NetworkTransform/NetworkRigidbody).
using System;
using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Animals;
using ConductorChaos.Art;
using ConductorChaos.Nature;
using ConductorChaos.Stations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConductorChaos.EditorTools
{
    public static partial class CliffLevelBuilder
    {
        const string DesertRoot = "Woestijndecor";
        const string AnimalPrefabDir = "Assets/Art/Prefabs/Animals";

        public sealed class HerdSpec
        {
            public string tag, name = "Overstekende bizons";
            public int side = 1;                 // +1 = de kudde komt van rechts (in rijrichting), -1 = van links
            public int adults = 5, calves = 2, stubborn = 2;
            public float distance = 26f;         // waar de kudde opduikt, vanaf de hartlijn
            public float approach = 170f;        // naderingszone: vanaf hier ligt de kudde op de loer
            public float chance = 0.85f;
            public const float StampedeSpeed = 7f;
        }

        public sealed class DropSpec
        {
            public string tag, name, warning, model;     // model relatief aan Assets/Art/Models/Environment, zonder .fbx
            public int count = 3, side = 1;
            public float distance = 8f, raise, spacing = 3.2f, scale = 1f, mass = 3f;
            public float linearDamping = 0.15f, angularDamping = 1.2f;
            public float triggerAhead = 60f, chance = 0.8f, along;
            public bool platform;                        // houten laadperron onder het obstakel (mijnkarretje)
        }

        sealed partial class LevelContext
        {
            readonly List<(Vector3 from, Vector3 to)> herdPaths = new List<(Vector3, Vector3)>();

            // ------------------------------------------------------------ grond en planten
            void DesertSlopes(StylizedTerrainCliffs slopes)
            {
                var rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Terrain/Layers/TL_Desert_Rock.terrainlayer");
                if (!slopes || !rock) return;
                slopes.slopeLayer = rock;                   // steile terreinhellingen: gelaagde rode rots i.p.v. aarde
                slopes.Rebuild();
            }

            // lagen: 0 zand, 1 licht zand (duinvlakken), 2 rode grond; 3 kiezels en 4 rots zet de gewone code (kloof/stations)
            void DesertGround(float wx, float wz, float d, float f, float ox, float oz, out float sand, out float dunes, out float red)
            {
                sand = 1f;
                float n = Noise(wx, wz, 38f, ox + 211f, oz + 97f) * 0.8f + f * 0.2f;
                dunes = Smooth(0.52f, 0.72f, n) * 1.4f * Smooth(6f, 20f, d);
                red = Smooth(0.6f, 0.8f, Noise(wx, wz, 16f, ox + 600f, oz + 40f)) * 0.85f;
            }

            float DesertPlantDensity(Vector3 p, float d, float ox, float oz)
            {
                float n = Noise(p.x, p.z, 40f, ox, oz) * 0.8f + Noise(p.x, p.z, 13f, ox + 40f, oz + 9f) * 0.35f;
                float density = Mathf.Clamp01((n - 0.40f) * 2.2f) * (0.3f + 0.7f * Smooth(5f, 26f, d));
                if (NearStation(p, 14f)) density *= 0.4f;
                return density * 0.8f;
            }

            // Cactussen en droge struiken; de vrije afstand houdt ook de armen van een saguaro naast de trein.
            (GameObject model, float weight, float clearance)[] DesertTrees() => new (GameObject model, float weight, float clearance)[]
            {
                (Model("Desert", "Cactus_Saguaro_01"), 1.0f, 4.4f),
                (Model("Desert", "Cactus_Saguaro_02"), 0.55f, 4.8f),
                (Model("Desert", "Cactus_Saguaro_Small_01"), 0.9f, 3.4f),
                (Model("Desert", "Desert_Scrub_01"), 1.0f, 3.0f),
                (Model("Desert", "Cactus_Prickly_01"), 0.7f, 3.0f),
                (Model("Desert", "Cactus_Barrel_01"), 0.5f, 2.6f),
            };

            // ------------------------------------------------------------ kudde
            ServiceIncidents.Herd MakeHerd(Transform parent, HerdSpec h)
            {
                var section = route.Where(r => r.tag == h.tag).ToList();
                if (section.Count == 0) throw new Exception($"Kudde mist spoorsectie {h.tag}.");
                var adultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{AnimalPrefabDir}/Bison.prefab");
                var calfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{AnimalPrefabDir}/Bison_Calf.prefab");
                if (!adultPrefab || !calfPrefab)
                    throw new Exception("Bison-prefabs ontbreken: Conductor Chaos > Art > Animals > Build Animal Prefabs.");
                float mid = (section[0].arcStart + section[section.Count - 1].arcEnd) * 0.5f;
                var centre = SampleAt(mid);
                var group = new GameObject(h.name).transform;
                group.SetParent(parent, false);
                var site = new GameObject("Plek op het spoor").transform;
                site.SetParent(group, false);
                site.SetPositionAndRotation(centre.rail, Quaternion.LookRotation(Flat(centre.dir)));
                var animalsRoot = Root("Dieren · kuddes").transform;          // dieren niet onder het NetworkObject van de incidenten

                // kant en afstand kiezen waar alle dieren op vlakke grond opduiken en uitkomen (niet in een kloof of op een helling)
                int n = h.adults + h.calves;
                var alongs = Enumerable.Range(0, n).Select(k => (k - (n - 1) * 0.5f) * 2.6f + ((float)rng.NextDouble() - 0.5f) * 1.2f).ToArray();
                var jitter = Enumerable.Range(0, n).Select(_ => ((float)rng.NextDouble(), (float)rng.NextDouble())).ToArray();
                int side = h.side;
                float reach = h.distance;
                bool Good(int sd, float r) => alongs.Select((a, k) =>
                {
                    var s = SampleAt(mid + a);
                    var across = Vector3.Cross(Vector3.up, Flat(s.dir)).normalized * sd;
                    var p0 = s.rail + across * (r + jitter[k].Item1 * 4f);
                    var p1 = s.rail - across * (r * 0.75f + jitter[k].Item2 * 5f);
                    p0.y = GroundAt(p0);
                    p1.y = GroundAt(p1);
                    return !InCanyon(p0, 2f) && !InCanyon(p1, 2f) && SteepnessAt(p0) < 20f && SteepnessAt(p1) < 20f &&
                           !CliffTop(p0, out _, out _) && !CliffTop(p1, out _, out _) && Mathf.Abs(p0.y - s.Pivot) < 3.5f && Mathf.Abs(p1.y - s.Pivot) < 3.5f;
                }).All(ok => ok);
                var options = new[] { (side, reach), (side, reach * 0.75f), (-side, reach), (-side, reach * 0.75f), (side, reach * 0.55f), (-side, reach * 0.55f) };
                var choice = options.FirstOrDefault(o => Good(o.Item1, o.Item2));
                if (choice.Item2 <= 0f)
                {
                    choice = (side, reach * 0.55f);
                    Debug.LogWarning($"[Level {spec.number}] {h.name}: geen vlakke plek voor de hele kudde gevonden.");
                }
                (side, reach) = choice;

                var animals = new List<PlaygroundAnimal>();
                var starts = new List<Transform>();
                var crossing = new List<Transform>();
                var destination = new List<Transform>();
                float sprint = 0f;
                for (int k = 0; k < n; k++)
                {
                    bool calf = k >= h.adults;
                    var s = SampleAt(mid + alongs[k]);
                    var across = Vector3.Cross(Vector3.up, Flat(s.dir)).normalized * side;
                    var start = s.rail + across * (reach + jitter[k].Item1 * 4f);
                    var end = s.rail - across * (reach * 0.75f + jitter[k].Item2 * 5f);
                    start.y = GroundAt(start);
                    end.y = GroundAt(end);
                    sprint = Mathf.Max(sprint, Vector3.ProjectOnPlane(start - s.rail, Vector3.up).magnitude);
                    // wacht kinematisch recht onder de opduikplek (onzichtbaar); ServiceIncidents haalt hem op tijd boven
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(calf ? calfPrefab : adultPrefab, scene);
                    go.transform.SetParent(animalsRoot, true);
                    go.transform.SetPositionAndRotation(start + Vector3.down * 14f, Quaternion.LookRotation(-across));
                    go.GetComponent<Rigidbody>().isKinematic = true;
                    MarkHidden(go.GetComponent<PlaygroundAnimal>());
                    go.name = $"{h.name} · {(calf ? "kalf" : "bizon")} {k + 1}";
                    var animal = go.GetComponent<PlaygroundAnimal>();
                    var so = new SerializedObject(animal);
                    so.FindProperty("roamRadius").floatValue = 2.5f;
                    so.FindProperty("stampedeSpeed").floatValue = calf ? HerdSpec.StampedeSpeed * 0.92f : HerdSpec.StampedeSpeed;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    animals.Add(animal);
                    Transform Marker(string label, Vector3 at)
                    {
                        var t = new GameObject($"{label} {k + 1}").transform;
                        t.SetParent(group, false);
                        t.position = at;
                        return t;
                    }
                    starts.Add(Marker("Opduiken", start + Vector3.up * 0.05f));
                    crossing.Add(Marker("Oversteek", s.rail));
                    destination.Add(Marker("Overkant", end));
                    herdPaths.Add((start, end));
                }
                // bizonbord ruim vóór de oversteek, rechts van het spoor, naar de trein gekeerd
                var sign = SampleAt(section[0].arcStart - 48f);
                if (sign != null) PlaceSign("Sign_BisonCrossing_01", sign, 3.6f, false, "Bizonbord · " + h.name.ToLowerInvariant());
                return new ServiceIncidents.Herd
                {
                    name = h.name,
                    trigger = Trigger(group, "Nadering " + h.name.ToLowerInvariant(), mid - h.approach * 0.5f - 8f, h.approach),
                    start = starts.ToArray(),
                    runSeconds = sprint / HerdSpec.StampedeSpeed + 1.2f,
                    animals = animals.ToArray(),
                    crossing = crossing.ToArray(),
                    destination = destination.ToArray(),
                    stubborn = h.stubborn,
                    site = site,
                    chance = h.chance,
                };
            }

            static void MarkHidden(PlaygroundAnimal animal)
            {
                var so = new SerializedObject(animal);
                so.FindProperty("waitHidden").boolValue = true;     // blijft kinematisch onder de grond tot ServerEmerge
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ------------------------------------------------------------ tumbleweeds / mijnkarretje
            ServiceIncidents.Rockfall MakeDrop(Transform parent, DropSpec d)
            {
                var section = route.Where(r => r.tag == d.tag).ToList();
                if (section.Count == 0) throw new Exception($"Obstakel {d.name} mist spoorsectie {d.tag}.");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{d.model}.fbx");
                if (!model) throw new Exception($"{EnvDir}/{d.model}.fbx ontbreekt.");
                float mid = Mathf.Lerp(section[0].arcStart, section[section.Count - 1].arcEnd, 0.5f + d.along);
                var group = new GameObject(d.name).transform;
                group.SetParent(parent, false);
                var rockRoot = Root("Rotsblokken · steenlawines").transform;
                var centre = SampleAt(mid);
                var siteMarker = new GameObject("Plek op het spoor").transform;
                siteMarker.SetParent(group, false);
                siteMarker.SetPositionAndRotation(centre.rail, Quaternion.LookRotation(Flat(centre.dir)));
                var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/M_Env_Timber.mat");
                var rocks = new List<Rigidbody>();
                var landing = new List<Transform>();
                for (int k = 0; k < d.count; k++)
                {
                    var s = SampleAt(mid + (k - (d.count - 1) * 0.5f) * d.spacing);
                    var side = Vector3.Cross(Vector3.up, Flat(s.dir)).normalized * d.side;
                    var perch = s.rail + side * d.distance;
                    float ground = GroundAt(perch);
                    if (d.platform && d.raise > 0.2f)
                        CampaignLevelBake.Box(Root(DesertRoot).transform,
                            $"Laadperron · {d.name.ToLowerInvariant()}", new Vector3(perch.x, ground + d.raise * 0.5f - 0.3f, perch.z),
                            Quaternion.LookRotation(Flat(s.dir)), new Vector3(2.6f, d.raise + 0.6f, 3.2f), timber, true);
                    var body = MakeRock(rockRoot, $"{d.name} · {k + 1}", model, new Vector3(perch.x, ground + d.raise, perch.z), d.scale);
                    if (d.platform) body.transform.rotation = Quaternion.LookRotation(Flat(s.dir));
                    body.mass = d.mass;
                    body.linearDamping = d.linearDamping;
                    body.angularDamping = d.angularDamping;
                    body.gameObject.AddComponent<LowTrackObstacle>();
                    rocks.Add(body);
                    var land = new GameObject($"Landing {k + 1}").transform;
                    land.SetParent(group, false);
                    land.position = s.rail + side * ((float)rng.NextDouble() * 0.6f - 0.3f) + Vector3.up * 0.35f;
                    landing.Add(land);
                }
                return new ServiceIncidents.Rockfall
                {
                    name = d.name,
                    trigger = Trigger(group, "Trigger " + d.name.ToLowerInvariant(), mid - d.triggerAhead),
                    rocks = rocks.ToArray(),
                    landing = landing.ToArray(),
                    site = siteMarker,
                    chance = d.chance,
                    warning = d.warning,
                };
            }

            // ------------------------------------------------------------ decor
            void DressDesert()
            {
                var root = Root(DesertRoot).transform;
                int arch = PlaceArch(root), mine = PlaceMine(root), mills = PlaceWindmills(root), chevrons = PlaceChevrons(root);
                int hoodoos = Scatter(root, new[] { Model("Desert", "Hoodoo_01"), Model("Desert", "Hoodoo_02") }, 12, 24f, 110f, 14f, 0.75f, 1.35f, true, "Hoodoo");
                int skulls = Scatter(root, new[] { Model("Desert", "Skull_Bull_01") }, 10, 4.5f, 16f, 10f, 0.9f, 1.2f, false, "Koeienschedel");
                int weeds = Scatter(root, new[] { Model("Desert", "Tumbleweed_01") }, 14, 5f, 30f, 12f, 0.7f, 1.1f, false, "Tumbleweed (decor)");
                int flags = PlaceFlags(root);
                bool bison = ReplaceBlockerCow();
                if (spec.sandstorm)
                {
                    var storm = new GameObject("Zandstorm (cosmetisch, lokaal)");
                    storm.transform.SetParent(root, false);
                    storm.AddComponent<Sandstorm>();
                }
                int cleared = 0;
                foreach (var (from, to) in herdPaths) cleared += ClearAlong(from, to, 2.4f);
                // zachte paarse waas in de kloven, zoals in de conceptbladen (palette: haze_purple)
                if (RenderSettings.fog) RenderSettings.fogColor = new Color(0.82f, 0.74f, 0.80f);
                log.AppendLine($"Woestijn: {arch} rotsboog, {mine} mijningang, {mills} windmolens, {chevrons} bochtborden, {hoodoos} hoodoos, " +
                               $"{skulls} schedels, {weeds} tumbleweeds, {flags} vlaggen, {cleared} planten uit de kuddepaden" +
                               (bison ? ", koppige bizon i.p.v. de koe op het spoor" : "") + (spec.sandstorm ? ", zandstorm aan." : "."));
            }

            // Level 1 zet een koppige koe op het spoor (met hooibaal ernaast); in de woestijn staat daar een koppige bizon.
            bool ReplaceBlockerCow()
            {
                var cow = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Cow_Blocker");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{AnimalPrefabDir}/Bison.prefab");
                if (!cow || !prefab) return false;
                var bison = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                bison.transform.SetPositionAndRotation(cow.transform.position, cow.transform.rotation);
                bison.name = "Bison_Blocker";
                var so = new SerializedObject(bison.GetComponent<PlaygroundAnimal>());
                so.FindProperty("temperament").enumValueIndex = (int)PlaygroundAnimal.Temperament.Blocker;
                so.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(cow);
                return true;
            }

            GameObject PlaceSign(string model, Sample s, float offset, bool left, string name)
            {
                var forward = Flat(s.dir);
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                var side = left ? -right : right;
                if (InCanyon(s.rail + side * offset, 0.5f)) side = -side;
                var p = s.rail + side * offset;
                p.y = GroundAt(p);
                var go = Place(Model("Desert", model), "Props", p - Vector3.up * 0.05f, Quaternion.LookRotation(-forward).eulerAngles.y, 1f, 1f, -0.05f, true);
                if (go) go.name = name;
                return go;
            }

            int PlaceArch(Transform root)
            {
                if (string.IsNullOrEmpty(spec.archTag)) return 0;
                var section = route.Where(r => r.tag == spec.archTag).ToList();
                if (section.Count == 0) throw new Exception($"Rotsboog mist spoorsectie {spec.archTag}.");
                var s = SampleAt((section[0].arcStart + section[section.Count - 1].arcEnd) * 0.5f);
                var go = Place(Model("Desert", "Rock_Arch_01"), "Props", new Vector3(s.rail.x, s.Pivot - 0.25f, s.rail.z),
                    Quaternion.LookRotation(Flat(s.dir)).eulerAngles.y, 1f, 1f, -0.25f, true);
                if (!go) return 0;
                go.name = "Rotsboog over het spoor";
                go.transform.SetParent(root, true);
                go.GetComponent<PaintedProp>().followGround = false;
                ClearAround(s.rail, 12f);
                return 1;
            }

            int PlaceMine(Transform root)
            {
                if (string.IsNullOrEmpty(spec.mineTag)) return 0;
                var buffer = branches.FirstOrDefault(b => b.tag == spec.mineTag && b.piece.name.Contains("BufferStop"));
                if (buffer == null) throw new Exception($"Mijnspoor {spec.mineTag} heeft geen stootblok.");
                var dir = Flat(buffer.piece.EndDirection);
                var end = buffer.piece.EndPosition;
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                float ground = end.y - RailTop;
                var entrance = Place(Model("Desert", "Mine_Entrance_01"), "Props", new Vector3(end.x, ground - 0.2f, end.z) + dir * 0.9f,
                    Quaternion.LookRotation(-dir).eulerAngles.y, 1f, 1f, -0.2f, true);
                if (!entrance) return 0;
                entrance.name = "Goudmijn · ingang";
                entrance.transform.SetParent(root, true);
                entrance.GetComponent<PaintedProp>().followGround = false;
                ClearAround(end, 10f);
                var crane = Place(Model("Desert", "Mine_Crane_01"), "Props", Ground(end - dir * 7f + right * 4.6f),
                    Quaternion.LookRotation(-right).eulerAngles.y, 1f, 1f, 0f, true);
                if (crane) { crane.name = "Goudmijn · takel"; crane.transform.SetParent(root, true); }
                var cart = Place(Model("Desert", "MineCart_Gold_01"), "Props", new Vector3(end.x, ground + 0.43f, end.z) - dir * 2.6f,
                    Quaternion.LookRotation(dir).eulerAngles.y, 1f, 1f, 0.43f, true);
                if (cart) { cart.name = "Goudmijn · vol karretje"; cart.transform.SetParent(root, true); cart.GetComponent<PaintedProp>().followGround = false; }
                foreach (float side in new[] { -1f, 1f })
                {
                    var flag = Place(Model("Desert", "Flag_Pennant_01"), "Props", Ground(end - dir * 1.5f + right * side * 5.2f),
                        (float)rng.NextDouble() * 360f, 1f, 1f, 0f, true);
                    if (flag) flag.transform.SetParent(root, true);
                }
                return 1;
            }

            Vector3 Ground(Vector3 p) { p.y = GroundAt(p); return p; }

            int PlaceWindmills(Transform root)
            {
                var model = Model("Desert", "Windmill_01");
                if (!model || spec.windmills <= 0) return 0;
                var main = samples.Where(s => s.arc >= 0f).ToList();
                int made = 0;
                for (int attempt = 0; attempt < 900 && made < spec.windmills; attempt++)
                {
                    // de eerste twee bij de stations, de rest langs de route
                    var s = made == 0 ? main[Mathf.Min(main.Count - 1, 30)] : made == 1 ? main[Mathf.Max(0, main.Count - 30)]
                        : main[rng.Next(main.Count)];
                    var side = Vector3.Cross(Vector3.up, Flat(s.dir)).normalized * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    var p = s.rail + side * (16f + (float)rng.NextDouble() * 20f) + Flat(s.dir) * ((float)rng.NextDouble() * 20f - 10f);
                    p.y = GroundAt(p);
                    if (far.Distance(p.x, p.z) < 12f || NearStation(p, 7f) || InCanyon(p, 6f) || SteepnessAt(p) > 9f ||
                        CliffTop(p, out _, out _) || NearCliff(p, 3f)) continue;
                    var go = Place(model, "Props", p - Vector3.up * 0.2f, (float)rng.NextDouble() * 360f, 1f, 1f, -0.2f, true);
                    if (!go) continue;
                    go.name = "Windmolen";
                    go.transform.SetParent(root, true);
                    go.AddComponent<WindmillRotor>();
                    ClearAround(p, 3.5f);
                    made++;
                }
                return made;
            }

            int PlaceChevrons(Transform root)
            {
                var model = Model("Desert", "Sign_Chevron_01");
                if (!model) return 0;
                int made = 0;
                for (int i = 0; i < route.Count; i++)
                {
                    float turn = route[i].piece.TurnDegrees;
                    if (Mathf.Abs(turn) < 25f || (i > 0 && Mathf.Abs(route[i - 1].piece.TurnDegrees) >= 25f)) continue;
                    int j = i;
                    while (j + 1 < route.Count && Mathf.Abs(route[j + 1].piece.TurnDegrees) >= 25f) j++;
                    float a0 = route[i].arcStart, a1 = route[j].arcEnd;
                    bool rightTurn = turn > 0f;
                    foreach (float t in new[] { -0.25f, 0.2f, 0.55f })
                    {
                        var s = SampleAt(t < 0f ? a0 - 12f : Mathf.Lerp(a0, a1, t));
                        if (s == null || s.bridge) continue;
                        var forward = Flat(s.dir);
                        var outside = Vector3.Cross(Vector3.up, forward).normalized * (rightTurn ? -1f : 1f);
                        var p = s.rail + outside * 3.9f;
                        if (InCanyon(p, 0.3f) || NearStation(p, 3f)) continue;
                        p.y = GroundAt(p);
                        var go = Place(model, "Props", p - Vector3.up * 0.05f, Quaternion.LookRotation(-forward).eulerAngles.y, 1f, 1f, -0.05f, true);
                        if (!go) continue;
                        // de pijlen wijzen in het model naar rechts (gezien vanuit de trein); spiegelen voor een bocht naar links
                        if (!rightTurn) go.transform.localScale = new Vector3(-1f, 1f, 1f);
                        go.name = rightTurn ? "Bochtbord · rechts" : "Bochtbord · links";
                        go.transform.SetParent(root, true);
                        made++;
                    }
                    i = j;
                }
                return made;
            }

            int PlaceFlags(Transform root)
            {
                var model = Model("Desert", "Flag_Pennant_01");
                if (!model) return 0;
                int made = 0;
                for (int attempt = 0; attempt < 600 && made < 8; attempt++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    if (d < 14f || d > 70f || !CliffTop(p, out var top, out var normal) || normal.y < 0.92f) continue;
                    var go = Place(model, "Props", new Vector3(p.x, top - 0.1f, p.z), (float)rng.NextDouble() * 360f, 1f, 1f, -0.1f, false);
                    if (!go) continue;
                    go.GetComponent<PaintedProp>().followGround = false;
                    go.transform.SetParent(root, true);
                    made++;
                }
                return made;
            }

            int Scatter(Transform root, GameObject[] models, int count, float near, float farthest, float maxSlope, float minScale,
                float maxScale, bool collider, string name)
            {
                models = models.Where(m => m).ToArray();
                if (models.Length == 0) return 0;
                int made = 0;
                for (int attempt = 0; attempt < count * 80 && made < count; attempt++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    if (d < near || d > farthest || NearStation(p, 6f) || InCanyon(p, 3f)) continue;
                    p.y = GroundAt(p);
                    if (SteepnessAt(p) > maxSlope || CliffTop(p, out var overhead, out _) && overhead > p.y + 0.3f) continue;
                    float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                    var go = Place(models[rng.Next(models.Length)], "Props", p - Vector3.up * 0.08f, (float)rng.NextDouble() * 360f, scale,
                        1f, -0.08f, collider);
                    if (!go) continue;
                    go.name = name;
                    go.transform.SetParent(root, true);
                    if (collider) ClearAround(p, 2.5f * scale);
                    made++;
                }
                return made;
            }

            // Natuur die al geplant is weghalen (rotsboog, mijningang, windmolen, kuddepad).
            int ClearAround(Vector3 p, float radius) => ClearWhere(q => Vector3.ProjectOnPlane(q - p, Vector3.up).sqrMagnitude < radius * radius);

            int ClearAlong(Vector3 a, Vector3 b, float width) => ClearWhere(q =>
            {
                Vector3 ab = Vector3.ProjectOnPlane(b - a, Vector3.up), aq = Vector3.ProjectOnPlane(q - a, Vector3.up);
                float t = Mathf.Clamp01(Vector3.Dot(aq, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                return (aq - ab * t).sqrMagnitude < width * width;
            });

            int ClearWhere(Func<Vector3, bool> inside)
            {
                if (!propsRoot) return 0;
                int n = 0;
                foreach (string category in new[] { "Trees", "Rocks", "Plants" })
                {
                    var group = propsRoot.Find(category);
                    if (!group) continue;
                    foreach (var item in group.Cast<Transform>().ToArray())
                        if (inside(item.position)) { Object.DestroyImmediate(item.gameObject); n++; }
                }
                return n;
            }
        }
    }
}
