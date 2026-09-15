using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-50)]
    public sealed class HammerSwing : MonoBehaviour
    {
        public float angle = 24f;
        public float angularFrequency = 1.8f;
        public float phase;

        Quaternion rest;

        void Awake()
        {
            rest = transform.localRotation;
        }

        void Update()
        {
            float time = GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.time;
            transform.localRotation = rest * Quaternion.AngleAxis(
                Mathf.Sin(time * angularFrequency + phase) * angle, Vector3.right);
        }
    }
}
