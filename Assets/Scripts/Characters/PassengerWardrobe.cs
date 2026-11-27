// Conductor Chaos — reizigerslooks: per reizigerstype extra looks (herkleuring + eigen haar/hoed) op hetzelfde model.
// Gebouwd door ArtSource/Characters/PassengerLooks/build_passenger_looks.py; gevuld door
// Conductor Chaos > Passengers > Build Passenger Wardrobe (Resources/PassengerWardrobe.asset).
// Puur presentatie: welke look een reiziger draagt bepaalt de server (PlaygroundPassenger), iedereen ziet dezelfde.
using UnityEngine;

namespace ConductorChaos.Characters
{
    [CreateAssetMenu(menuName = "Conductor Chaos/Passenger Wardrobe", fileName = "PassengerWardrobe")]
    public sealed class PassengerWardrobe : ScriptableObject
    {
        [System.Serializable]
        public struct Look
        {
            public string label;
            [Tooltip("Onderdelen van het basismodel die verdwijnen (bv. Hat).")]
            public string[] hide;
            [Tooltip("Body/Look_*-knopen uit het looks-model die erbij komen.")]
            public string[] add;
            public Material[] from;
            public Material[] to;
        }

        [System.Serializable]
        public struct Kind
        {
            [Tooltip("Materiaalvoorvoegsel van het basismodel, bv. M_Tourist_.")]
            public string materialPrefix;
            public GameObject looksModel;
            public Look[] looks;
        }

        [SerializeField] private Kind[] kinds = System.Array.Empty<Kind>();

        private static PassengerWardrobe cached;
        public static PassengerWardrobe Load() => cached ? cached : cached = Resources.Load<PassengerWardrobe>("PassengerWardrobe");

        public void Configure(Kind[] list) => kinds = list;

        /// <summary>Welk type is dit model (op het lijfmateriaal)? -1 als het geen bekende reiziger is.</summary>
        public int KindOf(Transform model)
        {
            Transform body = Find(model, "Body");
            if (!body || !body.TryGetComponent<Renderer>(out var renderer) || !renderer.sharedMaterial) return -1;
            string name = renderer.sharedMaterial.name;
            for (int i = 0; i < kinds.Length; i++)
                if (!string.IsNullOrEmpty(kinds[i].materialPrefix) && name.StartsWith(kinds[i].materialPrefix)) return i;
            return -1;
        }

        public int LookCount(int kind) => kind >= 0 && kind < kinds.Length ? kinds[kind].looks.Length : 0;

        /// <summary>Kleed het model in look <paramref name="look"/> (1..LookCount; 0 = basismodel, doet niets).</summary>
        public void Dress(Transform model, int kind, int look)
        {
            if (look <= 0 || look > LookCount(kind)) return;
            Kind k = kinds[kind];
            Look l = k.looks[look - 1];
            Transform body = Find(model, "Body");
            Transform source = k.looksModel ? Find(k.looksModel.transform, "Body") : null;
            if (!body || !source) { Debug.LogWarning("PassengerWardrobe: Body of looks-model ontbreekt.", model); return; }
            foreach (string node in l.hide)
                if (Find(body, node) is { } part) part.gameObject.SetActive(false);
            foreach (string node in l.add)
            {
                Transform template = source.Find(node);
                if (!template) { Debug.LogWarning("PassengerWardrobe: look-onderdeel ontbreekt: " + node, model); continue; }
                GameObject copy = Instantiate(template.gameObject, body, false);
                copy.name = template.name;
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    int swap = System.Array.IndexOf(l.from, materials[i]);
                    if (swap < 0 || !l.to[swap]) continue;
                    materials[i] = l.to[swap];
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static Transform Find(Transform root, string node)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == node) return item;
            return null;
        }
    }
}
