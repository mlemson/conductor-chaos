using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Tiny flat vector icon set for first UI passes. No emoji, shadows, fonts or textures.
    /// Intentionally few shapes; replace with approved sprites once the game icon set exists.
    /// </summary>
    public sealed class ChaosUIIcon : MaskableGraphic
    {
        public enum Symbol { Play, Train, Settings, Door, Ticket, Wrench, Key, Warning, Person, Back, Audio, Quit, Route, Chat }

        [SerializeField] public Symbol symbol;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float size = Mathf.Min(r.width, r.height);
            float scale = size / 100f;
            Vector2 origin = r.center;
            // Coordinates live in a square 100-unit design grid.
            switch (symbol)
            {
                case Symbol.Play:
                    Triangle(vh, P(-23, -31), P(-23, 31), P(33, 0));
                    break;
                case Symbol.Train:
                    Box(vh, -29, -18, 58, 47);
                    Box(vh, -25, 29, 50, 7);
                    Box(vh, -18, -8, 15, 18, clear: true);
                    Box(vh, 6, -8, 15, 18, clear: true);
                    Circle(vh, -19, -27, 9);
                    Circle(vh, 19, -27, 9);
                    break;
                case Symbol.Settings:
                    Ring(vh, 0, 0, 27, 13);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(vh, dir * 25f, dir * 38f, 10f);
                    }
                    break;
                case Symbol.Door:
                    RingRect(vh, -26, -39, 52, 78, 6);
                    Circle(vh, 14, -1, 5);
                    break;
                case Symbol.Ticket:
                    RingRect(vh, -38, -23, 76, 46, 6);
                    Line(vh, new Vector2(10, -17), new Vector2(10, 17), 3);
                    break;
                case Symbol.Wrench:
                    Line(vh, new Vector2(-30, -32), new Vector2(23, 22), 13);
                    Ring(vh, 25, 25, 16, 8);
                    Circle(vh, -32, -34, 8);
                    break;
                case Symbol.Key:
                    Ring(vh, -18, 16, 21, 9);
                    Line(vh, new Vector2(-4, 4), new Vector2(35, -35), 11);
                    Box(vh, 25, -43, 9, 19);
                    Box(vh, 35, -34, 8, 12);
                    break;
                case Symbol.Warning:
                    Line(vh, new Vector2(-34, -32), new Vector2(0, 35), 8);
                    Line(vh, new Vector2(0, 35), new Vector2(34, -32), 8);
                    Line(vh, new Vector2(34, -32), new Vector2(-34, -32), 8);
                    Box(vh, -3, -11, 6, 27);
                    Circle(vh, 0, -22, 4);
                    break;
                case Symbol.Person:
                    Circle(vh, 0, 19, 18);
                    Box(vh, -28, -34, 56, 39);
                    break;
                case Symbol.Back:
                    Line(vh, new Vector2(-26, 0), new Vector2(31, 0), 12f);
                    Triangle(vh, P(-36, 0), P(-4, 28), P(-4, -28));
                    break;
                case Symbol.Audio:
                    Box(vh, -34, -13, 19, 26);
                    Triangle(vh, P(-23, -13), P(1, -32), P(1, 32));
                    Line(vh, new Vector2(14, -18), new Vector2(25, 0), 6f);
                    Line(vh, new Vector2(25, 0), new Vector2(14, 18), 6f);
                    break;
                case Symbol.Quit:
                    Ring(vh, 0, -3, 29, 19);
                    Line(vh, new Vector2(0, 6), new Vector2(0, 39), 12f);
                    break;
                case Symbol.Route:
                    Triangle(vh, P(-40, -32), P(-10, 32), P(16, -32));
                    Triangle(vh, P(-5, -32), P(19, 18), P(42, -32));
                    break;
                case Symbol.Chat:
                    RingRect(vh, -36, -25, 72, 52, 7);
                    Triangle(vh, P(-18, -24), P(-30, -42), P(-3, -25));
                    break;
            }

            Vector2 P(float x, float y) => origin + new Vector2(x, y) * scale;
            void Vertex(Vector2 p)
            {
                UIVertex v = UIVertex.simpleVert;
                v.position = p;
                v.color = color;
                vh.AddVert(v);
            }
            void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                int idx = vh.currentVertCount;
                Vertex(a); Vertex(b); Vertex(c); Vertex(d);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }
            void Triangle(VertexHelper h, Vector2 a, Vector2 b, Vector2 c)
            {
                int idx = h.currentVertCount;
                Vertex(a); Vertex(b); Vertex(c);
                h.AddTriangle(idx, idx + 1, idx + 2);
            }
            void Box(VertexHelper h, float x, float y, float w, float ht, bool clear = false)
            {
                if (clear) return; // Icon openings remain minimal; use line outlines where clarity matters.
                Quad(P(x, y), P(x + w, y), P(x + w, y + ht), P(x, y + ht));
            }
            void Circle(VertexHelper h, float x, float y, float radius)
            {
                const int n = 20;
                Vector2 center = P(x, y);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)i / n * Mathf.PI * 2f;
                    float b = (float)(i + 1) / n * Mathf.PI * 2f;
                    Triangle(h, center, P(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius),
                        P(x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius));
                }
            }
            void Ring(VertexHelper h, float x, float y, float outer, float inner)
            {
                const int n = 24;
                for (int i = 0; i < n; i++)
                {
                    float a = (float)i / n * Mathf.PI * 2f;
                    float b = (float)(i + 1) / n * Mathf.PI * 2f;
                    Quad(P(x + Mathf.Cos(a) * inner, y + Mathf.Sin(a) * inner),
                        P(x + Mathf.Cos(a) * outer, y + Mathf.Sin(a) * outer),
                        P(x + Mathf.Cos(b) * outer, y + Mathf.Sin(b) * outer),
                        P(x + Mathf.Cos(b) * inner, y + Mathf.Sin(b) * inner));
                }
            }
            void Line(VertexHelper h, Vector2 a, Vector2 b, float weight)
            {
                Vector2 normal = new Vector2(-(b.y - a.y), b.x - a.x).normalized * (weight * 0.5f);
                Quad(P(a.x + normal.x, a.y + normal.y), P(b.x + normal.x, b.y + normal.y),
                    P(b.x - normal.x, b.y - normal.y), P(a.x - normal.x, a.y - normal.y));
            }
            void RingRect(VertexHelper h, float x, float y, float w, float ht, float weight)
            {
                Box(h, x, y, w, weight);
                Box(h, x, y + ht - weight, w, weight);
                Box(h, x, y, weight, ht);
                Box(h, x + w - weight, y, weight, ht);
            }
        }

        public static ChaosUIIcon Add(Transform parent, string name, Symbol icon,
            Vector2 min, Vector2 max, Color color)
        {
            RectTransform rt = ChaosUI.Rect(parent, name, min, max, Vector2.zero, Vector2.zero);
            ChaosUIIcon glyph = rt.gameObject.AddComponent<ChaosUIIcon>();
            glyph.symbol = icon;
            glyph.color = color;
            glyph.raycastTarget = false;
            return glyph;
        }
    }
}
