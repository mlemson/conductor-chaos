// Conductor Chaos — level 7 'De goudmijnkloof': het tweede woestijnlevel. Menu: Tools > Conductor Chaos > Level 7 bouwen.
// Redrock → Goldspur (concept: Docs/References/Desert/): een kudde op de vlakte bij de windmolenput, de wand op, een
// richel langs de diepe goudkloof met slingerbochten (bochtborden), een schraagbrug met kapotte rail, een zandvlakte
// waar in de zandstorm tumbleweeds op het spoor waaien, de mijnwissel met het mijnspoor naar de goudmijn (een
// losgeslagen ertskarretje van het laadperron), een tweede, grotere kudde, nog een schraagbrug, de rotsboog en omlaag
// naar Goldspur. Het bouwen zelf staat in CliffLevelBuilder (spec.desert, spec.sandstorm).
using ConductorChaos.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ConductorChaos.EditorTools.CliffLevelBuilder;

namespace ConductorChaos.EditorTools
{
    public static class Level07Builder
    {
        const string Straight = "Track_Straight_12m", Broken = "Track_Broken_6m", Trestle = "Track_Bridge_Trestle_6m";
        const string Right30 = "Track_Curve_R20_30_Right", Left30 = "Track_Curve_R20_30_Left";
        const string Right15 = "Track_Curve_R20_15_Right", Left15 = "Track_Curve_R20_15_Left";

        public static readonly LevelSpec Spec = new LevelSpec
        {
            number = 7,
            scene = ServiceRun.SeventhLevelScene,
            label = "LEVEL 7",
            title = "De goudmijnkloof",
            departureName = "Redrock",
            arrivalName = "Goldspur",
            seed = 2026_11_27,
            desert = true,
            sandstorm = true,
            route = Level02Builder.Join(
                Repeat(2, Straight, "meadow"),
                new[] { S(Straight, "vlakte"), S(Right15, "vlakte") },
                Repeat(4, Straight, "kuddeA"),
                new[] { Switch("Track_Switch_20m_Left", "windwissel", true,
                                S(Straight, "zijspoor"), S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                new[] { S(Left15, "vlakte2"), S(Straight, "vlakte2") },
                Repeat(3, "Track_Ramp_Up_18m_Steep", "klim"),
                Repeat(2, Left30, "kam"),
                new[] { S(Straight, "kam") },
                Repeat(5, Straight, "richel"),
                new[] { S(Left30, "slinger"), S(Straight, "slinger"), S(Left30, "slinger") },
                Repeat(2, Straight, "slinger"),
                Repeat(2, Straight, "ravijnA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(6, Trestle, "trestle1"),
                Repeat(2, Straight, "ravijnB"),
                Repeat(2, Right30, "bocht2"),
                new[] { S(Straight, "bocht2") },
                Repeat(3, Straight, "zandvlakte"),
                new[] { Switch("Track_Switch_20m_Right", "mijnwissel", false,
                                S(Straight, "mijnspoor"), S(Straight, "mijnspoor"), S("Track_BufferStop", "mijnspoor")) },
                Repeat(3, Straight, "mijn"),
                new[] { S(Right30, "bocht3"), S(Straight, "bocht3") },
                Repeat(4, Straight, "kuddeB"),
                Repeat(3, Straight, "vlakte3"),
                Repeat(2, Straight, "rimC"),
                new[] { S(Broken, "bridgeRail2") },
                Repeat(4, Trestle, "trestle2"),
                Repeat(2, Straight, "rimD"),
                Repeat(3, Straight, "boog"),
                Repeat(3, "Track_Ramp_Down_12m", "afdaling"),
                new[] { S(Left15, "aankomst"), S(Broken, "damage"), S(Straight, "aankomst") },
                Repeat(2, Straight, "meadow2")),
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "De goudkloof", parts = new[] { Along(new[] { "richel" }, -1) },
                    tiers = 13, armStart = new Vector3(26f, 80f, 40f), armEnd = new Vector3(24f, 35f, 0f), pillars = 4, sideFalls = 2,
                },
                new CanyonSpec
                {
                    name = "Het eerste ravijn", parts = new[] { Cross(new[] { "ravijnA" }, "trestle1", new[] { "ravijnB" }, 1) },
                    tiers = 4, riverHalf = 1.8f, minHalf = 6f, maxHalf = 15f, sideFalls = 1, pillars = 1, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
                new CanyonSpec
                {
                    name = "Het tweede ravijn", parts = new[] { Cross(new[] { "rimC" }, "trestle2", new[] { "rimD" }, -1) },
                    tiers = 4, riverHalf = 1.6f, minHalf = 6f, maxHalf = 14f, sideFalls = 0, pillars = 0, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Rode wand langs de klim", tags = new[] { "klim" }, left = false, rockfall = "Steenlawine bij de klim" },
                new CliffSpec { name = "Wand boven de goudkloof", tags = new[] { "richel" }, left = false, from = 5.8f, to = 13f, above = 5.2f,
                    rockfall = "Steenlawine boven de richel" },
                new CliffSpec { name = "Insnijding bij de afdaling", tags = new[] { "afdaling" }, left = true, from = 5.4f, to = 10f, above = 3.2f, stone = true },
            },
            herds = new[]
            {
                new HerdSpec { tag = "kuddeA", name = "Bizonkudde bij de windmolenput", side = -1, adults = 5, calves = 2, stubborn = 2 },
                new HerdSpec { tag = "kuddeB", name = "Grote bizonkudde", side = 1, adults = 6, calves = 3, stubborn = 3 },
            },
            drops = new[]
            {
                new DropSpec { tag = "zandvlakte", name = "Tumbleweeds in de zandstorm", warning = "Tumbleweeds op het spoor", model = "Desert/Tumbleweed_01",
                    count = 4, side = -1, distance = 9f, spacing = 2.6f, mass = 2f, linearDamping = 1.6f, angularDamping = 2.5f, chance = 0.85f },
                new DropSpec { tag = "mijn", name = "Losgeslagen mijnkarretje", warning = "Mijnkarretje op het spoor", model = "Desert/MineCart_Gold_01",
                    count = 1, side = -1, distance = 6.5f, raise = 2f, platform = true, mass = 30f, linearDamping = 0.4f, angularDamping = 2f,
                    triggerAhead = 55f, chance = 0.8f },
            },
            rails = new[]
            {
                new RailSpec { tag = "bridgeRail", title = "Herstel de rail bij de eerste schraagbrug",
                    instructions = "De rail vlak voor de houten brug over het eerste ravijn is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein." },
                new RailSpec { tag = "bridgeRail2", title = "Herstel de rail bij de tweede schraagbrug",
                    instructions = "Ook vóór de tweede schraagbrug is een rail kapot. Stop, sla de gele bout vast en rijd dan pas verder." },
            },
            damages = new[] { new DamageSpec { tag = "damage", name = "Spoorschade voor Goldspur", chance = 0.6f } },
            switchFaults = true,
            minimumIncidents = 3,
            extraWrenches = 3,
            mesas = 12,
            archTag = "boog",
            mineTag = "mijnspoor",
            windmills = 4,
        };

        [MenuItem("Tools/Conductor Chaos/Level 7 openen")]
        public static void OpenSavedLevel()
        {
            const string path = "Assets/Scenes/" + ServiceRun.SeventhLevelScene + ".unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/Conductor Chaos/Level 7 bouwen (goudmijn)")]
        public static void BuildMenu() => CliffLevelBuilder.BuildMenu(Spec);

        public static void Build() => CliffLevelBuilder.Build(Spec);
    }
}
