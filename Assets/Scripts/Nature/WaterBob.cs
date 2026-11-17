// Conductor Chaos — dobberen op het water (issue #241): bootjes, eendjes en waterlelies deinen zachtjes op en neer en
// wiegen een beetje. Puur cosmetisch en lokaal (geen botsvormen die meebewegen; geen gedeelde staat).
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class WaterBob : MonoBehaviour
    {
        [Tooltip("Hoe ver het op en neer gaat (m).")]
        [SerializeField, Min(0f)] float height = .04f;
        [Tooltip("Duur van één keer op en neer (s).")]
        [SerializeField, Min(.2f)] float period = 2.8f;
        [Tooltip("Hoeveel graden het wiegt.")]
        [SerializeField, Min(0f)] float roll = 3f;

        Vector3 basePosition;
        Quaternion baseRotation;
        float phase;

        public void Configure(float bobHeight, float bobPeriod, float rollDegrees)
        {
            height = bobHeight;
            period = bobPeriod;
            roll = rollDegrees;
        }

        void OnEnable()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            // vaste fase uit de plek: iedereen ziet hetzelfde, buren deinen niet gelijk op
            phase = Mathf.Repeat(basePosition.x * 1.37f + basePosition.z * .71f, Mathf.PI * 2f);
        }

        void OnDisable()
        {
            transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
        }

        void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + phase;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t) * height);
            transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t * .73f) * roll, 0f, Mathf.Cos(t * .91f) * roll);
        }
    }
}
