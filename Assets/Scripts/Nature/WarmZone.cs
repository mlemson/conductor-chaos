// Conductor Chaos — warme plek (issue #243): een stationshal, een hut met kachel, de cabine bij de vuurkist. Een reiziger
// binnen deze doos warmt op en ontdooit. Alleen de server gebruikt dit; de doos zelf is een trigger zonder botsing.
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class WarmZone : MonoBehaviour
    {
        static readonly List<WarmZone> zones = new();
        BoxCollider box;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void OnEnable() => zones.Add(this);
        void OnDisable() => zones.Remove(this);

        public static bool Contains(Vector3 world)
        {
            foreach (var zone in zones)
            {
                if (!zone || !zone.box) continue;
                Vector3 local = zone.transform.InverseTransformPoint(world) - zone.box.center;
                Vector3 half = zone.box.size * .5f;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z) return true;
            }
            return false;
        }
    }
}
