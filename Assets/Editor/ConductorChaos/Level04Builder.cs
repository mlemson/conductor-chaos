// Conductor Chaos — level 4 'Over de pas' (issue #242): het winterlevel. Menu: Tools > Conductor Chaos > Level 4 bouwen.
// Eaglecrest → Frostbury: haarspeldklim, tunnel door de pas, afdaling naar het ijsdal en klim naar het eindstation.
using ConductorChaos.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ConductorChaos.EditorTools.CliffLevelBuilder;

namespace ConductorChaos.EditorTools
{
    public static class Level04Builder
    {
        const string Straight = "Track_Straight_12m", Broken = "Track_Broken_6m", Viaduct = "Track_Bridge_Viaduct_12m";
        const string Right30 = "Track_Curve_R20_30_Right", Left30 = "Track_Curve_R20_30_Left";
        const string Right15 = "Track_Curve_R20_15_Right", Left15 = "Track_Curve_R20_15_Left";

        static Step[] Slope(string tag, int fullGradePieces, float grade) => Level02Builder.Join(
            new[] { S(Straight, tag, grade * 0.5f) }, Repeat(fullGradePieces, Straight, tag, grade),
            new[] { S(Straight, tag, grade * 0.5f) });

        public static readonly LevelSpec Spec = new LevelSpec
        {
            number = 4,
            scene = ServiceRun.FourthLevelScene,
            label = "LEVEL 4",
            title = "Over de pas",
            departureName = "Eaglecrest",
            arrivalName = "Frostbury",
            seed = 2026_11_17,
            winter = true,
            cliffRing = false,
            route = Level02Builder.Join(
                Repeat(3, Straight, "dal"),
                new[] { S(Straight, "climb1", 2f) },
                Repeat(6, Straight, "climb1", 4f),
                new[] { S(Left15, "climb1", 4f) },
                Repeat(6, Straight, "climb1", 4f),
                new[] { S(Right15, "climb1", 4f) },
                Repeat(3, Straight, "climb1", 4f),
                new[] { S(Straight, "climb1", 2f) },
                Repeat(3, Right30, "haarspeld1"),
                Repeat(3, Straight, "uitloper"),
                Repeat(3, Right30, "haarspeld1"),
                new[] { S(Straight, "climb2", 2f) },
                Repeat(4, Straight, "climb2", 4f),
                new[] { S(Right15, "climb2", 4f) },
                Repeat(6, Straight, "climb2", 4f),
                new[] { S(Left30, "climb2", 4f) },
                Repeat(3, Straight, "climb2", 4f),
                new[] { S(Straight, "climb2", 2f) },
                Repeat(2, Straight, "ledgeA"),
                Repeat(2, Left30, "haarspeld2"),
                Repeat(2, Straight, "sneeuwbank"),
                Repeat(3, Left30, "haarspeld2"),
                Slope("climb3", 14, 4f),
                Repeat(2, Straight, "voorportaal"),
                Repeat(5, Straight, "tunnel"),
                Repeat(3, Straight, "pass"),
                new[] { S(Left30, "pass"), S(Left30, "pass") },
                Slope("descent", 28, -4f),
                new[] { Switch("Track_Switch_20m_Right", "dalwissel", false, S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Repeat(2, Straight, "rimA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(3, Viaduct, "bridge1"),
                Repeat(3, Straight, "rimB"),
                new[] { S(Right30, "valleyTurn"), S(Right30, "valleyTurn"), S(Broken, "damage") },
                Slope("finalClimb", 15, 4f),
                Repeat(3, Straight, "meadow2")),
            tunnelTag = "tunnel",
            mountains = new[]
            {
                new MountainSpec("climb3", 104f, 213f, 126f, 90f),
                new MountainSpec("tunnel", 75f, 204f, 132f, 99f),
                new MountainSpec("tunnel", 36f, 103f, 72f, 51f),
                new MountainSpec("descent", -123f, 222f, 157f, 52f),
                new MountainSpec("finalClimb", 88f, 160f, 130f, 46f),
            },
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "Het ijsdal", parts = new[] { Cross(new[] { "rimA" }, "bridge1", new[] { "rimB" }, -1) },
                    tiers = 5, riverHalf = 2.2f, minHalf = 7f, maxHalf = 16f, sideFalls = 0, pillars = 0, gap = 30f,
                    armStart = new Vector3(20f, 38f, 39f), armEnd = new Vector3(20f, 42f, 42f),
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Besneeuwde wand boven de haarspeld", tags = new[] { "ledgeA" }, left = true, from = 8f, to = 18f, above = 2f, stone = true, rockfall = "Sneeuwlawine boven het dal" },
                new CliffSpec { name = "Rotsband na de tunnel", tags = new[] { "pass" }, left = false, from = 9f, to = 22f, above = 3f, stone = true, rockfall = "Sneeuwlawine na de tunnel" },
            },
            rails = new[]
            {
                new RailSpec { tag = "bridgeRail", title = "Herstel de rail bij het viaduct",
                    instructions = "De rail vlak voor het stenen viaduct is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein." },
            },
            damages = new[] { new DamageSpec { tag = "damage", name = "Spoorschade in het winterbos", chance = 0.6f } },
            switchFaults = true,
            minimumIncidents = 2,
            timeLimits = new[] { 900, 840, 780, 720 },
            extraWrenches = 2,
            mesas = 0,
        };

        [MenuItem("Tools/Conductor Chaos/Level 4 openen")]
        public static void OpenSavedLevel()
        {
            const string path = "Assets/Scenes/" + ServiceRun.FourthLevelScene + ".unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/Conductor Chaos/Level 4 bouwen (winter)")]
        public static void BuildMenu() => CliffLevelBuilder.BuildMenu(Spec);

        public static void Build() => CliffLevelBuilder.Build(Spec);
    }
}
