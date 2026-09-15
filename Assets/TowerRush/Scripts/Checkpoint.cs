using UnityEngine;

namespace HeatRise
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [Min(1)] public int order = 1;
        public Transform[] respawnPoints = new Transform[4];
        BoxCollider activationZone;

        void Awake()
        {
            activationZone = GetComponent<BoxCollider>();
            activationZone.isTrigger = true;
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public static bool TryActivateNearby(PlayerController player)
        {
            if (player == null || !player.Simulates || !GameManager.Playing) return false;
            foreach (Collider collider in Physics.OverlapSphere(player.transform.position + Vector3.up * 0.2f,
                0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                Checkpoint checkpoint = collider.GetComponent<Checkpoint>();
                if (checkpoint == null || !checkpoint.Contains(player)) continue;
                player.SaveCheckpoint(checkpoint);
                return true;
            }
            return false;
        }

        bool Contains(PlayerController player)
        {
            if (!isActiveAndEnabled || activationZone == null || !activationZone.enabled) return false;
            Vector3 point = transform.InverseTransformPoint(player.transform.position + Vector3.up * 0.2f);
            return new Bounds(activationZone.center, activationZone.size).Contains(point);
        }

        void OnGUI()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !GameManager.Playing || game.MenuOpen || game.player == null
                || !game.player.IsLocal || !Contains(game.player)) return;
            NetworkPlayer network = game.player.GetComponent<NetworkPlayer>();
            int savedOrder = network != null ? network.SavedCheckpointOrder.Value : game.player.CheckpointOrder;
            string text = savedOrder >= order ? "CHECKPOINT GUARDADO"
                : "F · GUARDAR CHECKPOINT";
            Rect safe = Screen.safeArea;
            float width = Mathf.Min(280f, safe.width - 32f);
            GUI.Box(new Rect(safe.center.x - width * 0.5f, Screen.height - safe.yMin - 108f, width, 32f), text);
        }

        public Transform GetRespawnPoint(PlayerController player)
        {
            NetworkPlayer network = player.GetComponent<NetworkPlayer>();
            int slot = network != null ? network.Slot.Value : 0;
            return respawnPoints != null && slot >= 0 && slot < respawnPoints.Length
                ? respawnPoints[slot] : null;
        }
    }
}
