using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HeatRise.UI
{
    /// Expands/collapses a panel's height (clipped via RectMask2D) and fades it, matching the mock's
    /// accordion toggle on the host/join buttons (~0.25s).
    [RequireComponent(typeof(LayoutElement))]
    public sealed class AccordionPanel : MonoBehaviour
    {
        public RectTransform content;
        public CanvasGroup group;
        public float duration = 0.25f;

        LayoutElement clip;
        bool expanded;
        Coroutine running;

        void Awake()
        {
            clip = GetComponent<LayoutElement>();
            if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            clip.preferredHeight = 0f;
            if (group != null) group.alpha = 0f;
        }

        public void SetExpanded(bool value)
        {
            if (expanded == value) return;
            expanded = value;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Animate(value));
        }

        IEnumerator Animate(bool show)
        {
            if (show)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
            float target = show ? LayoutUtility.GetPreferredHeight(content) : 0f;
            float start = clip.preferredHeight;
            RectTransform rt = (RectTransform)transform;
            yield return UiTween.Lerp(duration, t =>
            {
                float e = UiTween.EaseOutCubic(t);
                clip.preferredHeight = Mathf.LerpUnclamped(start, target, e);
                if (group != null) group.alpha = show ? e : 1f - e;
                // Changing a LayoutElement value doesn't dirty the parent VerticalLayoutGroup on its
                // own - without this, sibling blocks keep their pre-animation position and overlap.
                LayoutRebuilder.MarkLayoutForRebuild(rt);
            });
        }
    }
}
