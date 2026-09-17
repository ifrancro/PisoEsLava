using HeatRise.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeatRise.EditorTools
{
    /// <summary>Piezas compartidas para armar paneles con el mismo diseno del menu principal.</summary>
    public sealed class MenuUiKit
    {
        public const string SpriteDir = "Assets/TowerRush/UI/Sprites/Generated";
        public const string FontDir = "Assets/TowerRush/Fonts/TMP";

        public static readonly Color32 CardBg = new Color32(0x17, 0x0a, 0x08, 0xf0);
        public static readonly Color32 CardBorder = new Color32(0x7a, 0x3a, 0x1a, 0xff);
        public static readonly Color32 SecondaryIdle = new Color32(0x2b, 0x23, 0x20, 0xff);
        public static readonly Color32 BodyColor = new Color32(0xf2, 0xd9, 0xc8, 0xff);
        public static readonly Color32 AccentColor = new Color32(0xff, 0xcf, 0x9a, 0xff);
        public static readonly Color32 TitleColor = new Color32(0xff, 0xd9, 0xa0, 0xff);
        public static readonly Color32 HintColor = new Color32(0xc9, 0x9a, 0x7a, 0xff);
        public static readonly Color32 DimColor = new Color32(0x0a, 0x05, 0x04, 0xb4);
        public static readonly Color32 DisabledColor = new Color32(0x3a, 0x2f, 0x28, 0x99);

        public Sprite round14All, round14Cta, round10All, round8All, ring14, ring10, circle, circleSoft;
        public TMP_FontAsset fredokaBold, nunitoSemi, nunitoBold, nunitoExtra;

        readonly string tag;

        public MenuUiKit(string tag)
        {
            this.tag = tag;
            round14All = Load<Sprite>(SpriteDir + "/Round14All.png");
            round14Cta = Load<Sprite>(SpriteDir + "/Round14CTA.png");
            round10All = Load<Sprite>(SpriteDir + "/Round10All.png");
            round8All = Load<Sprite>(SpriteDir + "/Round8All.png");
            ring14 = Load<Sprite>(SpriteDir + "/Ring14.png");
            ring10 = Load<Sprite>(SpriteDir + "/Ring10.png");
            circle = Load<Sprite>(SpriteDir + "/Circle.png");
            circleSoft = Load<Sprite>(SpriteDir + "/CircleSoft.png");

            fredokaBold = Load<TMP_FontAsset>(FontDir + "/Fredoka-Bold SDF.asset");
            nunitoSemi = Load<TMP_FontAsset>(FontDir + "/NunitoSans-SemiBold SDF.asset");
            nunitoBold = Load<TMP_FontAsset>(FontDir + "/NunitoSans-Bold SDF.asset");
            nunitoExtra = Load<TMP_FontAsset>(FontDir + "/NunitoSans-ExtraBold SDF.asset");
        }

        T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError(tag + " Falta el asset (corre antes el build del menu LAN): " + path);
            return asset;
        }

        public static void EnsureEventSystem()
        {
            if (GameObject.Find("EventSystem") == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public RectTransform Dim(RectTransform parent, out CanvasGroup group)
        {
            RectTransform rt = NewChild("Dim", parent);
            Stretch(rt);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = DimColor;
            image.raycastTarget = true;
            group = rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        /// <summary>Tarjeta central con fondo, borde y layout vertical. Con height 0 crece con el contenido.</summary>
        public RectTransform Card(RectTransform parent, string name, float width, float height, RectOffset padding, float spacing)
        {
            RectTransform card = NewChild(name, parent);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(width, height);

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = round14All;
            bg.type = Image.Type.Sliced;
            bg.color = CardBg;

            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            if (height <= 0f)
            {
                ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            RectTransform border = NewChild("Border", card);
            Stretch(border);
            border.SetAsFirstSibling();
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image borderImage = border.gameObject.AddComponent<Image>();
            borderImage.sprite = ring14;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = CardBorder;
            borderImage.raycastTarget = false;

            return card;
        }

        public struct UiButton
        {
            public RectTransform rect;
            public Button button;
            public Image fill;
            public TMP_Text label;
        }

        public UiButton Button(Transform parent, string name, float height, Sprite sprite, Color color,
            string label, TMP_FontAsset font, float fontSize)
        {
            RectTransform rt = NewChild(name, parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

            Image fill = rt.gameObject.AddComponent<Image>();
            fill.sprite = sprite;
            fill.type = Image.Type.Sliced;
            fill.color = color;

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            UIHoverPress hover = rt.gameObject.AddComponent<UIHoverPress>();
            hover.target = rt;

            RectTransform labelRt = NewChild("Label", rt);
            Stretch(labelRt);
            TMP_Text labelText = Text(labelRt, label, font, fontSize, Color.white, TextAlignmentOptions.Center);
            labelText.characterSpacing = 0.5f;
            labelText.raycastTarget = false;

            return new UiButton { rect = rt, button = button, fill = fill, label = labelText };
        }

        public static RectTransform NewChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Spacer(Transform parent, float height)
        {
            NewChild("Spacer", parent).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static TMP_Text Text(RectTransform rt, string content, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions align, bool wrap = false)
        {
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.enableWordWrapping = wrap;
            text.raycastTarget = false;
            return text;
        }

        public TMP_Text Row(Transform parent, string name, string content, TMP_FontAsset font, float size,
            Color color, float height, bool wrap = false)
        {
            RectTransform rt = NewChild(name, parent);
            TMP_Text text = Text(rt, content, font, size, color, TextAlignmentOptions.Left, wrap);
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            if (wrap) le.minHeight = height;
            else le.preferredHeight = height;
            return text;
        }
    }
}
