// Conductor Chaos — eendjes (issue #241): zwemmen rustig rondjes rond hun plekje en peddelen weg als een speler of de
// trein dichtbij komt. Ze blijven op het water (StylizedWater onder zich). Puur cosmetisch en lokaal: alle spelers zien
// ongeveer hetzelfde omdat iedereen dezelfde spelersposities heeft, maar het is geen gedeelde staat.
using System.Collections.Generic;
using ConductorChaos.Playground;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class DuckSwim : MonoBehaviour
    {
        [SerializeField, Min(.5f)] float wanderRadius = 3.5f;
        [SerializeField, Min(.05f)] float swimSpeed = .35f;
        [SerializeField, Min(.5f)] float fleeDistance = 4f;
        [SerializeField, Min(.1f)] float fleeSpeed = 1.6f;

        static readonly List<TrainDrive> trains = new();
        static float nextTrainScan;
        const int WaterLayerMask = 1 << 2;        // StylizedWater ligt op Ignore Raycast

        Vector3 home, target;
        float waterY, nextTarget, phase;
        System.Random random;

        void Start()
        {
            home = transform.position;
            waterY = home.y;
            phase = Mathf.Repeat(home.x * 2.1f + home.z * 1.3f, 6.28f);
            random = new System.Random(Mathf.RoundToInt(home.x * 31f + home.z * 17f));
            PickTarget();
        }

        void PickTarget()
        {
            double a = random.NextDouble() * Mathf.PI * 2f, r = wanderRadius * System.Math.Sqrt(random.NextDouble());
            target = home + new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
            nextTarget = Time.time + 6f + (float)random.NextDouble() * 6f;
        }

        static bool OnWater(Vector3 p) =>
            Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, 6f, WaterLayerMask, QueryTriggerInteraction.Collide);

        void Update()
        {
            Vector3 position = transform.position;
            Vector3 flee = Vector3.zero;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player) flee += Away(position, player.transform.position, fleeDistance);
            if (Time.time > nextTrainScan)
            {
                nextTrainScan = Time.time + 2f;
                trains.Clear();
                trains.AddRange(FindObjectsByType<TrainDrive>());
            }
            foreach (var train in trains)
                if (train && train.LocomotiveBody) flee += Away(position, train.LocomotiveBody.position, fleeDistance * 2.5f) * 2f;

            Vector3 direction;
            float speed;
            if (flee.sqrMagnitude > .0004f)
            {
                direction = flee.normalized;
                speed = fleeSpeed;
                nextTarget = 0f;
            }
            else
            {
                Vector3 to = target - position;
                to.y = 0f;
                if (to.magnitude < .3f || Time.time > nextTarget) { PickTarget(); to = target - position; to.y = 0f; }
                direction = to.normalized;
                speed = swimSpeed;
            }
            Vector3 next = position + direction * speed * Time.deltaTime;
            Vector3 offset = next - home;
            offset.y = 0f;
            if (offset.magnitude > wanderRadius * 1.6f) next = home + offset.normalized * wanderRadius * 1.6f;
            if (!OnWater(next)) { next = position; PickTarget(); }
            next.y = waterY + Mathf.Sin(Time.time * 3.1f + phase) * .012f;
            transform.position = next;
            if (direction.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up),
                    Time.deltaTime * (speed > swimSpeed ? 6f : 2.5f));
        }

        static Vector3 Away(Vector3 from, Vector3 threat, float distance)
        {
            Vector3 d = from - threat;
            d.y = 0f;
            float m = d.magnitude;
            return m < distance && m > .01f ? d / m * (1f - m / distance) : Vector3.zero;
        }
    }
}
