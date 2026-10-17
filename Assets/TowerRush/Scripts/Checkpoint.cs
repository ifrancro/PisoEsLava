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

        void OnTriggerEnter(Collider other)
        {
            Activate(other);
        }

        void OnTriggerStay(Collider other)
        {
            Activate(other);
        }

        void Activate(Collider other)
        {
            if (other == null || !GameManager.Playing) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.Simulates) return;
            player.SaveCheckpoint(this);
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
