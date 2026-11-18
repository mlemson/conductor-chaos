// Conductor Chaos — level 3 'Over de hoogvlakte' (issue #228). Menu: Tools > Conductor Chaos > Level 3 bouwen.
// Craggleton → Eaglecrest: via viaducten en een bergkam naar een groen dal en een laatste klim naar de sneeuw.
using ConductorChaos.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ConductorChaos.EditorTools.CliffLevelBuilder;

namespace ConductorChaos.EditorTools
{
    public static class Level03Builder
    {
        const string Straight = "Track_Straight_12m", Broken = "Track_Broken_6m", Viaduct = "Track_Bridge_Viaduct_12m";
        const string Right30 = "Track_Curve_R20_30_Right", Left30 = "Track_Curve_R20_30_Left";
        const string Right15 = "Track_Curve_R20_15_Right", Left15 = "Track_Curve_R20_15_Left";

        static Step[] Slope(string tag, int straights, float grade) => Level02Builder.Join(
            new[] { S(Straight, tag, grade * .5f) }, Repeat(straights, Straight, tag, grade),
            new[] { S(Straight, tag, grade * .5f) });

        public static readonly LevelSpec Spec = new LevelSpec
        {
            number = 3,
            scene = ServiceRun.ThirdLevelScene,
            label = "LEVEL 3",
            title = "Over de hoogvlakte",
            departureName = "Craggleton",
            arrivalName = "Eaglecrest",
            seed = 2026_11_16,
            cliffRing = false,
            mountains = new[]
            {
                new MountainSpec("climb1", 105f, 180f, 122f, 56f),
                new MountainSpec("ridge", 100f, 213f, 138f, 82f),
                new MountainSpec("turnC", 99f, 182f, 113f, 65f),
                new MountainSpec("descent", -105f, 160f, 108f, 46f),
                new MountainSpec("winterClimb", -91f, 211f, 140f, 67f),
                new MountainSpec("meadow2", -102f, 109f, 118f, 53f)
            },
            route = Level02Builder.Join(
                Repeat(2, Straight, "meadow"),
                new[] { Switch("Track_Switch_20m_Left", "goederenwissel", false,
                                S(Straight, "zijspoor"), S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                new[] { S(Straight, "forest", 1.5f), S(Straight, "forest", 2.5f), S(Straight, "forest", 2.5f),
                        S(Right30, "forest", 2.5f), S(Straight, "forest", 1.5f) },
                Slope("climb1", 14, 4f),
                Repeat(5, Straight, "ledgeA"),
                Repeat(2, Right30, "turnA"),
                new[] { S(Straight, "turnA") },
                Repeat(3, Straight, "rimA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(6, Viaduct, "bridge1"),
                Repeat(3, Straight, "rimB"),
                new[] { Switch("Track_Switch_20m_Left", "uitzichtwissel", true,
                                S(Straight, "zijspoor"), S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Repeat(2, Left30, "turnB"),
                new[] { S(Straight, "turnB") },
                Slope("climb2", 20, 4f),
                Repeat(5, Straight, "ridge"),
                new[] { Switch("Track_Switch_20m_Right", "kamwissel", false, S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Repeat(3, Right30, "turnC"),
                new[] { S(Straight, "turnC") },
                Repeat(2, Straight, "ravineA"),
                Repeat(4, "Track_Bridge_Trestle_6m", "trestle"),
                Repeat(2, Straight, "ravineB"),
                Repeat(2, Right30, "turnD"),
                Repeat(2, Straight, "turnD"),
                Repeat(4, Straight, "ledgeC"),
                Repeat(2, Straight, "rimC"),
                new[] { S(Broken, "bridgeRail2") },
                Repeat(3, Viaduct, "bridge2"),
                Repeat(3, Straight, "rimD"),
                new[] { Switch("Track_Switch_20m_Left", "dalwissel", true, S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Slope("descent", 22, -4f),
                new[] { S(Left30, "forest2") },
                new[] { S(Broken, "damage"), S(Straight, "forest2"), S(Right15, "forest2") },
                new[] { S(Straight, "winterClimb", 2f) },
                Repeat(8, Straight, "winterClimb", 4f),
                new[] { S(Left15, "winterClimb", 4f) },
                Repeat(7, Straight, "winterClimb", 4f),
                new[] { S(Right30, "winterClimb", 4f) },
                Repeat(8, Straight, "winterClimb", 4f),
                new[] { S(Straight, "winterClimb", 2f) },
                Repeat(3, Straight, "meadow2")),
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "De diepe kloof",
                    parts = new[] { Along(new[] { "ledgeA" }, -1), Cross(new[] { "rimA" }, "bridge1", new[] { "rimB" }, -1) },
                    tiers = 14, armStart = new Vector3(26f, 80f, 40f), armEnd = new Vector3(26f, 60f, 30f), pillars = 4,
                },
                new CanyonSpec
                {
                    name = "De kamkloof", parts = new[] { Along(new[] { "ridge" }, -1) },
                    tiers = 12, armStart = new Vector3(26f, 70f, 30f), armEnd = new Vector3(26f, 60f, 30f), pillars = 2,
                },
                new CanyonSpec
                {
                    name = "Het ravijn", parts = new[] { Cross(new[] { "ravineA" }, "trestle", new[] { "ravineB" }, 1) },
                    tiers = 4, riverHalf = 1.6f, minHalf = 6f, maxHalf = 14f, sideFalls = 0, pillars = 0, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
                new CanyonSpec
                {
                    name = "De valleikloof",
                    parts = new[] { Along(new[] { "ledgeC" }, -1), Cross(new[] { "rimC" }, "bridge2", new[] { "rimD" }, -1) },
                    tiers = 13, armStart = new Vector3(26f, 60f, 30f), armEnd = new Vector3(26f, 70f, 40f), pillars = 2,
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Klifwand langs de richel", tags = new[] { "ledgeA" }, left = false, rockfall = "Steenlawine op de richel" },
                new CliffSpec { name = "Klifwand na de grote brug", tags = new[] { "rimB" }, left = true, from = 5.8f, to = 12f, above = 4.4f, rockfall = "Steenlawine na de grote brug" },
                new CliffSpec { name = "Klifwand langs de bergkam", tags = new[] { "ridge" }, left = false, from = 5.6f, to = 14f, above = 5.6f, rockfall = "Steenlawine op de bergkam" },
                new CliffSpec { name = "Klifwand boven de valleikloof", tags = new[] { "ledgeC", "rimC" }, left = false, from = 5.8f, to = 13f, above = 5f, rockfall = "Steenlawine boven de valleikloof" },
            },
            rails = new[]
            {
                new RailSpec { tag = "bridgeRail", title = "Herstel de rail bij het viaduct",
                    instructions = "De rail vlak voor het grote stenen viaduct is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein." },
                new RailSpec { tag = "bridgeRail2", title = "Herstel de rail bij de tweede brug",
                    instructions = "Ook vlak voor de tweede stenen brug ligt een kapotte rail. Stop ervoor en sla de gele bout vast met de moersleutel." },
            },
            damages = new[] { new DamageSpec { tag = "damage", name = "Spoorschade in het bos", chance = 0.6f } },
            switchFaults = true,
            minimumIncidents = 2,
            extraWrenches = 2,
            mesas = 0,
        };

        [MenuItem("Tools/Conductor Chaos/Level 3 openen")]
        public static void OpenSavedLevel()
        {
            const string path = "Assets/Scenes/" + ServiceRun.ThirdLevelScene + ".unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/Conductor Chaos/Level 3 bouwen")]
        public static void BuildMenu() => CliffLevelBuilder.BuildMenu(Spec);

        public static void Build() => CliffLevelBuilder.Build(Spec);
    }
}
