// Conductor Chaos — level 4 'Over de pas' (issue #242): het winterlevel. Menu: Tools > Conductor Chaos > Level 4 bouwen.
// Eaglecrest → Frostbury door de sneeuw: een stuk door het besneeuwde bos, steil omhoog naar een richel langs een
// besneeuwde wand (sneeuwlawine), een houten schraagbrug over de ijskloof met een bevroren rivier, over de pas zelf
// langs een tweede wand (nog een lawine), een kapotte rail vóór het stenen viaduct over de vallei, en dan omlaag naar
// Frostbury. Reizigers kunnen bevriezen (#243): buiten in de kou, of in een wagon met een open deur. Het bouwen zelf
// staat in CliffLevelBuilder (spec.winter zet sneeuwgrond, besneeuwde bomen/kliffen, ijs en de kou aan).
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
            route = Level02Builder.Join(
                Repeat(2, Straight, "meadow"),
                new[] { S(Straight, "forest", 1.5f), S(Straight, "forest", 2.5f), S(Left30, "forest", 2.5f), S(Straight, "forest", 1.5f) },
                Repeat(2, "Track_Ramp_Up_18m_Steep", "climb1"),
                Repeat(5, Straight, "ledgeA"),
                Repeat(2, Right30, "turnA"),
                new[] { S(Straight, "turnA") },
                Repeat(2, Straight, "ravineA"),
                Repeat(4, "Track_Bridge_Trestle_6m", "trestle"),
                Repeat(2, Straight, "ravineB"),
                new[] { Switch("Track_Switch_20m_Right", "paswissel", false, S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Repeat(2, "Track_Ramp_Up_18m_Steep", "climb2"),
                Repeat(5, Straight, "pass"),
                Repeat(2, Left30, "turnB"),
                Repeat(2, Straight, "rimA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(4, Viaduct, "bridge1"),
                Repeat(3, Straight, "rimB"),
                Repeat(2, "Track_Ramp_Down_18m_Steep", "descent"),
                Repeat(3, "Track_Ramp_Down_12m", "descent"),
                new[] { S(Right30, "forest2") },
                new[] { S(Straight, "forest2", -1.5f), S(Straight, "forest2", -2.5f), S(Straight, "forest2", -1.5f) },
                new[] { S(Broken, "damage"), S(Straight, "forest2"), S("Track_Curve_R20_15_Left", "forest2") },
                Repeat(2, Straight, "meadow2")),
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "De ijskloof", parts = new[] { Cross(new[] { "ravineA" }, "trestle", new[] { "ravineB" }, 1) },
                    tiers = 5, riverHalf = 1.8f, minHalf = 6f, maxHalf = 14f, sideFalls = 1, pillars = 0, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
                new CanyonSpec
                {
                    name = "De sneeuwvallei",
                    parts = new[] { Along(new[] { "pass" }, 1), Cross(new[] { "rimA" }, "bridge1", new[] { "rimB" }, -1) },
                    tiers = 12, armStart = new Vector3(26f, 70f, 30f), armEnd = new Vector3(26f, 60f, 30f), pillars = 2,
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Besneeuwde wand langs de richel", tags = new[] { "climb1", "ledgeA" }, left = false, rockfall = "Sneeuwlawine op de richel" },
                new CliffSpec { name = "Wand op de pas", tags = new[] { "climb2", "pass" }, left = false, from = 5.6f, to = 13f, above = 5.2f, rockfall = "Sneeuwlawine op de pas" },
                new CliffSpec { name = "Insnijding bij de afdaling", tags = new[] { "descent" }, left = true, from = 5.4f, to = 10f, above = 3.2f, stone = true },
            },
            rails = new[]
            {
                new RailSpec { tag = "bridgeRail", title = "Herstel de rail bij het viaduct",
                    instructions = "De rail vlak voor het stenen viaduct is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein." },
            },
            damages = new[] { new DamageSpec { tag = "damage", name = "Spoorschade in het winterbos", chance = 0.6f } },
            switchFaults = true,
            minimumIncidents = 2,
            timeLimits = new[] { 840, 780, 720, 660 },
            extraWrenches = 2,
            mesas = 14,
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
