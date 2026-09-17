using System.Collections;
using System.Net;
using System.Net.Sockets;
using HeatRise;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.UI
{
    public sealed class LanMenuView : MonoBehaviour
    {
        enum IpStatus { Idle, Checking, Valid, Invalid }

        [Header("Wiring")]
        public LanMenu lanMenu;
        public GameObject menuRoot;
        public MeltTransition melt;

        [Header("Entrance cascade")]
        public RectTransform title;
        public CanvasGroup titleGroup;
        public RectTransform subtitle;
        public CanvasGroup subtitleGroup;
        public TMP_Text subtitleText;
        public RectTransform hostBlock;
        public CanvasGroup hostBlockGroup;
        public RectTransform joinBlock;
        public CanvasGroup joinBlockGroup;

        [Header("Solo")]
        public Button soloButton;

        [Header("Connection mode")]
        public Button internetButton;
        public Image internetButtonFill;
        public Button lanButton;
        public Image lanButtonFill;

        [Header("Host")]
        public Button hostButton;
        public Image hostButtonFill;
        public AccordionPanel hostPanel;
        public TMP_Text hostAddressText;
        public TMP_Text hostHelpText;

        [Header("Join")]
        public Button joinButton;
        public Image joinButtonFill;
        public AccordionPanel joinPanel;
        public TMP_InputField ipInput;
        public TMP_Text joinHelpText;
        public Image ipInputBorder;
        public Image statusDot;
        public TMP_Text hintText;
        public Button connectButton;
        public Image connectButtonFill;
        public TMP_Text connectButtonLabel;

        [Header("Secondary button sprites/colors")]
        public Sprite round14All;
        public Sprite round14Top;
        public Color secondaryIdleColor = new Color32(0x2b, 0x23, 0x20, 0xff);
        public Color secondaryActiveColor = new Color32(0x3a, 0x24, 0x16, 0xff);
        public Color connectValidColor = new Color32(0xff, 0x6b, 0x1a, 0xff);
        public Color connectDisabledColor = new Color32(0x3a, 0x2f, 0x28, 0x99);

        [Header("Status")]
        public TMP_Text statusText;
        public TMP_Text portText;

        static readonly Color IdleColor = new Color32(0x6a, 0x5a, 0x4a, 0xff);
        static readonly Color CheckingColor = new Color32(0xff, 0xcf, 0x4d, 0xff);
        static readonly Color ValidColor = new Color32(0x5f, 0xbf, 0x5f, 0xff);
        static readonly Color InvalidColor = new Color32(0xe0, 0x4b, 0x3a, 0xff);
        static readonly Color HintIdle = new Color32(0xc9, 0x9a, 0x7a, 0xff);
        static readonly Color HintValid = new Color32(0x8f, 0xe3, 0x8f, 0xff);
        static readonly Color HintInvalid = new Color32(0xff, 0x8a, 0x7a, 0xff);

        bool hostExpanded;
        bool joinExpanded;
        IpStatus ipStatus = IpStatus.Idle;
        Coroutine debounce;
        Coroutine dotPulse;

        void Awake()
        {
            soloButton.onClick.AddListener(PlaySolo);
            internetButton.onClick.AddListener(() => SelectMode(true));
            lanButton.onClick.AddListener(() => SelectMode(false));
            hostButton.onClick.AddListener(ToggleHost);
            joinButton.onClick.AddListener(ToggleJoin);
            connectButton.onClick.AddListener(OnConnectPressed);
            ipInput.onValueChanged.AddListener(OnIpChanged);
        }

        void OnEnable()
        {
            lanMenu.OnConnected += HandleConnected;
        }

        void OnDisable()
        {
            lanMenu.OnConnected -= HandleConnected;
        }

        void Start()
        {
            SelectMode(true);
            statusText.text = lanMenu.Message;

            StartCoroutine(UiTween.SlideAndFade(title, titleGroup, new Vector2(0f, 18f), 0.6f, 0f));
            StartCoroutine(UiTween.SlideAndFade(subtitle, subtitleGroup, new Vector2(0f, 26f), 0.5f, 0.1f));
            StartCoroutine(UiTween.SlideAndFade(hostBlock, hostBlockGroup, new Vector2(0f, 26f), 0.5f, 0.3f));
            StartCoroutine(UiTween.SlideAndFade(joinBlock, joinBlockGroup, new Vector2(0f, 26f), 0.5f, 0.4f));
        }

        void Update()
        {
            bool connecting = lanMenu.Connecting;
            soloButton.interactable = !connecting;
            hostButton.interactable = !connecting;
            internetButton.interactable = !connecting;
            lanButton.interactable = !connecting;
            statusText.text = connecting ? lanMenu.Message : string.IsNullOrEmpty(lanMenu.Message) ? "" : lanMenu.Message;
            connectButtonLabel.text = connecting ? "CANCELAR" : "CONECTAR";
            connectButton.interactable = connecting || ipStatus == IpStatus.Valid;
            connectButtonFill.color = connecting || ipStatus == IpStatus.Valid ? connectValidColor : connectDisabledColor;
        }

        void PlaySolo()
        {
            if (lanMenu.Connecting) return;
            melt.PlayThenLoadScene("HeatRise_Jugable");
        }

        void ToggleHost()
        {
            if (lanMenu.Connecting) return;
            hostExpanded = !hostExpanded;
            if (hostExpanded)
            {
                joinExpanded = false;
                joinPanel.SetExpanded(false);
                joinButtonFill.sprite = round14All;
                joinButtonFill.color = secondaryIdleColor;
                hostAddressText.text = lanMenu.useInternet ? "Creando código..." : "Puerto UDP: " + lanMenu.port;
                lanMenu.CreateMatch();
            }
            hostPanel.SetExpanded(hostExpanded);
            hostButtonFill.sprite = hostExpanded ? round14Top : round14All;
            hostButtonFill.color = hostExpanded ? secondaryActiveColor : secondaryIdleColor;
        }

        void ToggleJoin()
        {
            joinExpanded = !joinExpanded;
            if (joinExpanded)
            {
                hostExpanded = false;
                hostPanel.SetExpanded(false);
                hostButtonFill.sprite = round14All;
                hostButtonFill.color = secondaryIdleColor;
            }
            joinPanel.SetExpanded(joinExpanded);
            joinButtonFill.sprite = joinExpanded ? round14Top : round14All;
            joinButtonFill.color = joinExpanded ? secondaryActiveColor : secondaryIdleColor;
        }

        void OnConnectPressed()
        {
            if (lanMenu.Connecting)
            {
                lanMenu.CancelConnect();
                return;
            }
            if (ipStatus != IpStatus.Valid) return;
            if (lanMenu.useInternet) lanMenu.joinCode = ipInput.text.Trim().ToUpperInvariant();
            else lanMenu.hostAddress = ipInput.text.Trim();
            lanMenu.JoinMatch();
        }

        void OnIpChanged(string value)
        {
            if (debounce != null) StopCoroutine(debounce);
            SetIpStatus(IpStatus.Checking);
            debounce = StartCoroutine(ValidateAfterDelay(value));
        }

        IEnumerator ValidateAfterDelay(string value)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            string input = value.Trim();
            bool valid = lanMenu.useInternet ? IsValidCode(input) : IPAddress.TryParse(input, out IPAddress address)
                && address.AddressFamily == AddressFamily.InterNetwork
                && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.Broadcast);
            SetIpStatus(valid ? IpStatus.Valid : IpStatus.Invalid);
        }

        static bool IsValidCode(string value)
        {
            if (value.Length != 6) return false;
            for (int i = 0; i < value.Length; i++)
                if (!char.IsLetterOrDigit(value[i])) return false;
            return true;
        }

        void SelectMode(bool internet)
        {
            if (lanMenu.Connecting) return;
            lanMenu.useInternet = internet;
            internetButtonFill.color = internet ? secondaryActiveColor : secondaryIdleColor;
            lanButtonFill.color = internet ? secondaryIdleColor : secondaryActiveColor;
            subtitleText.text = internet ? "2 a 4 jugadores desde cualquier red" : "2 a 4 jugadores en la misma red";
            hostHelpText.text = internet ? "Crea la partida y comparte el código." : "Comparte tu IP con los demás jugadores.";
            joinHelpText.text = internet ? "Escribe el código que compartió el host." : "Escribe la IP de la computadora host.";
            portText.text = internet ? "CONEXIÓN ONLINE POR CÓDIGO" : "Puerto UDP: " + lanMenu.port;
            ipInput.characterLimit = internet ? 6 : 45;
            ((TMP_Text)ipInput.placeholder).text = internet ? "ABC123" : "192.168.1.20";
            ipInput.text = "";
            SetIpStatus(IpStatus.Idle);
            StartCoroutine(RebuildLayout());
        }

        IEnumerator RebuildLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)subtitle.parent);
        }

        void SetIpStatus(IpStatus status)
        {
            ipStatus = status;
            if (dotPulse != null) { StopCoroutine(dotPulse); dotPulse = null; }
            switch (status)
            {
                case IpStatus.Idle:
                    statusDot.color = IdleColor;
                    ipInputBorder.color = IdleColor;
                    hintText.text = lanMenu.useInternet ? "Código de 6 caracteres." : "Escribe la IP del host.";
                    hintText.color = HintIdle;
                    break;
                case IpStatus.Checking:
                    statusDot.color = CheckingColor;
                    ipInputBorder.color = IdleColor;
                    hintText.text = "Verificando...";
                    hintText.color = CheckingColor;
                    dotPulse = StartCoroutine(UiTween.PingPong(this, 0.7f, t =>
                    {
                        Color c = statusDot.color;
                        c.a = Mathf.LerpUnclamped(0.3f, 1f, t);
                        statusDot.color = c;
                    }));
                    break;
                case IpStatus.Valid:
                    statusDot.color = ValidColor;
                    ipInputBorder.color = ValidColor;
                    hintText.text = lanMenu.useInternet ? "Código válido." : "IP válida.";
                    hintText.color = HintValid;
                    break;
                case IpStatus.Invalid:
                    statusDot.color = InvalidColor;
                    ipInputBorder.color = InvalidColor;
                    hintText.text = lanMenu.useInternet ? "Código inválido." : "Formato de IP inválido.";
                    hintText.color = HintInvalid;
                    break;
            }
        }

        void HandleConnected()
        {
            melt.PlayThenReveal(
                () => lanMenu.IsConnected && NetworkRace.Instance != null && NetworkRace.Instance.IsSpawned,
                () => menuRoot.SetActive(false));
        }
    }
}
