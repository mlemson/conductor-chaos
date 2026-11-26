// Conductor Chaos — level 6 'Door de rode kloof': het eerste woestijnlevel. Menu: Tools > Conductor Chaos > Level 6 bouwen.
// Fernwood → Redrock (concept: Docs/References/Desert/): over de zandvlakte met windmolens en tafelbergen, waar een
// bizonkudde het spoor oversteekt (bizonbord), de rode wand op (steenlawine), over een richel langs de diepe rode kloof
// met rivier en watervallen, een kapotte rail vóór de houten schraagbrug over het droge ravijn, onder een natuurlijke
// rotsboog door en omlaag naar Redrock. Het bouwen zelf staat in CliffLevelBuilder (spec.desert).
using ConductorChaos.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ConductorChaos.EditorTools.CliffLevelBuilder;

namespace ConductorChaos.EditorTools
{
    public static class Level06Builder
    {
        const string Straight = "Track_Straight_12m", Broken = "Track_Broken_6m", Trestle = "Track_Bridge_Trestle_6m";
        const string Right30 = "Track_Curve_R20_30_Right", Left30 = "Track_Curve_R20_30_Left";
        const string Right15 = "Track_Curve_R20_15_Right", Left15 = "Track_Curve_R20_15_Left";

        public static readonly LevelSpec Spec = new LevelSpec
        {
            number = 6,
            scene = ServiceRun.SixthLevelScene,
            label = "LEVEL 6",
            title = "Door de rode kloof",
            departureName = "Fernwood",
            arrivalName = "Redrock",
            seed = 2026_11_26,
            desert = true,
            route = Level02Builder.Join(
                Repeat(2, Straight, "meadow"),
                new[] { Switch("Track_Switch_20m_Right", "laadwissel", false,
                                S(Straight, "zijspoor"), S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                new[] { S(Straight, "vlakte"), S(Left15, "vlakte"), S(Straight, "vlakte") },
                Repeat(4, Straight, "kudde"),
                new[] { S(Right15, "vlakte2") },
                Repeat(4, Straight, "vlakte2"),
                Repeat(3, "Track_Ramp_Up_18m_Steep", "klim"),
                Repeat(2, Right30, "plateau"),
                new[] { S(Straight, "plateau") },
                Repeat(5, Straight, "richel"),
                new[] { S(Right30, "richelBocht") },
                Repeat(2, Straight, "richelBocht"),
                Repeat(2, Straight, "rimA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(5, Trestle, "trestle"),
                Repeat(2, Straight, "rimB"),
                Repeat(2, Left30, "bocht"),
                new[] { S(Straight, "bocht") },
                Repeat(3, Straight, "woestijnweg"),
                Repeat(3, Straight, "boog"),
                Repeat(3, "Track_Ramp_Down_12m", "afdaling"),
                new[] { S(Left15, "aankomst"), S(Straight, "aankomst"), S(Broken, "damage"), S(Straight, "aankomst"), S(Straight, "aankomst") },
                Repeat(2, Straight, "meadow2")),
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "De rode kloof", parts = new[] { Along(new[] { "richel" }, 1) },
                    tiers = 12, armStart = new Vector3(26f, 80f, 40f), armEnd = new Vector3(24f, 35f, 0f), pillars = 4, sideFalls = 2,
                },
                new CanyonSpec
                {
                    name = "Het droge ravijn", parts = new[] { Cross(new[] { "rimA" }, "trestle", new[] { "rimB" }, -1) },
                    tiers = 4, riverHalf = 1.6f, minHalf = 6f, maxHalf = 14f, sideFalls = 0, pillars = 1, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Rode wand langs de klim", tags = new[] { "klim" }, left = true, rockfall = "Steenlawine bij de klim" },
                new CliffSpec { name = "Wand boven de richel", tags = new[] { "richel" }, left = true, from = 5.8f, to = 13f, above = 5.2f,
                    rockfall = "Steenlawine op de richel" },
                new CliffSpec { name = "Insnijding bij de afdaling", tags = new[] { "afdaling" }, left = true, from = 5.4f, to = 10f, above = 3.2f, stone = true },
            },
            herds = new[]
            {
                new HerdSpec { tag = "kudde", name = "Bizonkudde op de vlakte", side = 1, adults = 5, calves = 2, stubborn = 2 },
            },
            drops = new[]
            {
                new DropSpec { tag = "vlakte2", name = "Tumbleweeds op de vlakte", warning = "Tumbleweeds op het spoor", model = "Desert/Tumbleweed_01",
                    count = 3, side = -1, distance = 9f, spacing = 2.8f, mass = 2f, linearDamping = 1.6f, angularDamping = 2.5f, chance = 0.5f },
            },
            rails = new[]
            {
                new RailSpec { tag = "bridgeRail", title = "Herstel de rail bij de schraagbrug",
                    instructions = "De rail vlak voor de houten schraagbrug over het droge ravijn is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein." },
            },
            damages = new[] { new DamageSpec { tag = "damage", name = "Spoorschade voor Redrock", chance = 0.6f } },
            switchFaults = true,
            minimumIncidents = 2,
            extraWrenches = 2,
            mesas = 12,
            archTag = "boog",
            windmills = 3,
        };

        [MenuItem("Tools/Conductor Chaos/Level 6 openen")]
        public static void OpenSavedLevel()
        {
            const string path = "Assets/Scenes/" + ServiceRun.SixthLevelScene + ".unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/Conductor Chaos/Level 6 bouwen (woestijn)")]
        public static void BuildMenu() => CliffLevelBuilder.BuildMenu(Spec);

        public static void Build() => CliffLevelBuilder.Build(Spec);
    }
}
