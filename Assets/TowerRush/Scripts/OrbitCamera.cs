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
        public bool invertX;
        public bool invertY;
        public bool lockCursor = true;
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        public NetworkPlayer SpectatedPlayer { get; private set; }

        float yaw;

        void Start()
        {
            Align();
        }

        void OnDisable()
        {
            ReleaseCursor();
        }

        void LateUpdate()
        {
            NetworkRace race = NetworkRace.Instance;
            NetworkPlayer local = race != null ? race.LocalPlayer : null;
            if (race != null && race.Racing && local != null && !local.Alive.Value)
            {
                int direction = GameInput.SpectatePrevious ? -1 : GameInput.SpectateNext ? 1 : 0;
                if (direction != 0 || SpectatedPlayer == null || !SpectatedPlayer.IsSpawned
                    || !SpectatedPlayer.Alive.Value || SpectatedPlayer.Finished.Value)
                    SelectSpectator(race, local.Slot.Value, direction == 0 ? 1 : direction);
                if (SpectatedPlayer != null) player = SpectatedPlayer.Player;
            }
            else if (local != null)
            {
                SpectatedPlayer = null;
                player = local.Player;
            }
            if (player == null || !GameManager.Playing || GameManager.Instance != null && GameManager.Instance.MenuOpen)
            {
                ReleaseCursor();
                return;
            }

            CaptureCursor();
            Vector2 delta = GameInput.LookDelta;
            yaw += delta.x * sensitivity * (invertX ? -1f : 1f);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity * (invertY ? -1f : 1f), 7f, 65f);
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

        void SelectSpectator(NetworkRace race, int localSlot, int direction)
        {
            int start = SpectatedPlayer != null ? SpectatedPlayer.Slot.Value : localSlot;
            SpectatedPlayer = null;
            for (int step = 1; step <= 4; step++)
            {
                int slot = (start + direction * step + 8) % 4;
                foreach (NetworkPlayer candidate in race.Players)
                {
                    if (candidate == null || !candidate.IsSpawned || candidate.Slot.Value != slot
                        || !candidate.Alive.Value || candidate.Finished.Value) continue;
                    SpectatedPlayer = candidate;
                    return;
                }
            }
        }

        void CaptureCursor()
        {
            if (!lockCursor || Cursor.lockState == CursorLockMode.Locked) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void ReleaseCursor()
        {
            if (Cursor.lockState == CursorLockMode.None) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
