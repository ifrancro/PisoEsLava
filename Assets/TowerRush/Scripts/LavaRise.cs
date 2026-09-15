using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LavaRise : MonoBehaviour
    {
        public float waitTime = 8f;
        public float initialSpeed = 0.22f;
        public float acceleration = 0.0018f;

        public float SurfaceHeight => transform.position.y + topOffset;

        float startY;
        float topOffset;

        void Awake()
        {
            startY = transform.position.y;
            topOffset = GetComponent<Collider>().bounds.max.y - startY;
        }

        void Update()
        {
            float elapsed = GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.time;
            float t = Mathf.Max(0f, elapsed - waitTime);
            Vector3 position = transform.position;
            position.y = startY + initialSpeed * t + 0.5f * acceleration * t * t;
            transform.position = position;
        }
    }
}
