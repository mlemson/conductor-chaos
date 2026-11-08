using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Playground
{
    // Cosmetic particles are generated locally; the server has no particle state to synchronize.
    internal sealed class AmbientDust : MonoBehaviour
    {
        private ParticleSystem particles;
        private Material material;
        private Mesh flake;
        private PlaygroundPlayer player;
        private float nextEmission;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isBatchMode || SceneManager.GetActiveScene().name != "TrainPlayground") return;
            new GameObject("Ambient dust").AddComponent<AmbientDust>();
        }

        private void Start()
        {
            Shader shader = Resources.Load<Shader>("AmbientDust");
            if (!shader) { Debug.LogWarning("Ambient dust shader not found."); enabled = false; return; }
            material = new Material(shader) { name = "Ambient dust material" };
            flake = CreateFlake();
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 10f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.023f, .047f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color32(254, 242, 176, 255), new Color32(255, 227, 188, 255));
            main.startRotation3D = true;
            var angle = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationX = main.startRotationY = main.startRotationZ = angle;
            main.maxParticles = 90;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(.18f, 1f), new Keyframe(.8f, 1f), new Keyframe(1f, 0f)));
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = .4f;

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

        private void LateUpdate()
        {
            if (Time.time < nextEmission) return;
            nextEmission = Time.time + .15f;

            if (!player || !player.IsSpawned || !player.IsOwner)
            {
                player = null;
                foreach (var candidate in FindObjectsByType<PlaygroundPlayer>())
                    if (candidate.IsSpawned && candidate.IsOwner)
                    {
                        player = candidate;
                        break;
                    }
            }

            var camera = Camera.main;
            if (!player || !camera) return;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(Random.Range(4f * 4f, 11f * 11f));
                var position = player.transform.position + new Vector3(
                    Mathf.Cos(angle) * radius, Random.Range(.4f, 3.4f), Mathf.Sin(angle) * radius);
                if ((position - camera.transform.position).sqrMagnitude < 3f * 3f) continue;

                // Keep births out of the near view; distant flakes can ease in while the player stands still.
                var view = camera.WorldToViewportPoint(position);
                if (radius < 9f && view.z > 0f && view.x > -.05f && view.x < 1.05f && view.y > -.05f && view.y < 1.05f)
                    continue;

                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Random.Range(-.17f, .17f), Random.Range(.02f, .09f), Random.Range(-.17f, .17f))
                }, 1);
                break;
            }
        }

        private static Mesh CreateFlake()
        {
            var points = new[]
            {
                new Vector3(0f, .68f, 0f), new Vector3(0f, -.47f, 0f),
                new Vector3(.42f, 0f, 0f), new Vector3(0f, .08f, .35f),
                new Vector3(-.49f, -.05f, 0f), new Vector3(0f, 0f, -.43f)
            };
            int[] faces = { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            var vertices = new Vector3[faces.Length];
            var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                vertices[i] = points[faces[i]];
                triangles[i] = i;
            }

            var mesh = new Mesh { name = "Faceted ambient flake", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
            if (flake) Destroy(flake);
        }
    }
}
