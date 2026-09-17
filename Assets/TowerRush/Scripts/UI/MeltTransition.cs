using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HeatRise.UI
{
    public sealed class MeltTransition : MonoBehaviour
    {
        public Image image;
        public float coverDuration = 0.9f;
        public float revealDuration = 0.5f;
        const float MaxRadius = 1.6f;

        Material material;

        void Awake()
        {
            material = image.material;
            image.raycastTarget = false;
            gameObject.SetActive(false);
        }

        void SetRadius(float value)
        {
            material.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
            material.SetFloat("_Radius", value);
        }

        public void PlayThenLoadScene(string sceneName)
        {
            gameObject.SetActive(true);
            image.raycastTarget = true;
            StopAllCoroutines();
            StartCoroutine(CoverAndLoad(sceneName));
        }

        public void PlayThenReveal(Func<bool> readyToReveal, Action onCovered = null)
        {
            gameObject.SetActive(true);
            image.raycastTarget = true;
            StopAllCoroutines();
            StartCoroutine(CoverThenReveal(readyToReveal, onCovered));
        }

        IEnumerator CoverAndLoad(string sceneName)
        {
            SetRadius(0f);
            yield return UiTween.Lerp(coverDuration, t => SetRadius(Mathf.LerpUnclamped(0f, MaxRadius, t)), UiTween.EaseOutCubic);
            SceneManager.LoadScene(sceneName);
        }

        IEnumerator CoverThenReveal(Func<bool> readyToReveal, Action onCovered)
        {
            SetRadius(0f);
            yield return UiTween.Lerp(coverDuration, t => SetRadius(Mathf.LerpUnclamped(0f, MaxRadius, t)), UiTween.EaseOutCubic);
            onCovered?.Invoke();
            while (readyToReveal != null && !readyToReveal()) yield return null;
            yield return UiTween.Lerp(revealDuration, t => SetRadius(Mathf.LerpUnclamped(MaxRadius, 0f, t)), UiTween.EaseOutCubic);
            image.raycastTarget = false;
            gameObject.SetActive(false);
        }
    }
}
