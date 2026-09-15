using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-50)]
    public sealed class RotatingObstacle : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float speed = 72f;
        public float swingAngle;

        Quaternion start;

        void Awake()
        {
            start = transform.localRotation;
        }

        void Update()
        {
            float time = GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.time;
            float phase = Mathf.Repeat(time * speed, 360f);
            float angle = swingAngle > 0f ? Mathf.Sin(phase * Mathf.Deg2Rad) * swingAngle : phase;
            transform.localRotation = start * Quaternion.AngleAxis(angle, axis);
        }
    }
}
