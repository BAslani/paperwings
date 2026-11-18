using UnityEditor;
using UnityEngine;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Paints the game's textures in code (no image files): paper stocks, wood, carpet, screens.
    /// Each recipe fills an array of pixels; Save() writes it as a texture asset in Assets/_Project/Art.
    /// Most are white/grey based so a material colour can tint them.
    /// </summary>
    public static class TextureFactory
    {
        const string ArtDir = "Assets/_Project/Art/";

        // ------------------------------------------------------------------ paper stocks

        /// <summary>Notebook paper: fine fibres, pale blue rules, a red margin, and the centre fold crease.</summary>
        public static Texture2D LinedPaper() => Save("T_PaperLined", 512, (x, y, s) =>
        {
            Color c = Fibres(x, y, s, new Color(0.98f, 0.97f, 0.93f), 0.035f);
            if (y % 32 < 2) c = Color.Lerp(c, new Color(0.55f, 0.7f, 0.92f), 0.55f);               // rules
            if (Mathf.Abs(x - s * 0.22f) < 1.5f) c = Color.Lerp(c, new Color(0.9f, 0.35f, 0.35f), 0.6f); // margin
            return Crease(c, x, s);
        });

        /// <summary>Graph paper: a faint blue-green grid with a stronger line every 5 squares.</summary>
        public static Texture2D GraphPaper() => Save("T_PaperGraph", 512, (x, y, s) =>
        {
            Color c = Fibres(x, y, s, new Color(0.97f, 0.98f, 0.96f), 0.03f);
            bool minor = x % 16 == 0 || y % 16 == 0, major = x % 80 == 0 || y % 80 == 0;
            if (minor) c = Color.Lerp(c, new Color(0.5f, 0.75f, 0.75f), major ? 0.6f : 0.3f);
            return Crease(c, x, s);
        });

        /// <summary>Dot-grid "bullet journal" paper: a soft grey dot every 20 pixels.</summary>
        public static Texture2D DotGridPaper() => Save("T_PaperDots", 512, (x, y, s) =>
        {
            Color c = Fibres(x, y, s, new Color(0.98f, 0.97f, 0.95f), 0.03f);
            int dx = x % 20 - 10, dy = y % 20 - 10;
            if (dx * dx + dy * dy <= 3) c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.55f), 0.6f);
            return Crease(c, x, s);
        });

        /// <summary>Cardstock: heavier grain with darker speckles.</summary>
        public static Texture2D Cardstock() => Save("T_PaperCard", 512, (x, y, s) =>
        {
            Color c = Fibres(x, y, s, new Color(0.96f, 0.94f, 0.9f), 0.06f);
            if (Hash(x, y) > 0.995f) c *= 0.75f;   // speckles
            return Crease(c, x, s);
        });

        /// <summary>Construction paper (tinted red by the rival material): strong, coarse fibres.</summary>
        public static Texture2D ConstructionPaper() => Save("T_PaperConstruction", 512, (x, y, s) =>
            Crease(Fibres(x, y, s, new Color(0.95f, 0.95f, 0.95f), 0.11f), x, s));

        /// <summary>Sticky-note paper (tinted blue by the wingman material): smooth, with a glue band at the top.</summary>
        public static Texture2D StickyPaper() => Save("T_PaperSticky", 512, (x, y, s) =>
        {
            Color c = Fibres(x, y, s, new Color(0.97f, 0.97f, 0.97f), 0.025f);
            if (y > s * 0.9f) c *= 0.93f;
            return Crease(c, x, s);
        });

        // ------------------------------------------------------------------ office

        /// <summary>Wood grain: warm stripes that wander, with darker streaks. Tinted by the desk material.</summary>
        public static Texture2D WoodGrain() => Save("T_WoodGrain", 512, (x, y, s) =>
        {
            float u = x / (float)s, v = y / (float)s;
            float wobble = Mathf.PerlinNoise(u * 3f, v * 0.6f) * 6f;
            float rings = Mathf.Sin((v * 22f + wobble) * Mathf.PI) * 0.5f + 0.5f;
            float streak = Mathf.PerlinNoise(u * 40f, v * 2f);
            float k = 0.78f + rings * 0.14f + streak * 0.1f;
            return new Color(k, k * 0.93f, k * 0.86f);
        });

        /// <summary>Carpet: a dark woven speckle (tiles seamlessly at this scale).</summary>
        public static Texture2D Carpet() => Save("T_CarpetWeave", 256, (x, y, s) =>
        {
            float weave = ((x / 2 + y / 2) % 2 == 0) ? 0.06f : 0f;
            float k = 0.16f + Hash(x, y) * 0.07f + weave;
            return new Color(k * 0.9f, k * 0.95f, k * 1.25f);
        });

        /// <summary>Monitor screen: an editor full of code, with coloured "syntax" and a cursor.</summary>
        public static Texture2D CodeScreen() => Save("T_ScreenCode", 512, (x, y, s) =>
        {
            Color bg = new Color(0.07f, 0.08f, 0.12f);
            int line = (s - y) / 14, col = x / 7;
            if (x < 34) return Mathf.Abs(line % 4) == 0 && x > 8 && x < 26 && (s - y) % 14 > 3 && (s - y) % 14 < 10 ? new Color(0.3f, 0.32f, 0.4f) : new Color(0.1f, 0.11f, 0.15f); // line numbers
            if ((s - y) % 14 < 3 || (s - y) % 14 > 10) return bg;
            int indent = (int)(Hash(line, 7) * 4) * 4 + 6;
            int length = indent + 8 + (int)(Hash(line, 3) * 46);
            if (Hash(line, 11) < 0.15f || col < indent || col > length) return bg;   // blank lines / margins
            float token = Hash(line * 31 + col / 5, 2);
            Color ink = token < 0.25f ? new Color(0.78f, 0.47f, 0.95f)     // keywords
                      : token < 0.45f ? new Color(0.45f, 0.75f, 1f)        // types
                      : token < 0.6f ? new Color(0.6f, 0.9f, 0.5f)         // strings
                      : new Color(0.85f, 0.87f, 0.92f);                    // plain
            if ((col - indent) % 5 == 4) return bg;                       // gaps between "words"
            return ink;
        });

        /// <summary>Laptop screen: Accounting's spreadsheet. A green grid, header row, numbers as bars.</summary>
        public static Texture2D SpreadsheetScreen() => Save("T_ScreenSheet", 512, (x, y, s) =>
        {
            int row = (s - y) / 20, colIndex = x / 64;
            bool gridLine = (s - y) % 20 == 0 || x % 64 == 0;
            Color bg = row == 0 ? new Color(0.2f, 0.45f, 0.3f) : (colIndex == 0 ? new Color(0.88f, 0.92f, 0.88f) : new Color(0.95f, 0.97f, 0.95f));
            if (gridLine) return new Color(0.7f, 0.8f, 0.72f);
            int inRow = (s - y) % 20, inCol = x % 64;
            float len = Hash(row, colIndex) * 44f + 8f;
            bool digits = row > 0 && inRow > 6 && inRow < 14 && inCol > 6 && inCol < len && (inCol / 4) % 2 == 0;
            if (digits) return colIndex == 0 ? new Color(0.25f, 0.3f, 0.28f) : new Color(0.2f, 0.25f, 0.6f);
            return bg;
        });

        /// <summary>Laptop deck: a grid of dark keys on a slightly lighter body.</summary>
        public static Texture2D Keyboard() => Save("T_Keyboard", 256, (x, y, s) =>
        {
            bool keyArea = y > s * 0.35f && y < s * 0.95f && x > s * 0.06f && x < s * 0.94f;
            bool gap = x % 16 < 2 || y % 16 < 2;
            if (keyArea && !gap) return new Color(0.12f, 0.12f, 0.14f);
            bool trackpad = y < s * 0.28f && y > s * 0.06f && x > s * 0.35f && x < s * 0.65f;
            return trackpad ? new Color(0.36f, 0.36f, 0.4f) : new Color(0.3f, 0.3f, 0.33f);
        });

        // ------------------------------------------------------------------ helpers

        delegate Color Painter(int x, int y, int size);

        static Texture2D Save(string name, int size, Painter paint)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = paint(x, y, size);

            string path = ArtDir + name + ".asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name };
                AssetDatabase.CreateAsset(tex, path);
            }
            else if (tex.width != size) tex.Reinitialize(size, size, TextureFormat.RGBA32, true);

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.SetPixels(px);
            tex.Apply(true);
            EditorUtility.SetDirty(tex);
            return tex;
        }

        /// <summary>Paper fibre: a base colour with soft noise at a few scales.</summary>
        static Color Fibres(int x, int y, int s, Color baseColor, float amount)
        {
            float u = x / (float)s, v = y / (float)s;
            float n = Mathf.PerlinNoise(u * 60f, v * 60f) * 0.5f + Mathf.PerlinNoise(u * 180f, v * 25f) * 0.35f + Hash(x, y) * 0.15f;
            return baseColor * (1f - amount + n * amount * 1.2f);
        }

        /// <summary>The centre fold: a thin shadow with a highlight beside it, down the middle of the sheet.</summary>
        static Color Crease(Color c, int x, int s)
        {
            float d = x - s * 0.5f;
            if (d > -1.5f && d <= 0f) return c * 0.8f;
            if (d > 0f && d < 2.5f) return Color.Lerp(c, Color.white, 0.25f);
            return c;
        }

        /// <summary>Repeatable pseudo-random 0..1 from two integers.</summary>
        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
