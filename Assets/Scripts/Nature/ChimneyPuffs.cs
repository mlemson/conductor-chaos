// Conductor Chaos — rook uit een schoorsteen (houthakkershut, issue #242): langzame grijze wolkjes die opstijgen,
// aanzwellen en vervagen. Puur cosmetisch en lokaal.
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class ChimneyPuffs : MonoBehaviour
    {
        ParticleSystem particles;
        Material material;
        Mesh puff;

        void Start()
        {
            Shader shader = Resources.Load<Shader>("AmbientDust");
            if (!shader) { enabled = false; return; }
            material = new Material(shader) { name = "Schoorsteenrook" };
            puff = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, .8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .55f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(178, 151, 184, 255), new Color32(210, 188, 208, 255));
            main.maxParticles = 40;
            var emission = particles.emission;
            emission.rateOverTime = 2.2f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = .12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .4f), new Keyframe(.5f, 1.6f), new Keyframe(1f, 0f)));
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.15f, .35f);   // een zuchtje wind
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = puff;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
        }
    }
}
