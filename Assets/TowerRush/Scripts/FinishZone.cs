using UnityEngine;

namespace HeatRise
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FinishZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (!GameManager.Playing || player == null || !player.Simulates) return;
            NetworkPlayer networkPlayer = player.GetComponent<NetworkPlayer>();
            if (networkPlayer != null) networkPlayer.RequestFinish();
            else GameManager.Instance?.Win();
        }
    }
}
