using System.Collections;
using HeatRise;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.UI
{
    /// <summary>Sala online, cuenta regresiva, resultado, eliminacion y pausa online con el diseno del menu.</summary>
    public sealed class LobbyView : MonoBehaviour
    {
        enum Panel { Hidden, Closing, Lobby, Countdown, Result, Eliminated, Paused }

        [Header("Wiring")]
        public LanMenu lanMenu;
        public MeltTransition melt;

        [Header("Tarjeta")]
        public GameObject panelRoot;
        public CanvasGroup panelGroup;
        public RectTransform panelRect;
        public GameObject dim;
        public CanvasGroup dimGroup;

        [Header("Encabezado")]
        public TMP_Text titleText;
        public Button helpButton;

        [Header("Codigo / IP")]
        public GameObject codeRow;
        public TMP_Text codeText;
        public Button codeButton;
        public TMP_Text codeButtonLabel;

        [Header("Jugadores")]
        public GameObject playersBlock;
        public TMP_Text countText;
        public TMP_Text[] playerTexts;

        [Header("Textos")]
        public TMP_Text messageText;
        public TMP_Text hintText;

        [Header("Botones")]
        public Button readyButton;
        public TMP_Text readyLabel;
        public Button startButton;
        public Image startFill;
        public Button continueButton;
        public Button lobbyButton;
        public Button leaveButton;
        public TMP_Text leaveLabel;

        [Header("Colores")]
        public Color ctaColor = new Color32(0xff, 0x6b, 0x1a, 0xff);
        public Color disabledColor = new Color32(0x3a, 0x2f, 0x28, 0x99);
        public Color titleColor = new Color32(0xff, 0xd9, 0xa0, 0xff);
        public Color winColor = new Color32(0x8f, 0xe3, 0x8f, 0xff);
        public Color lostColor = new Color32(0xff, 0x8a, 0x7a, 0xff);

        bool shown;
        Coroutine anim;

        void Awake()
        {
            helpButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null) GameManager.Instance.ShowHelp();
            });
            codeButton.onClick.AddListener(CodeAction);
            readyButton.onClick.AddListener(ToggleReady);
            startButton.onClick.AddListener(StartRace);
            continueButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
            });
            lobbyButton.onClick.AddListener(() =>
            {
                if (NetworkRace.Instance != null) NetworkRace.Instance.ReturnToLobby();
            });
            leaveButton.onClick.AddListener(() =>
            {
                if (lanMenu != null) lanMenu.LeaveMatch();
            });

            panelGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.94f;
            panelRoot.SetActive(false);
            dimGroup.alpha = 0f;
            dim.SetActive(false);
        }

        void Update()
        {
            Panel panel = Evaluate();
            if (panel != Panel.Hidden) Fill(panel);
            bool shouldShow = panel != Panel.Hidden;
            if (shouldShow && !shown) Open();
            else if (!shouldShow && shown) Close();
        }

        Panel Evaluate()
        {
            if (lanMenu == null) return Panel.Hidden;
            if (lanMenu.Leaving) return Panel.Closing;
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.HelpOpen) return Panel.Hidden;
            if (melt != null && melt.Active) return Panel.Hidden;

            NetworkRace race = NetworkRace.Instance;
            if (race == null || !race.IsSpawned || !lanMenu.IsConnected) return Panel.Hidden;
            if (race.InLobby) return Panel.Lobby;
            if (race.Stage.Value == 1) return Panel.Countdown;
            if (race.Stage.Value == 3) return Panel.Result;

            NetworkPlayer local = race.LocalPlayer;
            if (local != null && !local.Alive.Value) return Panel.Eliminated;
            if (manager != null && manager.MenuOpen) return Panel.Paused;
            return Panel.Hidden;
        }

        void Fill(Panel panel)
        {
            if (panel == Panel.Closing)
            {
                titleText.text = "Cerrando conexión...";
                titleText.color = titleColor;
                ShowOnly();
                return;
            }

            NetworkRace race = NetworkRace.Instance;
            NetworkPlayer local = race.LocalPlayer;
            bool host = lanMenu.IsHost;
            string leaveText = host ? "Cerrar partida para todos" : "Salir de la partida";

            switch (panel)
            {
                case Panel.Lobby:
                    titleText.text = LanMenu.VersionLabel;
                    titleText.color = titleColor;
                    FillLobby(race, local, host);
                    break;

                case Panel.Countdown:
                    titleText.text = "Comenzamos en " + race.Countdown;
                    titleText.color = titleColor;
                    ShowOnly(leave: true);
                    leaveLabel.text = leaveText;
                    break;

                case Panel.Result:
                    bool won = race.WinnerSlot.Value >= 0;
                    titleText.text = won
                        ? "Ganó Jugador " + (race.WinnerSlot.Value + 1)
                        : "Todos eliminados. Sin ganador.";
                    titleText.color = won ? winColor : lostColor;
                    ShowOnly(message: true, hint: !host, lobby: host, leave: true);
                    messageText.text = "Tiempo: " + race.Elapsed.ToString("0.0") + " s";
                    if (!host) hintText.text = "El host puede preparar otra ronda.";
                    leaveLabel.text = leaveText;
                    break;

                case Panel.Eliminated:
                    titleText.text = "Eliminado";
                    titleText.color = lostColor;
                    ShowOnly(message: true, hint: true, leave: true);
                    messageText.text = local != null && local.Cause.Value == 2
                        ? "La lava te alcanzó." : "Te caíste de la torre.";
                    hintText.text = host
                        ? "La carrera continúa. Mantené el juego abierto: esta computadora sigue siendo el servidor."
                        : "La carrera continúa. Esperá el resultado.";
                    leaveLabel.text = leaveText;
                    break;

                case Panel.Paused:
                    titleText.text = "Pausa";
                    titleText.color = titleColor;
                    ShowOnly(message: true, cont: true, leave: true);
                    messageText.text = "La carrera sigue en marcha.";
                    leaveLabel.text = leaveText;
                    break;
            }
        }

        void FillLobby(NetworkRace race, NetworkPlayer local, bool host)
        {
            bool showCode = lanMenu.useInternet || host;
            ShowOnly(code: showCode, players: true, hint: true,
                ready: local != null, start: host, leave: true);

            if (showCode)
            {
                if (lanMenu.useInternet)
                {
                    codeText.text = string.IsNullOrEmpty(lanMenu.RoomCode)
                        ? "Preparando código..." : "Código: " + lanMenu.RoomCode;
                    codeButtonLabel.text = "COPIAR";
                    codeButton.interactable = !string.IsNullOrEmpty(lanMenu.RoomCode);
                }
                else
                {
                    codeText.text = "IP: " + lanMenu.Addresses + " - Puerto " + lanMenu.port;
                    codeButtonLabel.text = "ACTUALIZAR";
                    codeButton.interactable = true;
                }
            }

            countText.text = race.Players.Count + " / 4 conectados";
            for (int i = 0; i < playerTexts.Length; i++)
            {
                bool used = i < race.Players.Count;
                playerTexts[i].gameObject.SetActive(used);
                if (!used) continue;
                NetworkPlayer player = race.Players[i];
                playerTexts[i].text = player.Label + (player.IsOwner ? " (vos)" : "")
                    + (player.Ready.Value ? " - Listo" : " - Esperando");
                playerTexts[i].color = player.RaceColor;
            }

            if (local != null) readyLabel.text = local.Ready.Value ? "QUITAR LISTO" : "ESTOY LISTO";
            if (host)
            {
                bool ready = race.AllReady;
                startButton.interactable = ready;
                startFill.color = ready ? ctaColor : disabledColor;
                hintText.text = "Se necesitan al menos 2 jugadores y todos listos.";
            }
            else hintText.text = "El host inicia cuando todos estén listos.";

            leaveLabel.text = host ? "Cerrar partida para todos" : "Salir de la partida";
        }

        void ShowOnly(bool code = false, bool players = false, bool message = false, bool hint = false,
            bool ready = false, bool start = false, bool cont = false, bool lobby = false, bool leave = false)
        {
            codeRow.SetActive(code);
            playersBlock.SetActive(players);
            messageText.gameObject.SetActive(message);
            hintText.gameObject.SetActive(hint);
            readyButton.gameObject.SetActive(ready);
            startButton.gameObject.SetActive(start);
            continueButton.gameObject.SetActive(cont);
            lobbyButton.gameObject.SetActive(lobby);
            leaveButton.gameObject.SetActive(leave);
        }

        void CodeAction()
        {
            if (lanMenu == null) return;
            if (lanMenu.useInternet)
            {
                if (!string.IsNullOrEmpty(lanMenu.RoomCode)) GUIUtility.systemCopyBuffer = lanMenu.RoomCode;
                return;
            }
            lanMenu.RefreshAddresses();
        }

        void ToggleReady()
        {
            NetworkRace race = NetworkRace.Instance;
            NetworkPlayer local = race != null ? race.LocalPlayer : null;
            if (local != null) local.SetReadyRpc(!local.Ready.Value);
        }

        void StartRace()
        {
            if (NetworkRace.Instance != null) NetworkRace.Instance.StartRace();
        }

        void Open()
        {
            shown = true;
            panelRoot.SetActive(true);
            dim.SetActive(true);
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(UiTween.Lerp(0.25f, t =>
            {
                panelGroup.alpha = Mathf.Clamp01(t * 2f);
                panelRect.localScale = Vector3.LerpUnclamped(Vector3.one * 0.94f, Vector3.one, UiTween.EaseOutBack(t));
                dimGroup.alpha = t;
            }));
        }

        void Close()
        {
            shown = false;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(CloseRoutine());
        }

        IEnumerator CloseRoutine()
        {
            yield return UiTween.Lerp(0.12f, t =>
            {
                panelGroup.alpha = 1f - t;
                panelRect.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.96f, t);
                dimGroup.alpha = 1f - t;
            });
            panelRoot.SetActive(false);
            dim.SetActive(false);
        }
    }
}
