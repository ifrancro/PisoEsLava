using HeatRise.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.EditorTools
{
    /// <summary>Construye la sala online (LobbyCanvas) con el diseno del menu principal.</summary>
    public sealed class LobbyMenuBuilder
    {
        const string ScenePath = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";
        const string Tag = "[LobbyMenuBuilder]";

        MenuUiKit kit;

        [MenuItem("HeatRise/Menu/5 Build Lobby UI")]
        public static void Build()
        {
            new LobbyMenuBuilder().Run();
        }

        void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            kit = new MenuUiKit(Tag);

            GameObject menuLan = GameObject.Find("Menu_LAN");
            if (menuLan == null) { Debug.LogError(Tag + " No se encontro Menu_LAN en la escena."); return; }
            LanMenu lanMenu = menuLan.GetComponent<LanMenu>();
            if (lanMenu == null) { Debug.LogError(Tag + " Menu_LAN no tiene el componente LanMenu."); return; }

            MenuUiKit.EnsureEventSystem();
            Canvas canvas = MenuUiKit.CreateCanvas("LobbyCanvas", 20);
            RectTransform canvasRt = (RectTransform)canvas.transform;

            LobbyView view = canvas.gameObject.AddComponent<LobbyView>();
            view.lanMenu = lanMenu;

            GameObject menuRoot = GameObject.Find("MenuRoot");
            LanMenuView menuView = menuRoot != null ? menuRoot.GetComponent<LanMenuView>() : null;
            if (menuView != null) view.melt = menuView.melt;
            else Debug.LogWarning(Tag + " No se encontro LanMenuView: la tarjeta puede aparecer durante la transicion.");

            CanvasGroup dimGroup;
            view.dim = kit.Dim(canvasRt, out dimGroup).gameObject;
            view.dimGroup = dimGroup;

            BuildCard(canvasRt, view);

            EditorUtility.SetDirty(view);
            UnityEngine.SceneManagement.Scene scene = menuLan.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + " Sala online construida y escena guardada.");
        }

        void BuildCard(RectTransform canvasRt, LobbyView view)
        {
            RectTransform card = kit.Card(canvasRt, "PanelRoot", 520f, 0f, new RectOffset(26, 26, 22, 24), 10f);
            view.panelRoot = card.gameObject;
            view.panelRect = card;
            view.panelGroup = card.gameObject.AddComponent<CanvasGroup>();

            // Encabezado: titulo + boton de ayuda anclado arriba a la derecha.
            RectTransform titleRt = MenuUiKit.NewChild("Title", card);
            view.titleText = MenuUiKit.Text(titleRt, LanMenu.VersionLabel, kit.fredokaBold, 30f,
                MenuUiKit.TitleColor, TextAlignmentOptions.Left);
            titleRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

            MenuUiKit.UiButton help = kit.Button(card, "HelpButton", 36f, kit.circle, MenuUiKit.SecondaryIdle,
                "?", kit.nunitoExtra, 16f);
            LayoutElement helpLe = help.rect.GetComponent<LayoutElement>();
            helpLe.ignoreLayout = true;
            help.rect.anchorMin = Vector2.one;
            help.rect.anchorMax = Vector2.one;
            help.rect.pivot = Vector2.one;
            help.rect.anchoredPosition = new Vector2(-20f, -20f);
            help.rect.sizeDelta = new Vector2(36f, 36f);
            view.helpButton = help.button;

            // Fila de codigo / IP con su boton (Copiar o Actualizar).
            RectTransform codeRow = MenuUiKit.NewChild("CodeRow", card);
            HorizontalLayoutGroup codeLayout = codeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            codeLayout.spacing = 10f;
            codeLayout.childAlignment = TextAnchor.MiddleLeft;
            codeLayout.childControlWidth = true;
            codeLayout.childControlHeight = true;
            codeLayout.childForceExpandWidth = false;
            codeRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            view.codeRow = codeRow.gameObject;

            RectTransform codeTextRt = MenuUiKit.NewChild("CodeText", codeRow);
            view.codeText = MenuUiKit.Text(codeTextRt, "Código: ------", kit.nunitoExtra, 17f,
                MenuUiKit.AccentColor, TextAlignmentOptions.Left);
            codeTextRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MenuUiKit.UiButton copy = kit.Button(codeRow, "CodeButton", 30f, kit.round10All,
                MenuUiKit.SecondaryIdle, "COPIAR", kit.nunitoExtra, 13f);
            copy.rect.GetComponent<LayoutElement>().preferredWidth = 120f;
            view.codeButton = copy.button;
            view.codeButtonLabel = copy.label;

            // Jugadores conectados.
            RectTransform players = MenuUiKit.NewChild("PlayersBlock", card);
            VerticalLayoutGroup playersLayout = players.gameObject.AddComponent<VerticalLayoutGroup>();
            playersLayout.spacing = 4f;
            playersLayout.childControlWidth = true;
            playersLayout.childControlHeight = true;
            playersLayout.childForceExpandWidth = true;
            ContentSizeFitter playersFitter = players.gameObject.AddComponent<ContentSizeFitter>();
            playersFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.playersBlock = players.gameObject;

            view.countText = kit.Row(players, "CountText", "0 / 4 conectados", kit.nunitoBold, 15f,
                MenuUiKit.BodyColor, 22f);

            view.playerTexts = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
                view.playerTexts[i] = kit.Row(players, "Player" + (i + 1), "Jugador " + (i + 1),
                    kit.nunitoExtra, 16f, Color.white, 22f);

            // Linea de estado y ayuda contextual.
            view.messageText = kit.Row(card, "MessageText", "", kit.nunitoSemi, 15f, MenuUiKit.BodyColor, 22f, true);
            view.hintText = kit.Row(card, "HintText", "", kit.nunitoSemi, 13f, MenuUiKit.HintColor, 18f, true);

            MenuUiKit.Spacer(card, 4f);

            MenuUiKit.UiButton ready = kit.Button(card, "ReadyButton", 44f, kit.round14All,
                MenuUiKit.SecondaryIdle, "ESTOY LISTO", kit.nunitoExtra, 16f);
            view.readyButton = ready.button;
            view.readyLabel = ready.label;

            MenuUiKit.UiButton start = kit.Button(card, "StartButton", 46f, kit.round14Cta,
                MenuUiKit.DisabledColor, "INICIAR CARRERA", kit.nunitoExtra, 17f);
            view.startButton = start.button;
            view.startFill = start.fill;

            MenuUiKit.UiButton cont = kit.Button(card, "ContinueButton", 44f, kit.round14Cta,
                Color.white, "CONTINUAR", kit.nunitoExtra, 16f);
            view.continueButton = cont.button;

            MenuUiKit.UiButton lobby = kit.Button(card, "LobbyButton", 44f, kit.round14Cta,
                Color.white, "VOLVER A LA SALA · OTRA RONDA", kit.nunitoExtra, 15f);
            view.lobbyButton = lobby.button;

            MenuUiKit.UiButton leave = kit.Button(card, "LeaveButton", 40f, kit.round14All,
                MenuUiKit.SecondaryIdle, "Salir de la partida", kit.nunitoExtra, 14f);
            view.leaveButton = leave.button;
            view.leaveLabel = leave.label;
        }
    }
}
