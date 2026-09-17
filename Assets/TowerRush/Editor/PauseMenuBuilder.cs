using HeatRise.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeatRise.EditorTools
{
    public sealed class PauseMenuBuilder
    {
        const string ScenePath = "Assets/TowerRush/Scenes/HeatRise_Jugable.unity";
        const string SpriteDir = "Assets/TowerRush/UI/Sprites/Generated";
        const string FontDir = "Assets/TowerRush/Fonts/TMP";

        static readonly Color32 CardBg = new Color32(0x17, 0x0a, 0x08, 0xe6);
        static readonly Color32 CardBorder = new Color32(0x7a, 0x3a, 0x1a, 0xff);
        static readonly Color32 SecondaryIdle = new Color32(0x2b, 0x23, 0x20, 0xff);
        static readonly Color32 BodyColor = new Color32(0xf2, 0xd9, 0xc8, 0xff);
        static readonly Color32 AccentColor = new Color32(0xff, 0xcf, 0x9a, 0xff);
        static readonly Color32 DimColor = new Color32(0x0a, 0x05, 0x04, 0x99);

        Sprite _round14All, _round14Cta, _ring14, _round10All, _circle;
        TMP_FontAsset _fredokaBold, _nunitoSemi, _nunitoExtra;
        Texture2D _lavaTexture;

        [MenuItem("HeatRise/Menu/4 Build Pause Menu UI")]
        public static void Build()
        {
            new PauseMenuBuilder().Run();
        }

        void Run()
        {
            LoadAssets();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject partida = GameObject.Find("Partida");
            if (partida == null) { Debug.LogError("[PauseMenuBuilder] 'Partida' (GameManager) not found in the open scene."); return; }

            GameObject existing = GameObject.Find("PauseCanvas");
            if (existing != null) Object.DestroyImmediate(existing);
            if (GameObject.Find("EventSystem") == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            GameObject canvasGO = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = canvasGO.GetComponent<RectTransform>();

            PauseMenuView view = canvasGO.AddComponent<PauseMenuView>();

            RectTransform dim = NewChild("Dim", canvasRt);
            Stretch(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = DimColor;
            dimImage.raycastTarget = true;
            view.dim = dim.gameObject;
            view.dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

            view.panelRoot = BuildCard(canvasRt, view);
            view.melt = BuildMeltOverlay(canvasRt);

            EditorUtility.SetDirty(view);
            UnityEngine.SceneManagement.Scene scene = partida.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PauseMenuBuilder] Pause menu UI built and scene saved.");
        }

        void LoadAssets()
        {
            _round14All = Load<Sprite>(SpriteDir + "/Round14All.png");
            _round14Cta = Load<Sprite>(SpriteDir + "/Round14CTA.png");
            _ring14 = Load<Sprite>(SpriteDir + "/Ring14.png");
            _round10All = Load<Sprite>(SpriteDir + "/Round10All.png");
            _circle = Load<Sprite>(SpriteDir + "/Circle.png");
            _fredokaBold = Load<TMP_FontAsset>(FontDir + "/Fredoka-Bold SDF.asset");
            _nunitoSemi = Load<TMP_FontAsset>(FontDir + "/NunitoSans-SemiBold SDF.asset");
            _nunitoExtra = Load<TMP_FontAsset>(FontDir + "/NunitoSans-ExtraBold SDF.asset");

            MenuAssetGenerator.EnsureLavaTextureImport();
            _lavaTexture = Load<Texture2D>(MenuAssetGenerator.LavaTexturePath);
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError("[PauseMenuBuilder] Missing asset (run the LAN menu build first): " + path);
            return asset;
        }

        GameObject BuildCard(RectTransform canvasRt, PauseMenuView view)
        {
            RectTransform card = NewChild("PanelRoot", canvasRt);
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 0f));
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 24);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = _round14All;
            bg.type = Image.Type.Sliced;
            bg.color = CardBg;

            RectTransform border = NewChild("Border", card);
            Stretch(border);
            border.SetAsFirstSibling();
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image borderImage = border.gameObject.AddComponent<Image>();
            borderImage.sprite = _ring14;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = CardBorder;
            borderImage.raycastTarget = false;

            view.panelGroup = card.gameObject.AddComponent<CanvasGroup>();
            view.panelRect = card;

            RectTransform header = NewChild("Header", card);
            HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

            RectTransform titleRt = NewChild("Title", header);
            TMP_Text title = AddText(titleRt, "Pausa", _fredokaBold, 32f, Color.white, TextAlignmentOptions.Left);
            titleRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            view.titleText = title;

            Button help = CreateButton(card, "HelpButton", 36f, 36f, _circle, SecondaryIdle, "?", _nunitoExtra, 16f).button;
            RectTransform helpRt = (RectTransform)help.transform;
            helpRt.GetComponent<LayoutElement>().ignoreLayout = true;
            helpRt.anchorMin = Vector2.one;
            helpRt.anchorMax = Vector2.one;
            helpRt.pivot = Vector2.one;
            helpRt.anchoredPosition = new Vector2(-18f, -18f);
            helpRt.sizeDelta = new Vector2(36f, 36f);
            view.helpButton = help;

            RectTransform msgRt = NewChild("Message", card);
            TMP_Text msg = AddText(msgRt, "", _nunitoSemi, 14f, BodyColor, TextAlignmentOptions.Left);
            msg.enableWordWrapping = true;
            msgRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            view.messageText = msg;

            RectTransform timeRt = NewChild("Time", card);
            view.timeText = AddText(timeRt, "Tiempo: 00:00", _nunitoExtra, 15f, AccentColor, TextAlignmentOptions.Left);
            timeRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            RectTransform heightRt = NewChild("Height", card);
            view.heightText = AddText(heightRt, "Altura maxima: 0.0 m", _nunitoExtra, 15f, AccentColor, TextAlignmentOptions.Left);
            heightRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            Spacer(card, 6f);

            view.continueButton = CreateButton(card, "ContinueButton", 0f, 44f, _round14Cta, Color.white, "CONTINUAR", _nunitoExtra, 16f, true).button;
            view.restartButton = CreateButton(card, "RestartButton", 0f, 44f, _round14All, SecondaryIdle, "REINICIAR", _nunitoExtra, 16f, true).button;
            view.menuButton = CreateButton(card, "MenuButton", 0f, 44f, _round14All, SecondaryIdle, "MENU PRINCIPAL", _nunitoExtra, 16f, true).button;

            return card.gameObject;
        }

        struct ButtonRefs { public Button button; public Image fill; }

        ButtonRefs CreateButton(Transform parent, string name, float width, float height, Sprite sprite, Color color, string label, TMP_FontAsset font, float fontSize, bool stretch = false)
        {
            RectTransform rt = NewChild(name, parent);
            if (stretch) rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            else
            {
                LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = width;
                le.preferredHeight = height;
            }
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
            TMP_Text labelText = AddText(labelRt, label, font, fontSize, Color.white, TextAlignmentOptions.Center);
            labelText.characterSpacing = 0.5f;
            labelText.raycastTarget = false;

            return new ButtonRefs { button = button, fill = fill };
        }

        MeltTransition BuildMeltOverlay(RectTransform canvasRt)
        {
            RectTransform rt = NewChild("MeltOverlay", canvasRt);
            Stretch(rt);
            Image image = rt.gameObject.AddComponent<Image>();
            Shader meltShader = Shader.Find("HeatRise/UI/MeltReveal");
            if (meltShader != null)
            {
                Material meltMat = new Material(meltShader);
                meltMat.mainTexture = _lavaTexture;
                image.material = meltMat;
            }
            MeltTransition melt = rt.gameObject.AddComponent<MeltTransition>();
            melt.image = image;
            return melt;
        }

        static RectTransform NewChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Spacer(Transform parent, float height)
        {
            RectTransform rt = NewChild("Spacer", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        static TMP_Text AddText(RectTransform rt, string content, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.enableWordWrapping = false;
            return text;
        }
    }
}
