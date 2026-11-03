// Conductor Chaos — bouwt een art-showcase-scène uit de layout die de art-pipeline exporteert (issue #33).
// Menu: Conductor Chaos > Art > Build Showcase Scene / Capture Showcase Screenshot.
// Batch: Unity -batchmode -projectPath . -executeMethod ConductorChaos.EditorTools.ArtShowcaseBuilder.BuildAndCaptureBatch -quit
//
// De layout (Assets/Art/Layouts/ArtShowcase.json) komt uit ArtSource/Render/export_unity_layout.py en gebruikt de
// rechtshandige assen van de bron (FBX/three.js). Unity spiegelt FBX bij import langs X; daarom x -> -x en rotY -> -rotY.
// Statische art-scène: geen gameplay en geen netwerkstatus (multiplayer niet van toepassing).
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConductorChaos.EditorTools
{
    public static class ArtShowcaseBuilder
    {
        const string LayoutPath = "Assets/Art/Layouts/ArtShowcase.json";
        const string ScenePath = "Assets/Scenes/Art/ArtShowcase.unity";
        const string ScreenshotPath = "Docs/Art/Unity_ArtShowcase.png";

        [Serializable] class CameraDef { public float[] target; public float yaw, pitch, dist, fov, far; }
        [Serializable] class LightDef { public float az, el, intensity; public string color; }
        [Serializable] class FogDef { public string color; public float near, far; }
        [Serializable] class Item { public string path; public float[] pos; public float rotY; public float[] scale; public string[] hide; public string[] show; }
        [Serializable] class Layout { public CameraDef camera; public LightDef light; public FogDef fog; public string sky; public Item[] items; }

        static Vector3 ToUnity(float[] p) => p == null || p.Length < 3 ? Vector3.zero : new Vector3(-p[0], p[1], p[2]);

        static Color Hex(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        [MenuItem("Conductor Chaos/Art/Build Showcase Scene")]
        public static void Build()
        {
            // open scène met niet-opgeslagen wijzigingen? Eerst vragen (NewScene zou ze anders weggooien).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (json == null) { Debug.LogError($"[ArtShowcase] Layout ontbreekt: {LayoutPath}"); return; }
            var layout = JsonUtility.FromJson<Layout>(json.text);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ArtShowcase");
            int placed = 0, missing = 0;
            foreach (var it in layout.items)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(it.path);
                if (prefab == null) { Debug.LogWarning($"[ArtShowcase] Model niet gevonden: {it.path}"); missing++; continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = ToUnity(it.pos);
                go.transform.localRotation = Quaternion.Euler(0f, -it.rotY, 0f);
                if (it.scale != null && it.scale.Length == 3) go.transform.localScale = new Vector3(it.scale[0], it.scale[1], it.scale[2]);
                if (it.hide != null) foreach (var n in it.hide) { var t = FindDeep(go.transform, n); if (t) t.gameObject.SetActive(false); }
                if (it.show != null) foreach (var n in it.show) { var t = FindDeep(go.transform, n); if (t) t.gameObject.SetActive(true); }
                placed++;
            }

            // zon: richting zoals in de render (positie op een bol rond het doel), warm licht
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex(layout.light?.color, new Color(1f, 0.89f, 0.74f));
            sun.intensity = layout.light != null && layout.light.intensity > 0 ? layout.light.intensity : 1.2f;
            sun.shadows = LightShadows.Soft;
            float az = (layout.light?.az ?? -40f) * Mathf.Deg2Rad, el = (layout.light?.el ?? 48f) * Mathf.Deg2Rad;
            var toLight = new Vector3(-Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            sun.transform.rotation = Quaternion.LookRotation(-toLight);

            // camera
            var cd = layout.camera;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            var target = ToUnity(cd.target);
            float yaw = cd.yaw * Mathf.Deg2Rad, pitch = cd.pitch * Mathf.Deg2Rad;
            camGo.transform.position = target + new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * cd.dist;
            camGo.transform.LookAt(target);
            cam.fieldOfView = cd.fov > 0 ? cd.fov : 40f;
            cam.farClipPlane = cd.far > 0 ? cd.far : 700f;
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex(layout.sky, new Color(0.52f, 0.64f, 0.93f));

            // atmosfeer: lineaire mist naar koel blauw (de shader gebruikt URP-fog), vlak ambient
            RenderSettings.fog = layout.fog != null;
            if (layout.fog != null)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Hex(layout.fog.color, new Color(0.57f, 0.66f, 0.90f));
                RenderSettings.fogStartDistance = layout.fog.near;
                RenderSettings.fogEndDistance = layout.fog.far;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.80f);
            RenderSettings.skybox = null;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[ArtShowcase] Scène opgeslagen: {ScenePath} ({placed} modellen, {missing} ontbrekend)");
        }

        [MenuItem("Conductor Chaos/Art/Capture Showcase Screenshot")]
        public static void Capture()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[ArtShowcase] Geen Main Camera in de open scène."); return; }
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            // URP: Camera.Render() naar een RenderTexture tekent in de editor alleen de achtergrond;
            // een render request gaat wel door de volledige pipeline.
            var prev = cam.targetTexture;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
            }
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = old;
            cam.targetTexture = prev;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScreenshotPath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[ArtShowcase] Screenshot: {path}");
        }

        /// <summary>Voor -batchmode -executeMethod: scène bouwen en een screenshot maken.</summary>
        public static void BuildAndCaptureBatch()
        {
            Build();
            Capture();
        }
    }
}
