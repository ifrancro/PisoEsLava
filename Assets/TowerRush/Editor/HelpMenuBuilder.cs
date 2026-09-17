using HeatRise.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.EditorTools
{
    /// <summary>Construye el panel de ayuda (HelpCanvas) en las dos escenas jugables.</summary>
    public sealed class HelpMenuBuilder
    {
        const string Tag = "[HelpMenuBuilder]";

        static readonly string[] Scenes =
        {
            "Assets/TowerRush/Scenes/HeatRise_LAN.unity",
            "Assets/TowerRush/Scenes/HeatRise_Jugable.unity"
        };

        MenuUiKit kit;

        [MenuItem("HeatRise/Menu/6 Build Help UI")]
        public static void Build()
        {
            HelpMenuBuilder builder = new HelpMenuBuilder();
            foreach (string scenePath in Scenes) builder.Run(scenePath);
        }

        void Run(string scenePath)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            kit = new MenuUiKit(Tag);

            MenuUiKit.EnsureEventSystem();
            Canvas canvas = MenuUiKit.CreateCanvas("HelpCanvas", 100);
            RectTransform canvasRt = (RectTransform)canvas.transform;

            HelpView view = canvas.gameObject.AddComponent<HelpView>();

            CanvasGroup dimGroup;
            view.dim = kit.Dim(canvasRt, out dimGroup).gameObject;
            view.dimGroup = dimGroup;

            BuildCard(canvasRt, view);

            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + " Ayuda construida en " + scenePath);
        }

        void BuildCard(RectTransform canvasRt, HelpView view)
        {
            RectTransform card = kit.Card(canvasRt, "PanelRoot", 660f, 660f, new RectOffset(26, 26, 22, 24), 12f);
            view.panelRoot = card.gameObject;
            view.panelRect = card;
            view.panelGroup = card.gameObject.AddComponent<CanvasGroup>();

            RectTransform titleRt = MenuUiKit.NewChild("Title", card);
            view.titleText = MenuUiKit.Text(titleRt, HelpContent.Title, kit.fredokaBold, 30f,
                MenuUiKit.TitleColor, TextAlignmentOptions.Left);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            // Area con scroll: Scroll > Viewport > Content > Body
            RectTransform scrollRt = MenuUiKit.NewChild("Scroll", card);
            LayoutElement scrollLe = scrollRt.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 200f;
            ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = false;

            RectTransform viewport = MenuUiKit.NewChild("Viewport", scrollRt);
            MenuUiKit.Stretch(viewport);
            viewport.pivot = new Vector2(0f, 1f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = MenuUiKit.NewChild("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 10, 0, 8);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform bodyRt = MenuUiKit.NewChild("Body", content);
            view.bodyText = MenuUiKit.Text(bodyRt, HelpContent.BuildRichText(), kit.nunitoSemi, 15f,
                MenuUiKit.BodyColor, TextAlignmentOptions.TopLeft, true);
            view.bodyText.lineSpacing = 4f;
            view.bodyText.paragraphSpacing = 6f;

            scroll.viewport = viewport;
            scroll.content = content;
            view.scroll = scroll;

            MenuUiKit.UiButton close = kit.Button(card, "CloseButton", 44f, kit.round14Cta, Color.white,
                "ENTENDIDO", kit.nunitoExtra, 16f);
            view.closeButton = close.button;
        }
    }
}
