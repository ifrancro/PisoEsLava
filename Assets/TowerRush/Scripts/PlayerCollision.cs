using System.Collections.Generic;
using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCollision : MonoBehaviour
    {
        static readonly List<PlayerCollision> players = new List<PlayerCollision>(4);
        PlayerController player;
        CharacterController body;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            players.Add(this);
        }

        void OnDisable()
        {
            players.Remove(this);
        }

        public void RefreshPairs()
        {
            if (!body.enabled) return;
            foreach (PlayerCollision other in players)
                if (other != this && other.body != null && other.body.enabled)
                    Physics.IgnoreCollision(body, other.body, true);
        }

        void LateUpdate()
        {
            if (!player.Simulates || !GameManager.Playing) return;
            foreach (PlayerCollision other in players)
            {
                if (other == this || other.body == null || !other.player.IsAlive) continue;
                float spacing = Spacing(transform.position, body.height, body.radius,
                    other.transform.position, other.body.height, other.body.radius);
                if (spacing <= 0f) continue;
                Vector3 delta = transform.position - other.transform.position;
                delta.y = 0f;
                float distance = delta.magnitude;
                float overlap = spacing - distance;
                if (overlap <= 0f) continue;
                Vector3 direction = distance > 0.0001f ? delta / distance : Vector3.right;
                float correction = Mathf.Min(overlap + 0.002f, 0.4f);
                body.Move(direction * correction);
            }
            player.RefreshSupportPoint();
        }

        public bool CanResize(float height, float radius)
        {
            foreach (PlayerCollision other in players)
            {
                if (other == this || !other.player.IsAlive) continue;
                float spacing = Spacing(transform.position, height, radius,
                    other.transform.position, other.body.height, other.body.radius);
                Vector3 delta = transform.position - other.transform.position;
                delta.y = 0f;
                if (spacing > 0f && delta.sqrMagnitude < spacing * spacing) return false;
            }
            return true;
        }

        static float Spacing(Vector3 a, float heightA, float radiusA,
            Vector3 b, float heightB, float radiusB)
        {
            float gap = Mathf.Max(0f, Mathf.Max(b.y + radiusB - (a.y + heightA - radiusA),
                a.y + radiusA - (b.y + heightB - radiusB)));
            float radius = radiusA + radiusB;
            return gap >= radius ? 0f : Mathf.Sqrt(radius * radius - gap * gap);
        }
    }
}
