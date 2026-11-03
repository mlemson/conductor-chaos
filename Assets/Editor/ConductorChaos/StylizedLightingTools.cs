// Conductor Chaos — belichting zoals het conceptbeeld (issue #33).
// Menu: Conductor Chaos > Art > Lighting > Apply Stylized Lighting (op de open scène).
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
            var sky = EnsureSky();
            var profile = EnsureProfile();

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
            sun.color = Sun;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;                         // schaduw blijft gekleurd door het omgevingslicht
            var e = sun.transform.eulerAngles;
            if (e.x > 70f || e.x < 15f) sun.transform.rotation = Quaternion.Euler(42f, e.y, 0f);   // schuin licht laat de facetten zien

            // omgevingslicht, lucht, nevel (scène-instellingen; niet via Undo terug te draaien)
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 320f;
            DynamicGI.UpdateEnvironment();

            // nabewerking
            Volume volume = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.isGlobal && v.sharedProfile == profile) { volume = v; break; }
            if (volume == null)
            {
                var go = new GameObject("StylizedLighting (Volume)");
                Undo.RegisterCreatedObjectUndo(go, "Stylized lighting");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.sharedProfile = profile;
            }
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
            Debug.Log("[Lighting] Gestileerde belichting toegepast: warme zon, koele schaduwen, verlooplucht, nevel en nabewerking. " +
                      "Speel ermee via het object 'StylizedLighting (Volume)' (Assets/Art/Lighting/VP_Stylized) en de Sun.", volume);
        }

        static Material EnsureSky()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShader);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, SkyPath);
            }
            mat.SetColor("_Top", SkyTop);
            mat.SetColor("_Horizon", SkyHorizon);
            mat.SetColor("_Bottom", SkyBottom);
            mat.SetColor("_SunColor", Sun);
            mat.SetFloat("_Exponent", 1.6f);
            mat.SetFloat("_SunGlow", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c))
                {
                    c = profile.Add<T>(false);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                return c;
            }

            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);           // houdt de paletkleuren herkenbaar

            var bloom = Get<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(Sun);

            var adjust = Get<ColorAdjustments>();
            adjust.postExposure.Override(0.15f);
            adjust.contrast.Override(8f);
            adjust.saturation.Override(14f);

            var split = Get<SplitToning>();                        // warm licht, koele blauw-violette schaduw
            split.shadows.Override(ShadowTint);
            split.highlights.Override(HighlightTint);
            split.balance.Override(-15f);

            var white = Get<WhiteBalance>();
            white.temperature.Override(6f);

            var vignette = Get<Vignette>();
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
