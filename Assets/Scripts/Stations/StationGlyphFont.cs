using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// De ronde bordletters uit ArtSource (StationGlyphs.fbx): per teken een mesh met kaphoogte 1 en diepte 1,
    /// plus de letterbreedte. Wordt door de editor gemaakt (Conductor Chaos > Art > Station) en door
    /// <see cref="StationSign"/> gebruikt om een naam op een stationsbord te zetten. Geen fontbestand nodig.
    /// </summary>
    public sealed class StationGlyphFont : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public string character;
            public float advance;
            public Mesh mesh;
        }

        [Tooltip("Ruimte tussen twee letters (in kaphoogtes).")]
        public float spacing = 0.30f;
        [Tooltip("Breedte van een spatie (in kaphoogtes).")]
        public float space = 0.50f;
        [Tooltip("+1 of -1: richting van de X-as van de letters na de FBX-import (de import spiegelt X).")]
        public float xSign = -1f;
        public List<Glyph> glyphs = new List<Glyph>();

        Dictionary<char, Glyph> map;

        public bool TryGet(char c, out Glyph glyph)
        {
            if (map == null || map.Count != glyphs.Count)
            {
                map = new Dictionary<char, Glyph>();
                foreach (var g in glyphs)
                    if (!string.IsNullOrEmpty(g.character) && g.mesh) map[g.character[0]] = g;
            }
            return map.TryGetValue(c, out glyph);
        }

        public bool Supports(char c) => c == ' ' || TryGet(c, out _);

        void OnValidate() => map = null;
    }
}
