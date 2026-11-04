// Conductor Chaos — belichting zoals het conceptbeeld (issue #33).
// Menu: Conductor Chaos > Art > Lighting > Apply Stylized Lighting (op de open scène).
// Apply Neutral Lighting is het rustige alternatief: minder blauw omgevingslicht, geen split toning, nauwelijks bloom.
// Apply Warm Stylized Lighting zit dichter bij de conceptart: warmere zon, blauwere lucht, wat verzadiging en meer nevel in de verte.
//   - Zon: warm (parchment), zachte schaduwen die niet zwart worden.
//   - Omgevingslicht in drie lagen: blauwe lucht van boven, lavendel bij de horizon, warm groen van onder,
//     zodat schaduwen koel blauw-violet kleuren (warm licht, koele schaduw).
//   - Lucht: verloop diepblauw -> licht bij de horizon met een zachte zongloed (ConductorChaos/GradientSky).
//   - Nevel in de verte (lavendelblauw), zodat bergen en achtergrond lagen krijgen.
//   - Nabewerking (URP Volume): zachte tonemapping, wat bloom, iets meer verzadiging en contrast,
//     warme hooglichten en koele schaduwen (split toning), lichte vignet.
// Alle kleuren komen uit Docs/References/palette.json (ramp-naam staat erbij). Cosmetisch en lokaal: geen netwerkstatus.
// Het pipeline-asset krijgt een grotere schaduwafstand en 4 cascades (geldt voor het hele project).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ConductorChaos.EditorTools
{
    public static class StylizedLightingTools
    {
        const string Dir = "Assets/Art/Lighting";
        const string ProfilePath = Dir + "/VP_Stylized.asset";
        const string NeutralProfilePath = Dir + "/VP_Neutral.asset";
        const string WarmProfilePath = Dir + "/VP_WarmStylized.asset";
        const string SkyPath = Dir + "/M_Sky_Gradient.mat";
        const string SkyShader = "Assets/Art/Shaders/GradientSky.shader";

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        // palette.json
        static readonly Color Sun = Hex("#FAECDC");            // parchment[4]
        static readonly Color SkyTop = Hex("#6E83BA");         // sky[0]
        static readonly Color SkyHorizon = Hex("#98C4FC");     // sky[4]
        static readonly Color SkyBottom = Hex("#C2CAEA");      // cloud_snow[0]
        static readonly Color AmbientSky = Hex("#85A4EC");     // sky[2]
        static readonly Color AmbientEquator = Hex("#B297B8"); // haze_purple[3]
        static readonly Color AmbientGround = Hex("#6B7F38");  // grass[1]
        static readonly Color Haze = Hex("#C2CAEA");           // cloud_snow[0]
        static readonly Color ShadowTint = Hex("#5D6EAC");     // mountain[0]
        static readonly Color HighlightTint = Hex("#F4B06C");  // track_wood[4]

        [MenuItem("Conductor Chaos/Art/Lighting/Apply Stylized Lighting")]
        public static void Apply()
        {
            EnsureFolder(Dir);
            Apply(EnsureProfile(), Sun, 1.35f, 0.7f, AmbientSky, AmbientEquator, AmbientGround, Haze, 45f, 320f, SkyTop);
        }

        [MenuItem("Conductor Chaos/Art/Lighting/Apply Neutral Lighting")]
        public static void ApplyNeutral()
        {
            EnsureFolder(Dir);
            Apply(EnsureNeutralProfile(), Sun, 1.15f, 0.6f, Hex("#8187B4") /* mountain[3] */, Hex("#9C8978") /* parchment[0] */,
                  AmbientGround, Haze, 70f, 450f, SkyTop);
        }

        [MenuItem("Conductor Chaos/Art/Lighting/Apply Warm Stylized Lighting")]
        public static void ApplyWarm()
        {
            EnsureFolder(Dir);
            Apply(EnsureWarmProfile(), Hex("#F2D8BB") /* parchment[2] */, 1.35f, 0.4f, Hex("#7D99D5") /* sky[1] */, Hex("#CAA791") /* parchment[1]: lichtere schaduwkant op het terrein */,
                  AmbientGround, Hex("#C7D0F0") /* cloud_snow[1] */, 60f, 420f, Hex("#7D99D5") /* sky[1] */);
        }

        static void Apply(VolumeProfile profile, Color sunColor, float sunIntensity, float shadowStrength, Color ambientSky, Color ambientEquator,
                          Color ambientGround, Color fogColor, float fogStart, float fogEnd, Color skyTop)
        {
            var sky = EnsureSky(skyTop);

            // zon
            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var go = new GameObject("Sun");
                Undo.RegisterCreatedObjectUndo(go, "Stylized lighting");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                go.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            }
            Undo.RecordObject(sun, "Stylized lighting");
            Undo.RecordObject(sun.transform, "Stylized lighting");
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = shadowStrength;                      // schaduw blijft gekleurd door het omgevingslicht
            var e = sun.transform.eulerAngles;
            if (e.x > 70f || e.x < 15f) sun.transform.rotation = Quaternion.Euler(42f, e.y, 0f);   // schuin licht laat de facetten zien

            // omgevingslicht, lucht, nevel (scène-instellingen; niet via Undo terug te draaien)
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            DynamicGI.UpdateEnvironment();

            // nabewerking
            Volume volume = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.isGlobal && v.sharedProfile != null && AssetDatabase.GetAssetPath(v.sharedProfile).StartsWith(Dir)) { volume = v; break; }
            if (volume == null)
            {
                var go = new GameObject("StylizedLighting (Volume)");
                Undo.RegisterCreatedObjectUndo(go, "Stylized lighting");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
            }
            Undo.RecordObject(volume, "Stylized lighting");
            volume.sharedProfile = profile;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Stylized lighting");
                data.renderPostProcessing = true;
            }

            // schaduwen (project-breed, URP-asset)
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                Undo.RecordObject(urp, "Stylized lighting");
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, 90f);
                urp.shadowCascadeCount = 4;
                urp.supportsHDR = true;
                EditorUtility.SetDirty(urp);
            }

            EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lighting] Belichting toegepast met {profile.name}. Speel ermee via het object 'StylizedLighting (Volume)' en de Sun.", volume);
        }

        static Material EnsureSky(Color top)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShader);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, SkyPath);
            }
            mat.SetColor("_Top", top);
            mat.SetColor("_Horizon", SkyHorizon);
            mat.SetColor("_Bottom", SkyBottom);
            mat.SetColor("_SunColor", Sun);
            mat.SetFloat("_Exponent", 1.6f);
            mat.SetFloat("_SunGlow", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            return profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var c))
            {
                c = profile.Add<T>(false);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            return c;
        }

        static VolumeProfile EnsureNeutralProfile()
        {
            var profile = LoadProfile(NeutralProfilePath);
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.08f);
            bloom.scatter.Override(0.5f);
            bloom.tint.Override(Sun);

            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(0f);
            adjust.contrast.Override(3f);
            adjust.saturation.Override(0f);

            Get<WhiteBalance>(profile).temperature.Override(2f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.1f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Hex("#351B13"));               // ink[2]

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static VolumeProfile EnsureWarmProfile()
        {
            var profile = LoadProfile(WarmProfilePath);
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.15f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(Sun);

            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(0f);
            adjust.contrast.Override(10f);
            adjust.saturation.Override(8f);

            var split = Get<SplitToning>(profile);                 // tinten dicht bij middengrijs: lichtere tinten maken het beeld melkig
            split.shadows.Override(Hex("#7B7582"));                // metal[1]: bijna grijs, blauw hierin maakt kliffen weer roze
            split.highlights.Override(Hex("#9C8978"));             // parchment[0]

            Get<WhiteBalance>(profile).temperature.Override(3f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Hex("#351B13"));               // ink[2]

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static VolumeProfile EnsureProfile()
        {
            var profile = LoadProfile(ProfilePath);

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);           // houdt de paletkleuren herkenbaar

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(Sun);

            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.15f);
            adjust.contrast.Override(8f);
            adjust.saturation.Override(14f);

            var split = Get<SplitToning>(profile);                        // warm licht, koele blauw-violette schaduw
            split.shadows.Override(ShadowTint);
            split.highlights.Override(HighlightTint);
            split.balance.Override(-15f);

            var white = Get<WhiteBalance>(profile);
            white.temperature.Override(6f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Hex("#351B13"));               // ink[2]

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
