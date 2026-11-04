// Conductor Chaos — één spoorstuk uit de spoorkit (issue #53).
// De modellen (ArtSource/Environment/build_track_kit.py) hebben lege nodes Socket_Start/Socket_End plus een punt
// 1 m verder in rijrichting (*_Fwd). Daaruit leidt dit component begin, eind, richting en de hartlijn af, los van
// hoe Unity de assen van het FBX-bestand omzet. Recht stuk = lijn, bocht = cirkelboog door begin en eind.
// Hoogte: de sockets liggen op de bovenkant van de rails. Een trein staat goed met zijn pivot RailToTrainPivot
// boven die hoogte (de treinmodellen hebben hun railhoogte op y = -0,62).
// Stukken met hoogteverloop (helling, bult) hebben extra punten Path_00..Path_12; dan volgt Evaluate die.
// Alleen gegevens en gizmo's; de treinbeweging (#55) kan Evaluate gebruiken.
using UnityEngine;

namespace ConductorChaos.Track
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TrackPiece : MonoBehaviour
    {
        /// <summary>Afstand van bovenkant rail tot de pivot van de treinmodellen (m).</summary>
        public const float RailToTrainPivot = 0.62f;

        public Transform start;
        public Transform startForward;
        public Transform end;
        public Transform endForward;

        [Tooltip("Punten langs de hartlijn (Path_00..Path_12) voor stukken met hoogteverloop: helling, bult (#136).")]
        public Transform[] pathPoints;

        [Tooltip("Volgend stuk (optioneel, wordt gezet door de Track Builder). Handig voor een route (#54).")]
        public TrackPiece next;

        public bool HasSockets => start != null && startForward != null && end != null && endForward != null;

        public Vector3 StartPosition => start.position;
        public Vector3 StartDirection => (startForward.position - start.position).normalized;
        public Vector3 EndPosition => end.position;
        public Vector3 EndDirection => (endForward.position - end.position).normalized;

        void Reset() => FindSockets();

        void OnEnable()
        {
            if (!HasSockets) FindSockets();
        }

        [ContextMenu("Find Sockets")]
        public void FindSockets()
        {
            start = FindDeep(transform, "Socket_Start");
            startForward = FindDeep(transform, "Socket_Start_Fwd");
            end = FindDeep(transform, "Socket_End");
            endForward = FindDeep(transform, "Socket_End_Fwd");
            var pts = new System.Collections.Generic.List<Transform>();
            for (int k = 0; k < 100; k++)
            {
                var t = FindDeep(transform, "Path_" + k.ToString("00"));
                if (t == null) break;
                pts.Add(t);
            }
            pathPoints = pts.Count >= 2 ? pts.ToArray() : null;
        }

        bool HasPath
        {
            get
            {
                if (pathPoints == null || pathPoints.Length < 2) return false;
                foreach (var p in pathPoints) if (p == null) return false;
                return true;
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>Hoek van de bocht in graden (0 = recht), positief = naar rechts.</summary>
        public float TurnDegrees => HasSockets ? Vector3.SignedAngle(StartDirection, EndDirection, transform.up) : 0f;

        /// <summary>Lengte langs de hartlijn (m).</summary>
        public float Length
        {
            get
            {
                if (!HasSockets) return 0f;
                if (HasPath)
                {
                    float sum = 0f;
                    for (int k = 1; k < pathPoints.Length; k++) sum += Vector3.Distance(pathPoints[k - 1].position, pathPoints[k].position);
                    return sum;
                }
                Arc(out var center, out var axis, out float angle, out float radius);
                return angle < 1e-4f ? Vector3.Distance(StartPosition, EndPosition) : radius * angle;
            }
        }

        /// <summary>Positie en rijrichting op fractie t (0 = begin, 1 = eind) van de hartlijn, op railhoogte.</summary>
        public void Evaluate(float t, out Vector3 position, out Vector3 direction)
        {
            t = Mathf.Clamp01(t);
            if (HasPath)
            {
                // langs de padpunten (gelijk verdeeld over de booglengte van het model)
                float f = t * (pathPoints.Length - 1);
                int k = Mathf.Min(Mathf.FloorToInt(f), pathPoints.Length - 2);
                var a = pathPoints[k].position;
                var b = pathPoints[k + 1].position;
                position = Vector3.Lerp(a, b, f - k);
                direction = t <= 0f ? StartDirection : t >= 1f ? EndDirection : (b - a).normalized;
                return;
            }
            Arc(out var center, out var axis, out float angle, out _);
            if (angle < 1e-4f)
            {
                position = Vector3.Lerp(StartPosition, EndPosition, t);
                direction = (EndPosition - StartPosition).normalized;
                return;
            }
            var rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * t, axis);
            position = center + rot * (StartPosition - center);
            direction = rot * StartDirection;
        }

        // cirkelboog door het begin (met de beginrichting als raaklijn) en het eind
        void Arc(out Vector3 center, out Vector3 axis, out float angle, out float radius)
        {
            var p0 = StartPosition;
            var f0 = StartDirection;
            var chord = EndPosition - p0;
            var side = chord - f0 * Vector3.Dot(chord, f0);
            center = p0;
            axis = transform.up;
            radius = 0f;
            angle = 0f;
            if (side.magnitude < 1e-3f || chord.magnitude < 1e-3f) return;   // recht
            float half = Vector3.Angle(f0, chord) * Mathf.Deg2Rad;
            radius = chord.magnitude / (2f * Mathf.Sin(half));
            center = p0 + side.normalized * radius;
            axis = Vector3.Cross(f0, side.normalized).normalized;
            angle = 2f * half;
        }

        void OnDrawGizmos()
        {
            if (!HasSockets) return;
            Gizmos.color = new Color(1f, 0.82f, 0.25f, 0.9f);
            Vector3 prev = StartPosition;
            const int steps = 16;
            for (int i = 1; i <= steps; i++)
            {
                Evaluate(i / (float)steps, out var p, out _);
                Gizmos.DrawLine(prev + Vector3.up * 0.05f, p + Vector3.up * 0.05f);
                prev = p;
            }
            Gizmos.color = new Color(0.35f, 0.9f, 0.45f, 0.9f);
            Gizmos.DrawSphere(StartPosition, 0.12f);
            Gizmos.color = new Color(0.95f, 0.35f, 0.3f, 0.9f);
            Gizmos.DrawSphere(EndPosition, 0.12f);
            Gizmos.DrawLine(EndPosition, EndPosition + EndDirection * 0.8f);
        }
    }
}
