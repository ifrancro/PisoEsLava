using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.UI
{
    /// Loops a graphic's alpha between two values, matching the mock's glowPulse/dotPulse keyframe loops.
    public sealed class GlowPulse : MonoBehaviour
    {
        public Graphic target;
        public float period = 2.4f;
        [Range(0f, 1f)] public float minAlpha = 0.35f;
        [Range(0f, 1f)] public float maxAlpha = 1f;

        Coroutine loop;

        void OnEnable()
        {
            if (target == null) target = GetComponent<Graphic>();
            loop = StartCoroutine(UiTween.PingPong(this, period, t =>
            {
                Color color = target.color;
                color.a = Mathf.LerpUnclamped(minAlpha, maxAlpha, t);
                target.color = color;
            }));
        }

        void OnDisable()
        {
            if (loop != null) StopCoroutine(loop);
        }
    }
}
