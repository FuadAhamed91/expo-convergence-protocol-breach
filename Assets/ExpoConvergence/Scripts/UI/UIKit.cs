using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExpoConvergence
{
    /// <summary>Runtime fonts: Windows' Bahnschrift when available (crisp, DIN-like), else TMP's default.</summary>
    public static class ExpoFonts
    {
        static TMP_FontAsset main, bold;
        static bool initialised;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { main = null; bold = null; initialised = false; }

        public static TMP_FontAsset Main { get { Init(); return main; } }
        public static TMP_FontAsset Bold { get { Init(); return bold != null ? bold : main; } }

        static void Init()
        {
            if (initialised) return;
            initialised = true;
            // Bahnschrift is a variable font: only its default (Regular) instance loads by name,
            // so headings use TMP's synthesized bold on the same face.
            main = FromSystem("Bahnschrift", "Regular") ?? FromSystem("Segoe UI", "Regular");
            var fallback = TMP_Settings.defaultFontAsset;
            if (main == null) main = fallback;
            bold = main;
            if (fallback != null && main != null && main != fallback)
            {
                if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (!main.fallbackFontAssetTable.Contains(fallback)) main.fallbackFontAssetTable.Add(fallback);
            }
        }

        static TMP_FontAsset FromSystem(string family, string style)
        {
            try
            {
                var method = typeof(TMP_FontAsset).GetMethod("CreateFontAsset", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(int) }, null);
                if (method == null) return null;
                var asset = method.Invoke(null, new object[] { family, style, 90 }) as TMP_FontAsset;
                if (asset != null) asset.name = family + " " + style;
                return asset;
            }
            catch (Exception) { return null; }
        }
    }

    /// <summary>Procedurally generated UI sprites (no texture assets needed).</summary>
    public static class UISprites
    {
        static Sprite white, rounded, circle, ring, vignette, glow, arrow, eye, diamond, gradient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { white = rounded = circle = ring = vignette = glow = arrow = eye = diamond = gradient = null; }

        public static Sprite White => white ??= Make(4, (x, y, s) => 1f);
        public static Sprite Rounded => rounded ??= Make(64, (x, y, s) => RoundRect(x, y, s, 18f), 20);
        public static Sprite Circle => circle ??= Make(128, (x, y, s) => Mathf.Clamp01((s * 0.5f - Dist(x, y, s)) * 1.2f));
        public static Sprite Ring => ring ??= Make(128, (x, y, s) => { float d = Dist(x, y, s); return Mathf.Clamp01(1.5f - Mathf.Abs(d - 58f) * 0.5f); });
        public static Sprite Vignette => vignette ??= Make(256, (x, y, s) => { float d = Dist(x, y, s) / (s * 0.5f); return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01((d - 0.45f) / 0.55f), 1.6f)); });
        public static Sprite Glow => glow ??= Make(128, (x, y, s) => { float d = Dist(x, y, s) / (s * 0.5f); return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d); });
        public static Sprite Arrow => arrow ??= Make(64, (x, y, s) =>
        {
            // Upward pointing chevron.
            float u = (x - s * 0.5f) / (s * 0.5f), v = y / s;
            float edge = 1f - Mathf.Abs(u);
            float top = Mathf.Clamp01((edge - v) * 12f);
            float inner = Mathf.Clamp01(((edge - 0.45f) - v) * 12f);
            return Mathf.Clamp01(top - inner);
        });
        public static Sprite Eye => eye ??= Make(96, (x, y, s) =>
        {
            float u = (x - s * 0.5f) / (s * 0.5f), v = (y - s * 0.5f) / (s * 0.5f);
            float lid = 0.55f * (1f - u * u);
            float shape = Mathf.Clamp01((lid - Mathf.Abs(v)) * 18f);
            float outline = shape - Mathf.Clamp01((lid - 0.12f - Mathf.Abs(v)) * 18f);
            float pupil = Mathf.Clamp01((0.24f - Mathf.Sqrt(u * u + v * v)) * 20f);
            return Mathf.Clamp01(outline + pupil);
        });
        public static Sprite Diamond => diamond ??= Make(64, (x, y, s) =>
        {
            float u = Mathf.Abs(x - s * 0.5f), v = Mathf.Abs(y - s * 0.5f);
            return Mathf.Clamp01((s * 0.46f - (u + v)) * 0.8f);
        });
        public static Sprite VerticalGradient => gradient ??= Make(64, (x, y, s) => y / (float)s);

        static float Dist(int x, int y, int s) => Mathf.Sqrt((x - s * 0.5f + 0.5f) * (x - s * 0.5f + 0.5f) + (y - s * 0.5f + 0.5f) * (y - s * 0.5f + 0.5f));

        static float RoundRect(int x, int y, int s, float r)
        {
            float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
            float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        static Sprite Make(int size, Func<int, int, int, float> alpha, int border = 0)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y, size)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }

    /// <summary>Tiny builder helpers for uGUI hierarchies.</summary>
    public static class UIKit
    {
        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size) =>
            Rect(parent, name, anchor, anchor, anchor, pos, size);

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size, bool sliced = false)
        {
            var rt = Anchored(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, float spacing = 2f)
        {
            var rt = Anchored(parent, name, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = bold ? ExpoFonts.Bold : ExpoFonts.Main;
            if (font != null) t.font = font;
            if (bold) t.fontStyle = FontStyles.Bold;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color accent, Action onClick)
        {
            var rt = Anchored(parent, "Button_" + label, anchor, pos, size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UISprites.Rounded;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.11f, 0.18f, 0.92f);
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(accent.r * 1.6f + 0.3f, accent.g * 1.6f + 0.3f, accent.b * 1.6f + 0.3f, 1f);
            colors.pressedColor = accent;
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            btn.onClick.AddListener(() => { Game.Audio?.PlayUI("ui_click"); onClick?.Invoke(); });
            var edge = Image(rt, "Edge", UISprites.White, accent, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(4f, size.y - 14f));
            Text(rt, "Label", label, 22f, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAlignmentOptions.Center, true, 6f);
            return btn;
        }

        /// <summary>Wraps each letter in a colour tag so a word sweeps across several colours.</summary>
        public static string Gradient(string text, params Color[] stops)
        {
            var sb = new System.Text.StringBuilder();
            int n = Mathf.Max(1, text.Length - 1);
            for (int i = 0; i < text.Length; i++)
            {
                float t = (float)i / n * (stops.Length - 1);
                int a = Mathf.Min(stops.Length - 1, Mathf.FloorToInt(t));
                int b = Mathf.Min(stops.Length - 1, a + 1);
                Color c = Color.Lerp(stops[a], stops[b], t - a);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(text[i]).Append("</color>");
            }
            return sb.ToString();
        }
    }
}
