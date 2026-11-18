// Conductor Chaos — level 5 'Door het woud' (issue #246): het boslevel. Menu: Tools > Conductor Chaos > Level 5 bouwen.
// Frostbury → Fernwood dwars door een dicht loofbos (boskit #244: hoge beuken, reuzeneiken, linden, esdoorns, kastanjes):
// langs de houtzagerij (zijspoor), onder een beboste helling waar boomstammen op het spoor kunnen rollen, over een stenen
// viaduct over de beek, door een lange beukenlaan met spoorschade, langs een tweede helling met stammen, met een kapotte
// rail vóór de houten schraagbrug over het ravijn en dan omlaag naar Fernwood. Het bouwen zelf staat in CliffLevelBuilder
// (spec.forest zet de bosgrond, het dichte loofbos, varens/stammen/paddenstoelen en de rollende stammen aan).
using ConductorChaos.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ConductorChaos.EditorTools.CliffLevelBuilder;

namespace ConductorChaos.EditorTools
{
    public static class Level05Builder
    {
        const string Straight = "Track_Straight_12m", Broken = "Track_Broken_6m", Viaduct = "Track_Bridge_Viaduct_12m";
        const string Right30 = "Track_Curve_R20_30_Right", Left30 = "Track_Curve_R20_30_Left";

        public static readonly LevelSpec Spec = new LevelSpec
        {
            number = 5,
            scene = ServiceRun.FifthLevelScene,
            label = "LEVEL 5",
            title = "Door het woud",
            departureName = "Frostbury",
            arrivalName = "Fernwood",
            seed = 2026_11_18,
            forest = true,
            route = Level02Builder.Join(
                Repeat(2, Straight, "meadow"),
                new[] { Switch("Track_Switch_20m_Right", "houtzagerij", false,
                                S(Straight, "zijspoor"), S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                new[] { S(Straight, "woudA", 1.5f), S(Left30, "woudA", 2.5f), S(Straight, "woudA", 2.5f), S(Straight, "woudA", 1.5f) },
                Repeat(4, Straight, "hellingA"),
                Repeat(2, Right30, "turnA"),
                Repeat(2, Straight, "beekA"),
                Repeat(3, Viaduct, "beekbrug"),
                Repeat(2, Straight, "beekB"),
                new[] { S(Straight, "woudB", -1.5f), S(Straight, "woudB", -2.5f), S(Left30, "woudB", -1.5f) },
                new[] { Switch("Track_Switch_20m_Left", "boswissel", true, S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Repeat(2, Left30, "turnB"),
                Repeat(4, Straight, "beukenlaan"),
                new[] { S(Broken, "damage"), S(Straight, "beukenlaan") },
                Repeat(2, "Track_Ramp_Up_12m", "klim"),
                Repeat(4, Straight, "hellingB"),
                Repeat(2, Right30, "turnC"),
                Repeat(2, Straight, "ravijnA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(4, "Track_Bridge_Trestle_6m", "trestle"),
                Repeat(2, Straight, "ravijnB"),
                Repeat(2, "Track_Ramp_Down_12m", "afdaling"),
                new[] { S(Straight, "woudD", -1.5f), S("Track_Curve_R20_15_Left", "woudD"), S(Straight, "woudD") },
                Repeat(2, Straight, "meadow2")),
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "De beek", parts = new[] { Cross(new[] { "beekA" }, "beekbrug", new[] { "beekB" }, 1) },
                    tiers = 5, riverHalf = 2.4f, minHalf = 8f, maxHalf = 15f, sideFalls = 1, pillars = 0, gap = 30f,
                    armStart = new Vector3(18f, 60f, 28f), armEnd = new Vector3(18f, 60f, 28f),
                },
                new CanyonSpec
                {
                    name = "Het ravijn", parts = new[] { Cross(new[] { "ravijnA" }, "trestle", new[] { "ravijnB" }, -1) },
                    tiers = 5, riverHalf = 1.6f, minHalf = 6f, maxHalf = 14f, sideFalls = 1, pillars = 0, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Beboste helling", tags = new[] { "hellingA" }, left = false, from = 5.6f, to = 12f, above = 4.4f,
                    rockfall = "Stammen van de helling" },
                new CliffSpec { name = "Tweede helling", tags = new[] { "klim", "hellingB" }, left = true, from = 5.6f, to = 12f, above = 4.6f,
                    rockfall = "Stammen bij de tweede helling" },
                new CliffSpec { name = "Insnijding bij de afdaling", tags = new[] { "afdaling" }, left = false, from = 5.4f, to = 10f, above = 3f, stone = true },
            },
            rails = new[]
            {
                new RailSpec { tag = "bridgeRail", title = "Herstel de rail bij de schraagbrug",
                    instructions = "De rail vlak voor de houten brug over het ravijn is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein." },
            },
            damages = new[] { new DamageSpec { tag = "damage", name = "Spoorschade in de beukenlaan", chance = 0.6f } },
            switchFaults = true,
            minimumIncidents = 2,
            extraWrenches = 2,
            mesas = 5,
        };

        [MenuItem("Tools/Conductor Chaos/Level 5 openen")]
        public static void OpenSavedLevel()
        {
            const string path = "Assets/Scenes/" + ServiceRun.FifthLevelScene + ".unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/Conductor Chaos/Level 5 bouwen (bos)")]
        public static void BuildMenu() => CliffLevelBuilder.BuildMenu(Spec);

        public static void Build() => CliffLevelBuilder.Build(Spec);
    }
}
