// Conductor Chaos — level 2 'Langs de kloof' (issue #224, groter in #228). Menu: Tools > Conductor Chaos > Level 2 bouwen.
// Puddleford → Craggleton: door het bos omhoog door een insnijding, een wissel met zijspoor op het plateau, langs de rand
// van een diepe kloof, over een stenen boogbrug, langs de andere rand en over een richel boven dezelfde kloof, dan over een
// houten schraagbrug door een smal ravijn en afdalen door het bos. De map is een vallei met rotswanden eromheen.
// Het bouwen zelf staat in CliffLevelBuilder; hier staat alleen wat level 2 eigen is.
using ConductorChaos.Stations;
using UnityEditor;
using UnityEngine;
using static ConductorChaos.EditorTools.CliffLevelBuilder;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class Level02Builder
    {
        const string OfferedKey = "ConductorChaos.Level02.Offered";
        const string Straight = "Track_Straight_12m", Broken = "Track_Broken_6m";

        public static readonly LevelSpec Spec = new LevelSpec
        {
            number = 2,
            scene = ServiceRun.SecondLevelScene,
            label = "LEVEL 2",
            title = "Langs de kloof",
            departureName = "Puddleford",
            arrivalName = "Craggleton",
            seed = 2026_11_15,
            route = Join(
                Repeat(2, Straight, "meadow"),
                new[] { S("Track_Curve_R20_15_Left", "forest") },
                new[] { S(Straight, "forest", 1.5f), S(Straight, "forest", 2.5f), S(Straight, "forest", 2.5f), S(Straight, "forest", 1.5f) },
                Repeat(3, "Track_Ramp_Up_18m_Steep", "climb"),
                Repeat(2, "Track_Curve_R20_30_Right", "plateau"),
                new[] { S(Straight, "plateau") },
                new[] { Switch("Track_Switch_20m_Right", "plateau-wissel", true,
                                S(Straight, "zijspoor"), S(Straight, "zijspoor"), S("Track_BufferStop", "zijspoor")) },
                Repeat(4, Straight, "rimA"),
                new[] { S(Broken, "bridgeRail") },
                Repeat(4, "Track_Bridge_Viaduct_12m", "bridge"),
                Repeat(4, Straight, "rimB"),
                Repeat(2, "Track_Curve_R20_30_Right", "bend"),
                new[] { S(Straight, "bend") },
                Repeat(5, Straight, "ledge"),
                new[] { S("Track_Curve_R20_30_Right", "ledgeTurn") },
                Repeat(2, Straight, "ledgeTurn"),
                Repeat(2, Straight, "ravineA"),
                Repeat(4, "Track_Bridge_Trestle_6m", "trestle"),
                Repeat(2, Straight, "ravineB"),
                Repeat(4, "Track_Ramp_Down_12m", "descent"),
                new[] { S("Track_Curve_R20_30_Right", "forest2") },
                new[] { S(Straight, "forest2", -1.5f), S(Straight, "forest2", -2.5f), S(Straight, "forest2", -2.5f), S(Straight, "forest2", -1.5f) },
                new[] { S(Broken, "damage"), S(Straight, "forest2"), S("Track_Curve_R20_15_Left", "forest2") },
                Repeat(2, Straight, "meadow2")),
            canyons = new[]
            {
                new CanyonSpec
                {
                    name = "De grote kloof",
                    parts = new[] { Cross(new[] { "rimA" }, "bridge", new[] { "rimB" }, -1), Along(new[] { "ledge" }, 1) },
                    tiers = 13, armStart = new Vector3(26f, 80f, 40f), armEnd = new Vector3(24f, 35f, 0f), pillars = 3,
                },
                new CanyonSpec
                {
                    name = "Het ravijn", parts = new[] { Cross(new[] { "ravineA" }, "trestle", new[] { "ravineB" }, -1) },
                    tiers = 4, riverHalf = 1.6f, minHalf = 6f, maxHalf = 14f, sideFalls = 0, pillars = 0, gap = 30f,
                    armStart = new Vector3(16f, 60f, 25f), armEnd = new Vector3(16f, 60f, 25f),
                },
            },
            cliffs = new[]
            {
                new CliffSpec { name = "Klifwand langs de klim", tags = new[] { "climb" }, left = true, rockfall = "Steenlawine bij de klim" },
                new CliffSpec { name = "Klifwand na de brug", tags = new[] { "rimB" }, left = true, from = 5.8f, to = 12f, above = 4.4f, rockfall = "Steenlawine na de brug" },
                new CliffSpec { name = "Klifwand boven de richel", tags = new[] { "ledge" }, left = true, from = 5.8f, to = 13f, above = 5.2f, rockfall = "Steenlawine op de richel" },
                new CliffSpec { name = "Insnijding bij de afdaling", tags = new[] { "descent" }, left = true, from = 5.4f, to = 10f, above = 3.2f, stone = true },
            },
            damages = new[]
            {
                new DamageSpec { tag = "bridgeRail", name = "Spoorschade voor de stenen brug" },
                new DamageSpec { tag = "damage", name = "Spoorschade in het bos" },
            },
            rotateDepartureObjectives = true,
            switchFaults = true,
            minimumIncidents = 2,
            timeLimits = new[] { 660, 600, 540, 480 },
            extraWrenches = 1,
            mesas = 10,
        };

        static Level02Builder()
        {
            EditorApplication.delayCall += OfferOnce;
        }

        // Geen pop-up (die zou je werk onderbreken): één melding in de Console zolang level 2 nog niet gebouwd is.
        static void OfferOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Exists(Spec) || !SourceExists || SessionState.GetBool(OfferedKey, false)) return;
            SessionState.SetBool(OfferedKey, true);
            Debug.Log("[Level 2] Nog niet gebouwd. Kies Tools > Conductor Chaos > Level 2 bouwen wanneer het je uitkomt.");
        }

        [MenuItem("Tools/Conductor Chaos/Level 2 bouwen")]
        public static void BuildMenu() => CliffLevelBuilder.BuildMenu(Spec);

        public static void Build() => CliffLevelBuilder.Build(Spec);

        internal static Step[] Join(params Step[][] parts)
        {
            var all = new System.Collections.Generic.List<Step>();
            foreach (var p in parts) all.AddRange(p);
            return all.ToArray();
        }
    }
}
