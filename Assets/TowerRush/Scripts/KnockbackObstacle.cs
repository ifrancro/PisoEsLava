using UnityEngine;

namespace HeatRise
{
    public sealed class KnockbackObstacle : MonoBehaviour
    {
        public float strength = 8f;
        public Transform motionPoint;
        Vector3 Position => motionPoint != null ? motionPoint.position : transform.position;
        Vector3 previousPosition;
        Vector3 velocity;
        RotatingObstacle rotation;

        void OnEnable()
        {
            previousPosition = Position;
            rotation = GetComponentInParent<RotatingObstacle>();
        }

        void LateUpdate()
        {
            velocity = (Position - previousPosition) / Mathf.Max(Time.deltaTime, 0.001f);
            previousPosition = Position;
        }

        public void Hit(PlayerController player)
        {
            if (player == null || !player.Simulates) return;
            Vector3 direction = velocity;
            if (rotation != null)
            {
                float angularSpeed = rotation.speed * Mathf.Deg2Rad;
                if (rotation.swingAngle > 0f)
                {
                    float time = GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.time;
                    float phase = Mathf.Repeat(time * rotation.speed, 360f) * Mathf.Deg2Rad;
                    angularSpeed *= rotation.swingAngle * Mathf.Deg2Rad * Mathf.Cos(phase);
                }
                Vector3 axis = rotation.transform.TransformDirection(rotation.axis.normalized);
                direction = Vector3.Cross(axis * angularSpeed,
                    player.transform.position - rotation.transform.position);
            }
            direction.y = 0f;
            if (direction.sqrMagnitude < (rotation != null ? 0.0001f : 0.1f))
                direction = player.transform.position - Position;
            player.Knockback(direction, strength);
        }

        void OnTriggerEnter(Collider other)
        {
            Hit(other.GetComponentInParent<PlayerController>());
        }

        void OnTriggerStay(Collider other)
        {
            Hit(other.GetComponentInParent<PlayerController>());
        }
    }
}
