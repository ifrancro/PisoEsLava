using HeatRise;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HeatRise.UI
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Wiring")]
        public GameObject panelRoot;
        public CanvasGroup panelGroup;
        public RectTransform panelRect;
        public GameObject dim;
        public CanvasGroup dimGroup;
        public MeltTransition melt;

        [Header("Texts")]
        public TMP_Text titleText;
        public TMP_Text messageText;
        public TMP_Text timeText;
        public TMP_Text heightText;

        [Header("Buttons")]
        public Button continueButton;
        public Button restartButton;
        public Button menuButton;
        public Button helpButton;

        static readonly Color PausedColor = new Color32(0xFF, 0xD9, 0xA0, 0xff);
        static readonly Color WonColor = new Color32(0x8f, 0xe3, 0x8f, 0xff);
        static readonly Color LostColor = new Color32(0xff, 0x8a, 0x7a, 0xff);

        bool shown;
        Coroutine anim;

        void Awake()
        {
            continueButton.onClick.AddListener(() => GameManager.Instance.SetPaused(false));
            restartButton.onClick.AddListener(() => melt.PlayThenLoadScene(SceneManager.GetActiveScene().name));
            menuButton.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                melt.PlayThenLoadScene("HeatRise_LAN");
            });
            helpButton.onClick.AddListener(() => GameManager.Instance.ShowHelp());
            panelGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.9f;
            panelRoot.SetActive(false);
            dimGroup.alpha = 0f;
            dim.SetActive(false);
        }

        void Update()
        {
            GameManager gm = GameManager.Instance;
            bool shouldShow = gm != null && (gm.Paused || gm.Finished) && !gm.HelpOpen;
            if (shouldShow && !shown) Open(gm);
            else if (!shouldShow && shown) Close();
        }

        void Open(GameManager gm)
        {
            shown = true;
            panelRoot.SetActive(true);
            dim.SetActive(true);
            bool finished = gm.Finished;
            titleText.text = finished ? gm.Won ? "Llegaste a la cima" : "Fin del intento" : "Pausa";
            titleText.color = finished ? gm.Won ? WonColor : LostColor : PausedColor;
            messageText.text = finished ? gm.Reason : "La lava y los obstaculos estan detenidos.";
            continueButton.gameObject.SetActive(!finished);

            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(UiTween.Lerp(0.25f, t =>
            {
                float e = UiTween.EaseOutBack(t);
                panelGroup.alpha = Mathf.Clamp01(t * 2f);
                panelRect.localScale = Vector3.LerpUnclamped(Vector3.one * 0.9f, Vector3.one, e);
                dimGroup.alpha = t;
            }));
        }

        void Close()
        {
            shown = false;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(CloseRoutine());
        }

        System.Collections.IEnumerator CloseRoutine()
        {
            yield return UiTween.Lerp(0.12f, t =>
            {
                panelGroup.alpha = 1f - t;
                panelRect.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.94f, t);
                dimGroup.alpha = 1f - t;
            });
            panelRoot.SetActive(false);
            dim.SetActive(false);
        }

        void LateUpdate()
        {
            GameManager gm = GameManager.Instance;
            if (!shown || gm == null) return;
            timeText.text = $"Tiempo: {Mathf.FloorToInt(gm.Elapsed / 60f):00}:{Mathf.FloorToInt(gm.Elapsed % 60f):00}";
            heightText.text = $"Altura maxima: {Mathf.Clamp(gm.BestHeight - gm.startHeight, 0f, gm.finishHeight - gm.startHeight):0.0} m";
        }
    }
}
