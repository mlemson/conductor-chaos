using UnityEngine;

namespace ConductorChaos.Playground
{
    // Each observer presents the engine state replicated by TrainDrive.
    internal sealed class TrainSteamEffects : MonoBehaviour
    {
        private ParticleSystem smoke;
        private AudioSource starter, engine;
        private AudioClip startupClip, engineClip;
        private bool wasPowered;

        public void Configure(Mesh puff, Material material)
        {
            smoke = gameObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 32;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.65f), new Keyframe(0.58f, 1.55f), new Keyframe(1f, 0f)));
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = puff;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            startupClip = MakeSteamClip(true);
            engineClip = MakeSteamClip(false);
            starter = AddSource();
            engine = AddSource();
            engine.clip = engineClip;
            engine.loop = true;
        }

        private AudioSource AddSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.85f;
            source.minDistance = 2f;
            source.maxDistance = 24f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            return source;
        }

        public void Present(bool powered, float speed)
        {
            speed = Mathf.Clamp01(speed);
            if (powered != wasPowered)
            {
                wasPowered = powered;
                if (powered)
                {
                    smoke.Play();
                    smoke.Emit(3);
                    starter.PlayOneShot(startupClip, 0.32f);
                    engine.PlayDelayed(startupClip.length * 0.65f);
                }
                else
                {
                    smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    starter.Stop();
                    engine.Stop();
                }
            }
            var emission = smoke.emission;
            emission.rateOverTime = Mathf.Lerp(3f, 10f, speed);
            engine.volume = Mathf.Lerp(0.15f, 0.33f, speed);
            engine.pitch = Mathf.Lerp(0.62f, 1.3f, speed);
        }

        private static AudioClip MakeSteamClip(bool start)
        {
            const int sampleRate = 24000;
            var samples = new float[start ? 28800 : 48000];
            var random = new System.Random(start ? 731 : 438);
            float low = 0f, middle = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / sampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += 0.018f * (noise - low);
                middle += 0.15f * (noise - middle);
                float hiss = (middle - low) * 1.4f + noise * 0.14f;
                if (start)
                {
                    float envelope = Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
                    float valve = Mathf.Exp(-25f * Mathf.Abs(time - 0.11f)) * Mathf.Sin(time * 2f * Mathf.PI * 104f);
                    samples[i] = hiss * envelope * 0.9f + valve * 0.18f * envelope;
                }
                else
                {
                    float phase = Mathf.Repeat(time * 2f, 1f);
                    float chuff = Mathf.Sin(phase * Mathf.PI) * Mathf.Exp(-3.5f * phase);
                    float seam = Mathf.Min(1f, Mathf.Min(i, samples.Length - 1 - i) / 180f);
                    samples[i] = seam * (hiss * (0.2f + chuff) + low * (0.25f + chuff) +
                        0.045f * Mathf.Sin(time * 2f * Mathf.PI * 38f));
                }
            }
            var clip = AudioClip.Create(start ? "Steam valve start" : "Steam locomotive loop", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Teardown() => Destroy(gameObject);

        private void OnDestroy()
        {
            if (startupClip) Destroy(startupClip);
            if (engineClip) Destroy(engineClip);
        }
    }
}
