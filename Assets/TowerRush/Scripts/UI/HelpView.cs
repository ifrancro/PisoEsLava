using System.Collections;
using HeatRise;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.UI
{
    /// <summary>Panel de ayuda con el mismo diseno del menu principal. Vive en las dos escenas.</summary>
    public sealed class HelpView : MonoBehaviour
    {
        [Header("Wiring")]
        public GameObject panelRoot;
        public CanvasGroup panelGroup;
        public RectTransform panelRect;
        public GameObject dim;
        public CanvasGroup dimGroup;

        [Header("Contenido")]
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public ScrollRect scroll;
        public Button closeButton;

        bool shown;
        Coroutine anim;

        void Awake()
        {
            closeButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null) GameManager.Instance.CloseHelp();
            });
            titleText.text = HelpContent.Title;
            bodyText.text = HelpContent.BuildRichText();
            panelGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.94f;
            panelRoot.SetActive(false);
            dimGroup.alpha = 0f;
            dim.SetActive(false);
        }

        void Update()
        {
            bool shouldShow = GameManager.Instance != null && GameManager.Instance.HelpOpen;
            if (shouldShow && !shown) Open();
            else if (!shouldShow && shown) Close();
        }

        void Open()
        {
            shown = true;
            panelRoot.SetActive(true);
            dim.SetActive(true);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(UiTween.Lerp(0.22f, t =>
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
