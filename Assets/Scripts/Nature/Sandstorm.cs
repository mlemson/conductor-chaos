// Conductor Chaos — zandstorm (level 7): vlagen opgewaaid zand rond de camera van deze speler. Puur cosmetisch en lokaal,
// net als Snowfall. De tumbleweeds die op het spoor waaien zijn wel echte, server-autoritatieve obstakels
// (ServiceIncidents); dit is alleen de lucht eromheen.
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class Sandstorm : MonoBehaviour
    {
        [SerializeField, Min(0f)] float grainsPerSecond = 260f;
        [SerializeField] Vector3 wind = new(-1f, 0f, 0.35f);
        [SerializeField, Min(0f)] float windSpeed = 11f;
        [Tooltip("Seconden per vlaag (rustig - storm - rustig).")]
        [SerializeField, Min(5f)] float gustPeriod = 26f;

        ParticleSystem particles;
        Material material;
        Mesh grain;
        float baseFog;
        bool fogTouched;

        void Start()
        {
            Shader shader = Resources.Load<Shader>("AmbientDust");
            if (!shader) { Debug.LogWarning("Zandstorm: shader AmbientDust ontbreekt."); enabled = false; return; }
            material = new Material(shader) { name = "Zandkorrels" };
            grain = MakeGrain();
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .09f);
            // desert_sand / desert_band uit palette.json
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(251, 183, 107, 255), new Color32(231, 123, 76, 255));
            main.startRotation3D = true;
            var angle = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationX = main.startRotationY = main.startRotationZ = angle;
            main.maxParticles = 2400;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 9f, 44f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            Vector3 w = wind.normalized * windSpeed;
            velocity.x = new ParticleSystem.MinMaxCurve(w.x * .8f, w.x * 1.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-.4f, .8f);
            velocity.z = new ParticleSystem.MinMaxCurve(w.z * .8f, w.z * 1.2f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 1.1f;
            noise.frequency = .35f;
            noise.scrollSpeed = .6f;
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = grain;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (RenderSettings.fog && RenderSettings.fogMode != FogMode.Linear)
            {
                baseFog = RenderSettings.fogDensity;
                fogTouched = true;
            }
            particles.Play();
        }

        // 0 = windstil, 1 = volle storm: lange rustige stukken met af en toe een flinke vlaag
        float Gust => Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Time.time * Mathf.PI * 2f / gustPeriod) * .8f + .45f), 1.6f);

        void LateUpdate()
        {
            if (!particles) return;
            float gust = Gust;
            var emission = particles.emission;
            emission.rateOverTime = grainsPerSecond * (.12f + .88f * gust);
            if (fogTouched) RenderSettings.fogDensity = baseFog * (1f + 1.6f * gust);
            var cam = Camera.main;
            if (!cam) return;
            Vector3 p = cam.transform.position - wind.normalized * 14f;              // de wind blaast door het beeld
            transform.SetPositionAndRotation(p, Quaternion.identity);
        }

        static Mesh MakeGrain()
        {
            // klein facetkorreltje (octaëder), dicht volume: geen dubbelzijdig materiaal nodig
            var v = new List<Vector3> { Vector3.up * .5f, Vector3.down * .5f, Vector3.left * .5f, Vector3.right * .5f,
                Vector3.forward * .35f, Vector3.back * .35f };
            var t = new List<int> { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var mesh = new Mesh { name = "Zandkorrel" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        void OnDestroy()
        {
            if (fogTouched) RenderSettings.fogDensity = baseFog;
            if (material) Destroy(material);
            if (grain) Destroy(grain);
        }
    }
}
