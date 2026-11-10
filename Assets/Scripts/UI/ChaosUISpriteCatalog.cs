using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Uses the supplied transparent icon sheet, with the imported individual sprites as fallbacks.
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
            foreach (Entry item in entries ?? Array.Empty<Entry>())
                if (!string.IsNullOrEmpty(item.key) && item.sprite)
                    lookup[item.key] = item.sprite;

            Texture2D sheet = Resources.Load<Texture2D>("ChaosUIIcons");
            if (!sheet) return;

            const int sourceWidth = 1448;
            const int sourceHeight = 1086;
            float scaleX = (float)sheet.width / sourceWidth;
            float scaleY = (float)sheet.height / sourceHeight;
            Slice("ticket", 96, 16, 323, 178);
            Slice("wrench", 381, 12, 569, 176);
            Slice("key", 640, 12, 809, 176);
            Slice("train", 868, 14, 1087, 176);
            Slice("coal", 1133, 28, 1344, 178);
            Slice("speed-gauge", 93, 252, 331, 403);
            Slice("warning", 384, 249, 569, 408);
            Slice("chat", 624, 260, 820, 402);
            Slice("player", 897, 246, 1061, 407);
            Slice("lantern", 1156, 240, 1322, 408);
            Slice("door", 127, 469, 298, 630);
            Slice("play", 377, 475, 576, 628);
            Slice("options", 637, 478, 823, 627);
            Slice("close", 884, 478, 1066, 628);
            Slice("back", 1140, 478, 1330, 628);
            Slice("audio", 117, 703, 312, 826);
            Slice("controls", 372, 701, 572, 826);
            Slice("accessibility", 618, 701, 829, 826);
            Slice("route", 877, 703, 1081, 826);
            Slice("upgrades", 1132, 702, 1338, 826);
            Slice("player-group", 155, 892, 391, 1022);
            Slice("invite", 461, 892, 689, 1021);
            Slice("ready", 763, 891, 976, 1022);
            Slice("quit", 1058, 890, 1277, 1021);

            // Coordinates use the source sheet's top-left origin and stop before each caption plaque.
            void Slice(string key, int left, int top, int right, int bottom)
            {
                var rect = new Rect(left * scaleX, (sourceHeight - bottom) * scaleY,
                    (right - left) * scaleX, (bottom - top) * scaleY);
                Debug.Assert(left >= 0 && top >= 0 && right <= sourceWidth && bottom <= sourceHeight &&
                    right > left && bottom > top, "Invalid UI icon bounds for " + key);
                Sprite sprite = Sprite.Create(sheet, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = key;
                lookup[key] = sprite;
            }
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
                case "back": return ChaosUIIcon.Symbol.Back;
                case "audio": return ChaosUIIcon.Symbol.Audio;
                case "quit": return ChaosUIIcon.Symbol.Quit;
                case "route": return ChaosUIIcon.Symbol.Route;
                case "chat": return ChaosUIIcon.Symbol.Chat;
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
