using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Playground
{
    // Cosmetic particles are generated locally; the server has no particle state to synchronize.
    internal sealed class AmbientDust : MonoBehaviour
    {
        private ParticleSystem particles;
        private Material material;
        private Texture2D texture;

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
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Ambient dust glow", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    float dx = (x + .5f) / texture.width * 2f - 1f;
                    float dy = (y + .5f) / texture.height * 2f - 1f;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            texture.Apply(false, true);
            material = new Material(shader) { name = "Ambient dust material" };
            material.mainTexture = texture;
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .075f);
            main.startColor = new Color(1f, .91f, .69f, .35f);
            main.maxParticles = 70;
            var emission = particles.emission;
            emission.rateOverTime = 5f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 5f, 12f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.10f, .10f);
            velocity.y = new ParticleSystem.MinMaxCurve(.015f, .075f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.10f, .10f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(1f, .8f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (Camera.main) transform.position = Camera.main.transform.position;
            particles.Play();
        }

        private void LateUpdate()
        {
            if (Camera.main) transform.position = Camera.main.transform.position;
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
            if (texture) Destroy(texture);
        }
    }
}
