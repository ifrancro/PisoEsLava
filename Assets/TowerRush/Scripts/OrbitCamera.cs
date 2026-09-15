using UnityEngine;

namespace HeatRise
{
    public sealed class OrbitCamera : MonoBehaviour
    {
        public PlayerController player;
        public Transform[] route;
        public float distance = 10f;
        public float sensitivity = 0.2f;
        public float pitch = 28f;
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        float yaw;
        Vector2 previousMouse;
        bool wasDragging;

        void Start()
        {
            previousMouse = GameInput.MousePosition;
            Align();
        }

        void LateUpdate()
        {
            Vector2 mouse = GameInput.MousePosition;
            Vector2 delta = mouse - previousMouse;
            previousMouse = mouse;
            if (player == null || !GameManager.Playing || GameManager.Instance != null && GameManager.Instance.MenuOpen)
            {
                wasDragging = false;
                return;
            }

            bool dragging = GameInput.OrbitHeld;
            if (dragging && wasDragging)
            {
                yaw -= delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, 7f, 65f);
            }
            wasDragging = dragging;
            distance = Mathf.Clamp(distance - GameInput.Scroll, 4f, 16f);
            if (GameInput.Align) Align();

            Vector3 pivot = player.transform.position + Vector3.up * Mathf.Min(player.Height * 0.65f, 1.8f);
            float cameraPitch = pitch;
            if (player.CurrentSize == PlayerController.Size.Small
                && Physics.Raycast(pivot, Vector3.up, 0.8f, obstacleMask, QueryTriggerInteraction.Ignore))
                cameraPitch = 2.5f;

            float y = yaw * Mathf.Deg2Rad;
            float p = cameraPitch * Mathf.Deg2Rad;
            Vector3 boom = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y));
            float length = distance;
            if (Physics.SphereCast(pivot, 0.15f, boom, out RaycastHit hit, distance,
                obstacleMask, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(0.08f, hit.distance - 0.05f);

            transform.position = pivot + boom * length;
            transform.LookAt(pivot);
        }

        public void Align()
        {
            if (player == null || route == null) return;
            for (int i = 0; i < route.Length; i++)
            {
                if (route[i] == null || route[i].position.y <= player.GroundHeight + 0.3f) continue;
                Vector3 back = player.transform.position - route[i].position;
                yaw = Mathf.Atan2(back.x, back.z) * Mathf.Rad2Deg;
                return;
            }
        }
    }
}
