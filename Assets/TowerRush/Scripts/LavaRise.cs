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
            float distance = Distance(t);
            float eruptionAt = GameManager.Instance != null ? GameManager.Instance.EruptionAt : -1f;
            if (eruptionAt >= 0f)
                distance += 0.5f * Mathf.Max(0f, distance - Distance(Mathf.Max(0f, eruptionAt - waitTime)));
            Vector3 position = transform.position;
            position.y = startY + distance;
            transform.position = position;
        }

        float Distance(float time)
        {
            return initialSpeed * time + 0.5f * acceleration * time * time;
        }
    }
}
