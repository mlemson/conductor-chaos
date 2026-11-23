using ConductorChaos.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    internal sealed class ServiceNoticeboard : MonoBehaviour
    {
        private const float FaceWidth = 3.36f;
        private const float FaceHeight = 1.42f;
        private const float FaceMiddle = 1.43f;
        private const float ShutterTop = FaceMiddle + FaceHeight * .29f;
        private const float ShutterHeight = FaceHeight * .76f;
        private readonly Material[] materials = new Material[4];
        private ServiceRun run;
        private ManifestBoard cards;
        private Transform shutter;
        private bool readable;
        private float cover = 1f;

        internal static ServiceNoticeboard Place(ServiceRun service)
        {
            var station = service.Departure;
            if (!station || !station.Platform || !station.TrainStop) return null;

            FindSpot(station, out var place, out var rotation, out var surface);
            var board = new GameObject("Dienstbord · vertrekperron").AddComponent<ServiceNoticeboard>();
            SceneManager.MoveGameObjectToScene(board.gameObject, station.gameObject.scene);
            board.transform.SetPositionAndRotation(place, rotation);
            board.Build(service, surface);
            return board;
        }

        private static void FindSpot(ServiceStation station, out Vector3 place, out Quaternion rotation, out Material surface)
        {
            var platform = station.Platform;
            var frame = platform.transform;
            bool longX = platform.size.x * frame.lossyScale.x >= platform.size.z * frame.lossyScale.z;
            var along = Vector3.ProjectOnPlane(longX ? frame.right : frame.forward, Vector3.up).normalized;
            var center = frame.TransformPoint(platform.center);
            var track = station.TrainStop.transform.TransformPoint(station.TrainStop.center);
            var inward = Vector3.Cross(Vector3.up, along);
            if (Vector3.Dot(center - track, inward) < 0f) inward = -inward;
            rotation = Quaternion.LookRotation(inward);
            float halfDepth = (longX ? platform.size.z * frame.lossyScale.z : platform.size.x * frame.lossyScale.x) * .5f;
            float halfLength = (longX ? platform.size.x * frame.lossyScale.x : platform.size.z * frame.lossyScale.z) * .5f;
            var edge = center + inward * Mathf.Max(.4f, halfDepth - .55f);
            float end = Mathf.Max(0f, halfLength - 2.2f);
            float crew = station.CrewSpawn ? Vector3.Dot(station.CrewSpawn.position - center, along) : 0f;
            float nearCrew = Mathf.Clamp(crew + (crew > 0f ? -4f : 4f), -end, end);
            var physics = station.gameObject.scene.GetPhysicsScene();
            var hits = new Collider[24];
            Physics.SyncTransforms();
            place = edge - Vector3.up * .65f;
            bool sampledFloor = false;
            surface = null;
            foreach (float offset in new[] { nearCrew, nearCrew + 4.25f, nearCrew - 4.25f, end, -end, 0f })
            {
                var candidate = edge + along * Mathf.Clamp(offset, -end, end);
                if (!physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out var floor, 4.5f, ~0, QueryTriggerInteraction.Ignore) ||
                    floor.rigidbody || floor.normal.y < .8f || !ServiceStation.Contains(platform, floor.point + Vector3.up * .35f))
                    continue;
                var mesh = floor.collider.GetComponentInParent<MeshRenderer>();
                if (!sampledFloor)
                {
                    place = floor.point;
                    surface = mesh ? mesh.sharedMaterial : null;
                    sampledFloor = true;
                }

                int count = physics.OverlapBox(floor.point + Vector3.up * 1.14f - inward * .32f,
                    new Vector3(1.85f, 1.02f, .50f), hits, rotation, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < count; i++)
                    if (!hits[i].attachedRigidbody) { blocked = true; break; }
                if (blocked) continue;
                place = floor.point;
                surface = mesh ? mesh.sharedMaterial : surface;
                return;
            }
        }

        private void Build(ServiceRun service, Material surface)
        {
            run = service;
            var shader = surface && surface.HasProperty("_BaseColor") ? surface.shader : Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader)
            {
                materials[0] = Finish(shader, "#AC6E43");
                materials[1] = Finish(shader, "#C98E52");
                materials[2] = Finish(shader, "#353951");
                materials[3] = Finish(shader, "#B88D52");
            }

            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 1.77f;
                Box("Staander", transform, new Vector3(x, 1.055f, .02f), new Vector3(.17f, 2.11f, .22f), 0, true);
                Box("Stalen voet", transform, new Vector3(x, .065f, .02f), new Vector3(.30f, .13f, .36f), 2);
                Box("Staanderkop", transform, new Vector3(x, 2.08f, .02f), new Vector3(.22f, .10f, .26f), 1);
                Box("Schoor", transform, new Vector3(x - side * .15f, .78f, .025f), new Vector3(.10f, .43f, .11f), 1)
                    .localRotation = Quaternion.Euler(0f, 0f, -side * 42f);
            }
            Box("Bordpaneel", transform, new Vector3(0f, FaceMiddle, .01f), new Vector3(3.48f, FaceHeight, .15f), 2, true);
            Box("Bovenlijst", transform, new Vector3(0f, 2.16f, -.01f), new Vector3(3.73f, .12f, .24f), 0);
            Box("Onderlijst", transform, new Vector3(0f, .69f, -.01f), new Vector3(3.65f, .08f, .21f), 1);
            Box("Rolluikcassette", transform, new Vector3(0f, ShutterTop + .055f, -.105f), new Vector3(3.45f, .10f, .14f), 2);

            shutter = new GameObject("Rolluik").transform;
            shutter.SetParent(transform, false);
            shutter.localPosition = new Vector3(0f, ShutterTop, -.135f);
            Box("Luik", shutter, new Vector3(0f, -ShutterHeight * .5f, 0f), new Vector3(3.40f, ShutterHeight, .058f), 2);
            for (int i = 1; i <= 7; i++)
                Box("Lamellen", shutter, new Vector3(0f, -ShutterHeight * i / 8f, -.033f), new Vector3(3.39f, .011f, .012f), 0);
            Box("Sluitlat", shutter, new Vector3(0f, -ShutterHeight, -.018f), new Vector3(3.42f, .028f, .04f), 3);

            var face = new GameObject("Affiches", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)face.transform;
            rect.SetParent(transform, false);
            rect.localPosition = new Vector3(0f, FaceMiddle, -.069f);
            rect.localScale = Vector3.one * .004f;
            rect.sizeDelta = new Vector2(FaceWidth, FaceHeight) / .004f;
            var canvas = face.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var title = ChaosUI.Label(rect, "Bordtitel", "DIENSTBORD", 36, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold,
                new Vector2(.10f, .82f), new Vector2(.90f, .986f));
            title.raycastTarget = false;
            cards = new ManifestBoard(rect);
        }

        private static Material Finish(Shader shader, string hex)
        {
            Color tint = ChaosUI.Hex(hex);
            var material = new Material(shader) { color = tint };
            if (material.HasProperty("_Ramp0"))
            {
                material.SetColor("_Ramp0", Color.Lerp(tint, Color.black, .46f));
                material.SetColor("_Ramp1", Color.Lerp(tint, Color.black, .22f));
                material.SetColor("_Ramp2", tint);
                material.SetColor("_Ramp3", Color.Lerp(tint, ChaosUI.Paper, .17f));
                material.SetColor("_Ramp4", Color.Lerp(tint, ChaosUI.Paper, .28f));
                material.SetFloat("_Spec", .16f);
                material.SetFloat("_Cells", .28f);
                material.SetFloat("_Grime", .15f);
            }
            else if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .13f);
            return material;
        }

        private Transform Box(string label, Transform parent, Vector3 position, Vector3 size, int material, bool solid = false)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<BoxCollider>().enabled = solid;
            var mesh = box.GetComponent<MeshRenderer>();
            if (materials[material]) mesh.sharedMaterial = materials[material];
            mesh.shadowCastingMode = ShadowCastingMode.On;
            return box.transform;
        }

        private void Update()
        {
            if (!shutter) return;
            bool open = run && run.IsSpawned && run.Manifest.IsReady &&
                run.Phase is ServiceRun.RunPhase.Briefing or ServiceRun.RunPhase.Departure;
            if (open != readable)
            {
                readable = open;
                if (readable) cards.Show(run.Manifest, run.PickupStation);
                else cards.Hide();
            }

            float target = open ? 0f : 1f;
            if (cover == target) return;
            cover = Mathf.MoveTowards(cover, target, Time.deltaTime * 2.3f);
            shutter.gameObject.SetActive(cover > .001f);
            shutter.localScale = new Vector3(1f, Mathf.Max(cover, .001f), 1f);
        }

        private void OnDestroy()
        {
            foreach (var material in materials)
                if (material) Destroy(material);
        }
    }
}
