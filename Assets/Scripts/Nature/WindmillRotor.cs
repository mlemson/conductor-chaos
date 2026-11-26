// Conductor Chaos — windmolen in de woestijn (levels 6-7): de rotor draait langzaam en wisselt mee met de wind.
// Puur cosmetisch en lokaal; geen netwerkstatus. Model: ArtSource/Environment/Desert/build_desert_kit.py (knoop Rotor).
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class WindmillRotor : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 80f;

        Transform rotor;
        Quaternion rest;
        Vector3 axis;
        float angle, seed;

        void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Rotor") { rotor = t; break; }
            if (!rotor) { enabled = false; return; }
            rest = rotor.localRotation;
            axis = rotor.parent.InverseTransformDirection(transform.forward);     // de naaf wijst naar +Z van het model
            seed = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 10f;
            angle = seed * 36f;
        }

        void Update()
        {
            float gust = 0.75f + 0.25f * Mathf.Sin(Time.time * 0.31f + seed) + 0.1f * Mathf.Sin(Time.time * 1.7f + seed * 2f);
            angle = (angle + degreesPerSecond * gust * Time.deltaTime) % 360f;
            rotor.localRotation = Quaternion.AngleAxis(angle, axis) * rest;
        }
    }
}
