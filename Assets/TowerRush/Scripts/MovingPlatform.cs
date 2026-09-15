using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-50)]
    public sealed class MovingPlatform : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 0f, 0.85f);
        public float speed = 1.25f;

        Vector3 start;

        void Awake()
        {
            start = transform.localPosition;
        }

        void Update()
        {
            float time = GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.time;
            transform.localPosition = start + offset * Mathf.Sin(time * speed);
        }
    }
}
