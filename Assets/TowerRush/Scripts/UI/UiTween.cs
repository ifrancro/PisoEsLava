using System;
using System.Collections;
using UnityEngine;

namespace HeatRise.UI
{
    public static class UiTween
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public static IEnumerator Lerp(float duration, Action<float> setter, Func<float, float> ease = null, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (duration <= 0f) { setter(1f); yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                setter(ease != null ? ease(t) : t);
                yield return null;
            }
            setter(1f);
        }

        public static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration, float delay = 0f)
        {
            group.alpha = from;
            yield return Lerp(duration, t => group.alpha = Mathf.LerpUnclamped(from, to, t), EaseOutCubic, delay);
        }

        public static IEnumerator ScaleTo(Transform target, Vector3 from, Vector3 to, float duration, Func<float, float> ease = null, float delay = 0f)
        {
            target.localScale = from;
            yield return Lerp(duration, t => target.localScale = Vector3.LerpUnclamped(from, to, t), ease ?? EaseOutBack, delay);
        }

        public static IEnumerator SlideAndFade(RectTransform target, CanvasGroup group, Vector2 fromOffset, float duration, float delay = 0f)
        {
            Vector2 basePos = target.anchoredPosition;
            group.alpha = 0f;
            target.anchoredPosition = basePos + fromOffset;
            yield return Lerp(duration, t =>
            {
                float e = EaseOutCubic(t);
                target.anchoredPosition = Vector2.LerpUnclamped(basePos + fromOffset, basePos, e);
                group.alpha = t;
            }, null, delay);
        }

        public static IEnumerator PingPong(MonoBehaviour owner, float period, Action<float> setter)
        {
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < period)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.PingPong(elapsed / period * 2f, 1f);
                    setter(t);
                    yield return null;
                }
            }
        }
    }
}
