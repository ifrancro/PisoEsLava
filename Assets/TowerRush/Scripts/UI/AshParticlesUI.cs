using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.UI
{
    public sealed class AshParticlesUI : MonoBehaviour
    {
        public Sprite sprite;
        public int count = 10;
        public float riseDistance = 620f;
        static readonly Color32 Warm = new Color32(0xff, 0xdc, 0xa0, 0xff);
        static readonly Color32 Ember = new Color32(0xff, 0x8a, 0x2a, 0xff);

        void Start()
        {
            for (int i = 0; i < count; i++) StartCoroutine(RunParticle(i));
        }

        IEnumerator RunParticle(int index)
        {
            RectTransform rt = new GameObject("Ash" + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float size = 3f + index % 4;
            rt.sizeDelta = new Vector2(size, size);
            Image image = rt.GetComponent<Image>();
            image.sprite = sprite;
            image.color = index % 3 == 0 ? Warm : Ember;
            image.raycastTarget = false;

            float left = (index * 97 + 13) % 100 / 100f;
            float initialDelay = index * 0.7f % 6f;
            yield return new WaitForSecondsRealtime(initialDelay);

            while (true)
            {
                float duration = 5f + index % 5;
                float elapsed = 0f;
                Vector2 basePos = new Vector2(left * 1600f, 0f);
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    rt.anchoredPosition = basePos + new Vector2(0f, riseDistance * t);
                    rt.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.4f, t);
                    float alpha = t < 0.1f ? t / 0.1f : t < 0.9f ? Mathf.Lerp(1f, 0.7f, (t - 0.1f) / 0.8f) : Mathf.Lerp(0.7f, 0f, (t - 0.9f) / 0.1f);
                    Color c = image.color;
                    c.a = alpha;
                    image.color = c;
                    yield return null;
                }
            }
        }
    }
}
