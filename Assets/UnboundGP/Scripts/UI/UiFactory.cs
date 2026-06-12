using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnboundGP.UI
{
    /// <summary>
    /// uGUI をコードから構築するためのヘルパー。
    /// Vertical Slice ではプレハブを持たず、全 UI をここ経由で生成する。
    /// アートパス導入時はこのファクトリをプレハブ参照に差し替えればよい。
    /// </summary>
    public static class UiFactory
    {
        static Font defaultFont;

        /// <summary>OS フォントフォールバック付きの既定フォント (日本語表示用)。</summary>
        public static Font DefaultFont
        {
            get
            {
                if (defaultFont == null)
                {
                    try { defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { /* 古い Unity では Arial */ }
                    if (defaultFont == null)
                    {
                        try { defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                        catch { }
                    }
                    if (defaultFont == null)
                        defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return defaultFont;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            EnsureEventSystem();

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>単色パネル。anchorMin/Max + オフセットで配置する。</summary>
        public static RectTransform CreatePanel(Transform parent, Color color, string name = "Panel")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return (RectTransform)go.transform;
        }

        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max,
            Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }

        public static Text CreateText(Transform parent, string text, int size,
            TextAnchor align = TextAnchor.MiddleLeft, Color? color = null, bool bold = false)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button CreateButton(Transform parent, string label, UnityAction onClick,
            Color? bg = null, int fontSize = 22)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg ?? new Color(0.2f, 0.45f, 0.85f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label0 = CreateText(go.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white, true);
            SetAnchors((RectTransform)label0.transform, Vector2.zero, Vector2.one);
            return btn;
        }

        /// <summary>縦スクロールリスト。content に LayoutGroup 付きの入れ物を返す。</summary>
        public static ScrollRect CreateScrollView(Transform parent, out RectTransform content)
        {
            var rootRt = CreatePanel(parent, new Color(0f, 0f, 0f, 0.25f), "ScrollView");
            var scroll = rootRt.gameObject.AddComponent<ScrollRect>();

            var viewportRt = CreatePanel(rootRt, new Color(0f, 0f, 0f, 0.01f), "Viewport");
            SetAnchors(viewportRt, Vector2.zero, Vector2.one);
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportRt, false);
            content = contentGo.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRt;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        /// <summary>横バー (ゲージ)。戻り値の Image.fillAmount を 0〜1 で更新する。</summary>
        public static Image CreateBar(Transform parent, Color fillColor, string name = "Bar")
        {
            var bgRt = CreatePanel(parent, new Color(0f, 0f, 0f, 0.55f), name);
            var fillRt = CreatePanel(bgRt, fillColor, "Fill");
            SetAnchors(fillRt, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var img = fillRt.GetComponent<Image>();
            // Filled タイプには Sprite が必要なため白スプライトを生成
            img.sprite = WhiteSprite();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 0f;
            return img;
        }

        static Sprite whiteSprite;

        static Sprite WhiteSprite()
        {
            if (whiteSprite == null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var cols = new Color[4] { Color.white, Color.white, Color.white, Color.white };
                tex.SetPixels(cols);
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            }
            return whiteSprite;
        }

        static Sprite vignetteSprite;

        /// <summary>
        /// 中心が透明で外周が黒いラジアルビネットスプライト。
        /// ブラックアウト演出 (視野狭窄) に使う。
        /// </summary>
        public static Sprite VignetteSprite()
        {
            if (vignetteSprite == null)
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var cols = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - size * 0.5f) / (size * 0.5f);
                        float dy = (y - size * 0.5f) / (size * 0.5f);
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Pow(Mathf.Clamp01((r - 0.25f) / 0.6f), 1.6f);
                        cols[y * size + x] = new Color(0f, 0f, 0f, a);
                    }
                }
                tex.SetPixels(cols);
                tex.Apply();
                vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            return vignetteSprite;
        }

        /// <summary>LayoutGroup 内で高さを確保するためのヘルパー。</summary>
        public static LayoutElement SetLayoutHeight(GameObject go, float minHeight)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
            return le;
        }
    }
}
