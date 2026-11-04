using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Runtime references to the approved sprites under Assets/Art/UI/Icons.
    /// The asset in Resources stores GUID references, not copies of the PNGs.
    /// Missing sprites fall back to the existing simple vector pictograms.
    /// </summary>
    [CreateAssetMenu(fileName = "ChaosUISpriteCatalog", menuName = "Conductor Chaos/UI Sprite Catalog")]
    public sealed class ChaosUISpriteCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private static ChaosUISpriteCatalog loaded;
        private Dictionary<string, Sprite> lookup;

        public static Sprite Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!loaded) loaded = Resources.Load<ChaosUISpriteCatalog>("ChaosUISpriteCatalog");
            if (!loaded) return null;
            if (loaded.lookup == null) loaded.BuildLookup();
            return loaded.lookup.TryGetValue(key, out Sprite sprite) ? sprite : null;
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (entries == null) return;
            foreach (Entry item in entries)
                if (!string.IsNullOrEmpty(item.key) && item.sprite)
                    lookup[item.key] = item.sprite;
        }

        // Used only by the optional Editor validation/rebuild menu item.
        public void SetEntries(Entry[] newEntries)
        {
            entries = newEntries ?? Array.Empty<Entry>();
            lookup = null;
        }

        public static Graphic Add(Transform parent, string name, string key,
            Vector2 min, Vector2 max, Color fallbackColor)
        {
            Sprite sprite = Find(key);
            if (sprite)
            {
                RectTransform rect = ChaosUI.Rect(parent, name, min, max, Vector2.zero, Vector2.zero);
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;  // preserve the authored icon's colors
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;  // never block a parent button
                return image;
            }

            // The menu must remain usable if a sprite is missing or not yet imported.
            return ChaosUIIcon.Add(parent, name + " (vector fallback)", FallbackSymbol(key),
                min, max, fallbackColor);
        }

        private static ChaosUIIcon.Symbol FallbackSymbol(string key)
        {
            switch (key)
            {
                case "play": return ChaosUIIcon.Symbol.Play;
                case "door": return ChaosUIIcon.Symbol.Door;
                case "key": return ChaosUIIcon.Symbol.Key;
                case "ticket": return ChaosUIIcon.Symbol.Ticket;
                case "wrench":
                case "upgrades": return ChaosUIIcon.Symbol.Wrench;
                case "warning": return ChaosUIIcon.Symbol.Warning;
                case "options":
                case "controls": return ChaosUIIcon.Symbol.Settings;
                case "player":
                case "player-group":
                case "invite": return ChaosUIIcon.Symbol.Person;
                default: return ChaosUIIcon.Symbol.Train;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => loaded = null;
    }
}
