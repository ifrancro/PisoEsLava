using HeatRise.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeatRise.EditorTools
{
    /// Builds the pre-connect LAN menu Canvas hierarchy in HeatRise_LAN.unity from the generated
    /// sprites/fonts and wires it to a new LanMenuView component. Re-runnable: deletes and rebuilds
    /// only the "LanMenuCanvas" root it owns, leaving the rest of the scene untouched.
    public sealed class LanMenuBuilder
    {
        const string SpriteDir = "Assets/TowerRush/UI/Sprites/Generated";
        const string FontDir = "Assets/TowerRush/Fonts/TMP";

        static readonly Color32 TitleColor = new Color32(0xFF, 0xD9, 0xA0, 0xff);
        static readonly Color32 TitleOutline = new Color32(0x4a, 0x14, 0x00, 0xff);
        static readonly Color32 SubtitleColor = new Color32(0xf2, 0xd9, 0xc8, 0xff);
        static readonly Color32 SubtitlePlate = new Color32(0x00, 0x00, 0x00, 0x66);
        static readonly Color32 SecondaryIdle = new Color32(0x2b, 0x23, 0x20, 0xff);
        static readonly Color32 SecondaryBorder = new Color32(0x7a, 0x3a, 0x1a, 0xff);
        static readonly Color32 PanelBg = new Color32(0x00, 0x00, 0x00, 0x70);
        static readonly Color32 HintIdle = new Color32(0xc9, 0x9a, 0x7a, 0xff);
        static readonly Color32 InputBg = new Color32(0x1a, 0x15, 0x12, 0xff);
        static readonly Color32 IdleDot = new Color32(0x6a, 0x5a, 0x4a, 0xff);
        static readonly Color32 SuccessGreen = new Color32(0x8f, 0xe3, 0x8f, 0xff);
        static readonly Color32 ShadowSolo = new Color32(0x7a, 0x26, 0x00, 0xff);
        static readonly Color32 ShadowSecondary = new Color32(0x00, 0x00, 0x00, 0x60);
        static readonly Color32 PortTextColor = new Color32(0xc9, 0x9a, 0x7a, 0xff);

        Sprite _round14All, _round14Top, _round14Bottom, _round14Cta, _round10All, _round8All, _ring10, _ring14, _circle, _circleSoft, _bgGradient;
        TMP_FontAsset _fredokaBold, _nunitoSemi, _nunitoBold, _nunitoExtra;

        [MenuItem("HeatRise/Menu/3 Build LAN Menu UI")]
        public static void Build()
        {
            LanMenuBuilder builder = new LanMenuBuilder();
            builder.Run();
        }

        void Run()
        {
            LoadAssets();

            GameObject menuLan = GameObject.Find("Menu_LAN");
            if (menuLan == null) { Debug.LogError("[LanMenuBuilder] Menu_LAN GameObject not found in the open scene."); return; }
            LanMenu lanMenu = menuLan.GetComponent<LanMenu>();
            if (lanMenu == null) { Debug.LogError("[LanMenuBuilder] Menu_LAN has no LanMenu component."); return; }

            GameObject existing = GameObject.Find("LanMenuCanvas");
            if (existing != null) Object.DestroyImmediate(existing);
            if (GameObject.Find("EventSystem") == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            GameObject canvasGO = new GameObject("LanMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform canvasRt = canvasGO.GetComponent<RectTransform>();

            BuildBackground(canvasRt);

            GameObject menuRootGo = NewChild("MenuRoot", canvasRt);
            RectTransform menuRoot = menuRootGo.GetComponent<RectTransform>();
            Stretch(menuRoot);

            LanMenuView view = menuRootGo.AddComponent<LanMenuView>();
            view.lanMenu = lanMenu;
            view.menuRoot = menuRootGo;
            view.round14All = _round14All;
            view.round14Top = _round14Top;

            BuildTopStack(menuRoot, view);
            BuildBottomPortText(menuRoot, view);
            view.melt = BuildMeltOverlay(canvasRt);

            EditorUtility.SetDirty(view);
            UnityEngine.SceneManagement.Scene scene = menuLan.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LanMenuBuilder] LAN menu UI built and scene saved.");
        }

        void LoadAssets()
        {
            _round14All = Load<Sprite>(SpriteDir + "/Round14All.png");
            _round14Top = Load<Sprite>(SpriteDir + "/Round14Top.png");
            _round14Bottom = Load<Sprite>(SpriteDir + "/Round14Bottom.png");
            _round14Cta = Load<Sprite>(SpriteDir + "/Round14CTA.png");
            _round10All = Load<Sprite>(SpriteDir + "/Round10All.png");
            _round8All = Load<Sprite>(SpriteDir + "/Round8All.png");
            _ring10 = Load<Sprite>(SpriteDir + "/Ring10.png");
            _ring14 = Load<Sprite>(SpriteDir + "/Ring14.png");
            _circle = Load<Sprite>(SpriteDir + "/Circle.png");
            _circleSoft = Load<Sprite>(SpriteDir + "/CircleSoft.png");
            _bgGradient = Load<Sprite>(SpriteDir + "/BackgroundGradient.png");

            _fredokaBold = Load<TMP_FontAsset>(FontDir + "/Fredoka-Bold SDF.asset");
            _nunitoSemi = Load<TMP_FontAsset>(FontDir + "/NunitoSans-SemiBold SDF.asset");
            _nunitoBold = Load<TMP_FontAsset>(FontDir + "/NunitoSans-Bold SDF.asset");
            _nunitoExtra = Load<TMP_FontAsset>(FontDir + "/NunitoSans-ExtraBold SDF.asset");
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError("[LanMenuBuilder] Missing asset: " + path);
            return asset;
        }

        void BuildBackground(RectTransform canvasRt)
        {
            RectTransform bg = NewChild("Background", canvasRt).GetComponent<RectTransform>();
            Stretch(bg);
            Image bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = _bgGradient;
            bgImage.type = Image.Type.Simple;
            bgImage.raycastTarget = false;

            RectTransform glow = NewChild("GlowRadial", canvasRt).GetComponent<RectTransform>();
            glow.anchorMin = new Vector2(0.5f, 0f);
            glow.anchorMax = new Vector2(0.5f, 0f);
            glow.pivot = new Vector2(0.5f, 0.2f);
            glow.anchoredPosition = Vector2.zero;
            glow.sizeDelta = new Vector2(1500f, 800f);
            Image glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = _circleSoft;
            glowImage.color = new Color(1f, 0.42f, 0.1f, 0.35f);
            glowImage.raycastTarget = false;

            RectTransform shimmer = NewChild("Shimmer", canvasRt).GetComponent<RectTransform>();
            shimmer.anchorMin = new Vector2(0f, 0f);
            shimmer.anchorMax = new Vector2(1f, 0f);
            shimmer.pivot = new Vector2(0.5f, 0f);
            shimmer.anchoredPosition = Vector2.zero;
            shimmer.sizeDelta = new Vector2(0f, 340f);
            Image shimmerImage = shimmer.gameObject.AddComponent<Image>();
            shimmerImage.raycastTarget = false;
            Shader shimmerShader = Shader.Find("HeatRise/UI/HeatShimmer");
            if (shimmerShader != null) shimmerImage.material = new Material(shimmerShader);

            GameObject ashLayer = NewChild("AshParticles", canvasRt);
            Stretch(ashLayer.GetComponent<RectTransform>());
            AshParticlesUI ash = ashLayer.AddComponent<AshParticlesUI>();
            ash.sprite = _circleSoft;
            ash.count = 10;
        }

        void BuildTopStack(RectTransform menuRoot, LanMenuView view)
        {
            RectTransform top = NewChild("TopStack", menuRoot).GetComponent<RectTransform>();
            Anchor(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(640f, 0f));
            VerticalLayoutGroup topLayout = top.gameObject.AddComponent<VerticalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.UpperCenter;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = true;
            topLayout.childForceExpandHeight = false;
            ContentSizeFitter topFitter = top.gameObject.AddComponent<ContentSizeFitter>();
            topFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform titleRt = BuildTitle(top);
            view.title = titleRt;
            view.titleGroup = titleRt.gameObject.AddComponent<CanvasGroup>();

            Spacer(top, 6f);

            RectTransform subtitleRt = BuildSubtitle(top);
            view.subtitle = subtitleRt;
            view.subtitleGroup = subtitleRt.gameObject.AddComponent<CanvasGroup>();

            Spacer(top, 34f);

            RectTransform stack = NewChild("ButtonStack", top).GetComponent<RectTransform>();
            LayoutElement stackLe = stack.gameObject.AddComponent<LayoutElement>();
            stackLe.flexibleWidth = 1f;
            VerticalLayoutGroup stackLayout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            stackLayout.spacing = 16f;
            stackLayout.childControlWidth = true;
            stackLayout.childControlHeight = true;
            stackLayout.childForceExpandWidth = true;
            stackLayout.childForceExpandHeight = false;
            ContentSizeFitter stackFitter = stack.gameObject.AddComponent<ContentSizeFitter>();
            stackFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildSoloButton(stack, view);
            BuildHostBlock(stack, view);
            BuildJoinBlock(stack, view);

            RectTransform statusRt = NewChild("StatusText", stack).GetComponent<RectTransform>();
            TMP_Text statusText = AddText(statusRt, "", _nunitoBold, 14f, new Color32(0xff, 0xcf, 0x9a, 0xff), TextAlignmentOptions.Center);
            LayoutElement statusLe = statusRt.gameObject.AddComponent<LayoutElement>();
            statusLe.minHeight = 20f;
            view.statusText = statusText;
        }

        RectTransform BuildTitle(Transform parent)
        {
            RectTransform rt = NewChild("Title", parent).GetComponent<RectTransform>();
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 84f;
            TMP_Text text = AddText(rt, "TOWERRUSH", _fredokaBold, 64f, TitleColor, TextAlignmentOptions.Center);
            text.characterSpacing = 2f;
            text.outlineWidth = 0.2f;
            text.outlineColor = TitleOutline;
            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = TitleOutline;
            shadow.effectDistance = new Vector2(0f, -6f);
            return rt;
        }

        RectTransform BuildSubtitle(Transform parent)
        {
            RectTransform rt = NewChild("Subtitle", parent).GetComponent<RectTransform>();
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;
            HorizontalLayoutGroup layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(16, 16, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            ContentSizeFitter fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = _round8All;
            plate.type = Image.Type.Sliced;
            plate.color = SubtitlePlate;
            plate.raycastTarget = false;

            RectTransform textRt = NewChild("Text", rt).GetComponent<RectTransform>();
            AddText(textRt, "2 a 4 jugadores en la misma red", _nunitoBold, 16f, SubtitleColor, TextAlignmentOptions.Center);
            return rt;
        }

        void BuildSoloButton(Transform parent, LanMenuView view)
        {
            ButtonRefs refs = CreateButtonSlot(parent, "SoloButton", 64f, true, _round14Cta, Color.white, ShadowSolo, "JUGAR SOLO", _nunitoExtra, 20f);
            view.soloButton = refs.button;
        }

        void BuildHostBlock(Transform parent, LanMenuView view)
        {
            RectTransform block = NewChild("HostBlock", parent).GetComponent<RectTransform>();
            VerticalLayoutGroup layout = block.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = block.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.hostBlock = block;
            view.hostBlockGroup = block.gameObject.AddComponent<CanvasGroup>();

            ButtonRefs refs = CreateButtonSlot(block, "HostButton", 64f, false, _round14All, SecondaryIdle, ShadowSecondary, "CREAR PARTIDA (HOST)", _nunitoExtra, 20f);
            view.hostButton = refs.button;
            view.hostButtonFill = refs.fill;

            RectTransform panelRt = NewChild("HostPanel", block).GetComponent<RectTransform>();
            LayoutElement panelLe = panelRt.gameObject.AddComponent<LayoutElement>();
            AccordionPanel accordion = panelRt.gameObject.AddComponent<AccordionPanel>();
            Image panelBg = panelRt.gameObject.AddComponent<Image>();
            panelBg.sprite = _round14Bottom;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = PanelBg;
            panelBg.raycastTarget = false;
            CanvasGroup panelGroup = panelRt.gameObject.AddComponent<CanvasGroup>();

            RectTransform content = NewChild("Content", panelRt).GetComponent<RectTransform>();
            Stretch(content);
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(20, 20, 16, 16);
            contentLayout.spacing = 8f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform helpRt = NewChild("HelpText", content).GetComponent<RectTransform>();
            AddText(helpRt, "Tu controlas la partida - comparte tu IP con los demas.", _nunitoSemi, 14f, SubtitleColor, TextAlignmentOptions.Left);
            helpRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            RectTransform row = NewChild("Row", content).GetComponent<RectTransform>();
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            RectTransform addrRt = NewChild("Address", row).GetComponent<RectTransform>();
            TMP_Text addrText = AddText(addrRt, "Puerto UDP: 7777", _nunitoExtra, 15f, new Color32(0xff, 0xcf, 0x9a, 0xff), TextAlignmentOptions.Left);
            addrRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            view.hostAddressText = addrText;

            RectTransform waitRt = NewChild("Waiting", row).GetComponent<RectTransform>();
            AddText(waitRt, "* Esperando jugadores...", _nunitoBold, 13f, SuccessGreen, TextAlignmentOptions.Right);
            waitRt.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;

            accordion.content = content;
            accordion.group = panelGroup;
            view.hostPanel = accordion;
        }

        void BuildJoinBlock(Transform parent, LanMenuView view)
        {
            RectTransform block = NewChild("JoinBlock", parent).GetComponent<RectTransform>();
            VerticalLayoutGroup layout = block.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = block.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.joinBlock = block;
            view.joinBlockGroup = block.gameObject.AddComponent<CanvasGroup>();

            ButtonRefs refs = CreateButtonSlot(block, "JoinButton", 64f, false, _round14All, SecondaryIdle, ShadowSecondary, "UNIRSE A LA PARTIDA", _nunitoExtra, 20f);
            view.joinButton = refs.button;
            view.joinButtonFill = refs.fill;

            RectTransform panelRt = NewChild("JoinPanel", block).GetComponent<RectTransform>();
            panelRt.gameObject.AddComponent<LayoutElement>();
            AccordionPanel accordion = panelRt.gameObject.AddComponent<AccordionPanel>();
            Image panelBg = panelRt.gameObject.AddComponent<Image>();
            panelBg.sprite = _round14Bottom;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = PanelBg;
            panelBg.raycastTarget = false;
            CanvasGroup panelGroup = panelRt.gameObject.AddComponent<CanvasGroup>();

            RectTransform content = NewChild("Content", panelRt).GetComponent<RectTransform>();
            Stretch(content);
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(20, 20, 16, 16);
            contentLayout.spacing = 10f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform helpRt = NewChild("HelpText", content).GetComponent<RectTransform>();
            AddText(helpRt, "Pide la IP a quien creo la partida.", _nunitoSemi, 14f, SubtitleColor, TextAlignmentOptions.Left);
            helpRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            RectTransform row = NewChild("Row", content).GetComponent<RectTransform>();
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            RectTransform inputRt = BuildIpInput(row, view);
            inputRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            RectTransform dotRt = NewChild("StatusDot", row).GetComponent<RectTransform>();
            LayoutElement dotLe = dotRt.gameObject.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 14f;
            dotLe.preferredHeight = 14f;
            Image dot = dotRt.gameObject.AddComponent<Image>();
            dot.sprite = _circle;
            dot.color = IdleDot;
            dot.raycastTarget = false;
            view.statusDot = dot;

            RectTransform hintRt = NewChild("Hint", content).GetComponent<RectTransform>();
            TMP_Text hint = AddText(hintRt, "Escribe la IP del host.", _nunitoExtra, 12f, HintIdle, TextAlignmentOptions.Left);
            hintRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
            view.hintText = hint;

            ButtonRefs connect = CreateButtonSlot(content, "ConnectButton", 44f, false, _round10All, new Color32(0x3a, 0x2f, 0x28, 0x99), ShadowSecondary, "CONECTAR", _nunitoExtra, 16f);
            view.connectButton = connect.button;
            view.connectButtonFill = connect.fill;
            view.connectButtonLabel = connect.label;

            accordion.content = content;
            accordion.group = panelGroup;
            view.joinPanel = accordion;
        }

        RectTransform BuildIpInput(Transform parent, LanMenuView view)
        {
            GameObject go = NewChild("IpInput", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            Image bg = go.AddComponent<Image>();
            bg.sprite = _round10All;
            bg.type = Image.Type.Sliced;
            bg.color = InputBg;
            TMP_InputField field = go.AddComponent<TMP_InputField>();

            RectTransform border = NewChild("Border", rt).GetComponent<RectTransform>();
            Stretch(border);
            Image borderImage = border.gameObject.AddComponent<Image>();
            borderImage.sprite = _ring10;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = IdleDot;
            borderImage.raycastTarget = false;
            view.ipInputBorder = borderImage;

            RectTransform textArea = NewChild("Text Area", rt).GetComponent<RectTransform>();
            Stretch(textArea, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            textArea.gameObject.AddComponent<RectMask2D>();

            RectTransform placeholderRt = NewChild("Placeholder", textArea).GetComponent<RectTransform>();
            Stretch(placeholderRt);
            TMP_Text placeholder = AddText(placeholderRt, "192.168.1.20", _nunitoBold, 16f, new Color(0.95f, 0.85f, 0.78f, 0.4f), TextAlignmentOptions.Left);

            RectTransform textRt = NewChild("Text", textArea).GetComponent<RectTransform>();
            Stretch(textRt);
            TMP_Text text = AddText(textRt, "", _nunitoBold, 16f, Color.white, TextAlignmentOptions.Left);

            field.textViewport = textArea;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 45;
            field.text = "192.168.1.20";
            view.ipInput = field;
            return rt;
        }

        RectTransform BuildBottomPortText(RectTransform menuRoot, LanMenuView view)
        {
            RectTransform rt = NewChild("BottomPortText", menuRoot).GetComponent<RectTransform>();
            Anchor(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(400f, 24f));
            TMP_Text text = AddText(rt, "Puerto UDP: 7777", _nunitoBold, 13f, PortTextColor, TextAlignmentOptions.Center);
            view.portText = text;
            return rt;
        }

        MeltTransition BuildMeltOverlay(RectTransform canvasRt)
        {
            GameObject go = NewChild("MeltOverlay", canvasRt);
            RectTransform rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            Image image = go.AddComponent<Image>();
            Shader meltShader = Shader.Find("HeatRise/UI/MeltReveal");
            if (meltShader != null) image.material = new Material(meltShader);
            MeltTransition melt = go.AddComponent<MeltTransition>();
            melt.image = image;
            return melt;
        }

        struct ButtonRefs
        {
            public GameObject go;
            public Button button;
            public Image fill;
            public TMP_Text label;
        }

        ButtonRefs CreateButtonSlot(Transform parent, string name, float height, bool glow, Sprite fillSprite, Color fillColor, Color shadowColor, string label, TMP_FontAsset font, float fontSize)
        {
            RectTransform slot = NewChild(name + "Slot", parent).GetComponent<RectTransform>();
            LayoutElement slotLe = slot.gameObject.AddComponent<LayoutElement>();
            slotLe.preferredHeight = height;

            if (glow)
            {
                RectTransform glowRt = NewChild("Glow", slot).GetComponent<RectTransform>();
                glowRt.anchorMin = Vector2.zero;
                glowRt.anchorMax = Vector2.one;
                glowRt.offsetMin = new Vector2(-18f, -18f);
                glowRt.offsetMax = new Vector2(18f, 18f);
                Image glowImage = glowRt.gameObject.AddComponent<Image>();
                glowImage.sprite = _circleSoft;
                glowImage.color = new Color(1f, 0.42f, 0.1f, 0.7f);
                glowImage.raycastTarget = false;
                GlowPulse pulse = glowRt.gameObject.AddComponent<GlowPulse>();
                pulse.target = glowImage;
                pulse.period = 2.4f;
                pulse.minAlpha = 0.25f;
                pulse.maxAlpha = 0.7f;
            }

            RectTransform shadowRt = NewChild("Shadow", slot).GetComponent<RectTransform>();
            shadowRt.anchorMin = Vector2.zero;
            shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(0f, -6f);
            shadowRt.offsetMax = new Vector2(0f, -6f);
            Image shadowImage = shadowRt.gameObject.AddComponent<Image>();
            shadowImage.sprite = _round14All;
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = shadowColor;
            shadowImage.raycastTarget = false;

            RectTransform btnRt = NewChild(name, slot).GetComponent<RectTransform>();
            Stretch(btnRt);
            Image fill = btnRt.gameObject.AddComponent<Image>();
            fill.sprite = fillSprite;
            fill.type = Image.Type.Sliced;
            fill.color = fillColor;
            Button button = btnRt.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            UIHoverPress hover = btnRt.gameObject.AddComponent<UIHoverPress>();
            hover.target = btnRt;

            RectTransform labelRt = NewChild("Label", btnRt).GetComponent<RectTransform>();
            Stretch(labelRt);
            TMP_Text labelText = AddText(labelRt, label, font, fontSize, Color.white, TextAlignmentOptions.Center);
            labelText.characterSpacing = 1f;
            labelText.raycastTarget = false;

            return new ButtonRefs { go = btnRt.gameObject, button = button, fill = fill, label = labelText };
        }

        static GameObject NewChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Spacer(Transform parent, float height)
        {
            GameObject go = NewChild("Spacer", parent);
            go.AddComponent<LayoutElement>().preferredHeight = height;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
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
