using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeatRise.UI
{
    /// Scales a target RectTransform on hover/press, matching the mock's transform:scale hover/active states.
    public sealed class UIHoverPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform target;
        public float hoverScale = 1.04f;
        public float pressScale = 0.97f;
        public float duration = 0.12f;

        Coroutine active;
        bool pointerDown;
        bool pointerOver;

        void Awake()
        {
            if (target == null) target = transform as RectTransform;
        }

        public void OnPointerEnter(PointerEventData eventData) { pointerOver = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { pointerOver = false; pointerDown = false; Apply(); }
        public void OnPointerDown(PointerEventData eventData) { pointerDown = true; Apply(); }
        public void OnPointerUp(PointerEventData eventData) { pointerDown = false; Apply(); }

        void Apply()
        {
            float scale = pointerDown ? pressScale : pointerOver ? hoverScale : 1f;
            if (active != null) StopCoroutine(active);
            active = StartCoroutine(UiTween.ScaleTo(target, target.localScale, Vector3.one * scale, duration, UiTween.EaseOutCubic));
        }

        void OnDisable()
        {
            pointerDown = pointerOver = false;
            if (target != null) target.localScale = Vector3.one;
        }
    }
}
