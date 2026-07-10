using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Supplies the small UI sprites the overlay needs. The hand pointers and arrow load bundled art
    /// (CC0, Kenney Cursor Pack) from the package's <c>Resources/TutorialKit/Pointers</c> folder; the
    /// rounded panel, tap ring and dot are generated procedurally. Every accessor falls back to a
    /// procedural shape if the art is missing, so the package still works with no art present. Results
    /// are cached; a game can still override the pointer art via the pointer/text-box view fields.
    /// </summary>
    public static class TutorialSpriteFactory
    {
        private static Sprite _panel, _hand, _handOpen, _handClosed, _arrow, _ring, _dot;

        /// <summary>Bundled pointer art (CC0, Kenney Cursor Pack) loaded from the package's Resources;
        /// falls back to the procedural shape if the art is ever missing.</summary>
        private const string ArtPath = "TutorialKit/Pointers/";
        private static Sprite Art(string name) => Resources.Load<Sprite>(ArtPath + name);

        public static Sprite RoundedPanel => _panel ??= BuildRoundedRect(96, 26);
        public static Sprite Hand => _hand ??= Art("hand_point") ?? BuildHand(128);
        public static Sprite HandOpen => _handOpen ??= Art("hand_open") ?? BuildHand(128);
        public static Sprite HandClosed => _handClosed ??= Art("hand_closed") ?? BuildHand(128);
        public static Sprite Arrow => _arrow ??= Art("arrow") ?? BuildArrow(128);
        public static Sprite Ring => _ring ??= BuildRing(128, 0.16f);
        public static Sprite Dot => _dot ??= BuildCircle(64);

        private static Sprite MakeSprite(Texture2D tex, Vector4 border = default)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Texture2D NewTex(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "TK_Generated" };
            var clear = new Color32(255, 255, 255, 0);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            t.SetPixels32(px);
            return t;
        }

        private static float Aa(float d) => Mathf.Clamp01(0.5f - d); // signed distance in px -> coverage

        // Rounded rectangle, 9-sliced (border = corner radius).
        private static Sprite BuildRoundedRect(int size, int radius)
        {
            var tex = NewTex(size);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float qx = Mathf.Abs(px) - (half - radius);
                float qy = Mathf.Abs(py) - (half - radius);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                float a = Aa(d);
                if (a > 0f) tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            return MakeSprite(tex, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite BuildCircle(int size)
        {
            var tex = NewTex(size);
            float half = size * 0.5f;
            float r = half - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) - r;
                float a = Aa(d);
                if (a > 0f) tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            return MakeSprite(tex);
        }

        // Hollow ring for tap feedback.
        private static Sprite BuildRing(int size, float thickness)
        {
            var tex = NewTex(size);
            float half = size * 0.5f;
            float rOuter = half - 1f;
            float rInner = rOuter * (1f - thickness * 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                float d = Mathf.Max(dist - rOuter, rInner - dist);
                float a = Aa(d);
                if (a > 0f) tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            return MakeSprite(tex);
        }

        // A simple upward arrow (tip at top): triangle head + rounded stem. Rotate at runtime as needed.
        private static Sprite BuildArrow(int size)
        {
            var tex = NewTex(size);
            float w = size;
            float cx = w * 0.5f;
            float headBase = w * 0.45f;   // y where the head meets the stem
            float headHalf = w * 0.34f;   // half-width of the head at the base
            float stemHalf = w * 0.12f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                bool inside;
                if (py >= headBase)
                {
                    float t = Mathf.InverseLerp(w - 2f, headBase, py); // 0 at tip, 1 at base
                    float halfW = Mathf.Lerp(0f, headHalf, t);
                    inside = Mathf.Abs(px - cx) <= halfW;
                }
                else
                {
                    inside = Mathf.Abs(px - cx) <= stemHalf && py >= 2f;
                }
                if (inside) tex.SetPixel(x, y, Color.white);
            }
            return MakeSprite(tex);
        }

        // A stylised pointing hand: a capsule "finger" with a rounded tip, tilted, plus a knuckle.
        private static Sprite BuildHand(int size)
        {
            var tex = NewTex(size);
            float half = size * 0.5f;
            // Finger capsule from bottom to upper area, tilted slightly.
            Vector2 tip = new Vector2(half * 0.9f, size * 0.86f);
            Vector2 baseP = new Vector2(half * 1.05f, size * 0.18f);
            float fingerR = size * 0.15f;
            float knuckleR = size * 0.22f;
            Vector2 knuckle = new Vector2(half * 1.15f, size * 0.28f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float dFinger = SdSegment(p, baseP, tip) - fingerR;
                float dKnuckle = (p - knuckle).magnitude - knuckleR;
                float d = Mathf.Min(dFinger, dKnuckle);
                float a = Aa(d);
                if (a > 0f) tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            return MakeSprite(tex);
        }

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }
    }
}
