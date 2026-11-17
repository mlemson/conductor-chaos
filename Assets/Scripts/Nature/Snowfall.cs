// Conductor Chaos — vallende sneeuw (issue #242): een wolk vlokjes rond de camera van deze speler. Puur cosmetisch en
// lokaal. Staat in winterlevels als component op een leeg object (de levelbouwer zet het neer).
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class Snowfall : MonoBehaviour
    {
        [SerializeField, Min(0f)] float flakesPerSecond = 140f;
        [SerializeField] Vector3 area = new(36f, 1f, 36f);
        [SerializeField, Min(1f)] float heightAboveCamera = 11f;

        ParticleSystem particles;
        Material material;
        Mesh flake;

        void Start()
        {
            Shader shader = Resources.Load<Shader>("AmbientDust");
            if (!shader) { Debug.LogWarning("Sneeuw: shader AmbientDust ontbreekt."); enabled = false; return; }
            material = new Material(shader) { name = "Sneeuwvlokjes" };
            flake = MakeFlake();
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(253, 242, 235, 255), new Color32(223, 221, 238, 255));
            main.startRotation3D = true;
            var angle = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationX = main.startRotationY = main.startRotationZ = angle;
            main.maxParticles = 1600;
            var emission = particles.emission;
            emission.rateOverTime = flakesPerSecond;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            shape.rotation = new Vector3(90f, 0f, 0f);       // de doos 'schiet' omlaag
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = .45f;
            noise.frequency = .25f;
            noise.scrollSpeed = .2f;
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.lifetimeLoss = 1f;                      // landt en is weg
            collision.collidesWith = ~(1 << 2);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = flake;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam || !particles) return;
            Vector3 p = cam.transform.position;
            transform.position = new Vector3(p.x, p.y + heightAboveCamera, p.z);
            transform.rotation = Quaternion.identity;
        }

        static Mesh MakeFlake()
        {
            // klein plat zeshoekje met dikte (geen dubbelzijdig materiaal nodig)
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                float y = side == 0 ? .08f : -.08f;
                int c = v.Count;
                v.Add(new Vector3(0f, y, 0f));
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    v.Add(new Vector3(Mathf.Cos(a) * .5f, y * .5f, Mathf.Sin(a) * .5f));
                }
                for (int k = 0; k < 6; k++)
                {
                    int a = c + 1 + k, b = c + 1 + (k + 1) % 6;
                    if (side == 0) { t.Add(c); t.Add(b); t.Add(a); }
                    else { t.Add(c); t.Add(a); t.Add(b); }
                }
            }
            var mesh = new Mesh { name = "Sneeuwvlok" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
            if (flake) Destroy(flake);
        }
    }
}
