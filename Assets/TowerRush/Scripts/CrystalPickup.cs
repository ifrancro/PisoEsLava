using UnityEngine;

namespace HeatRise
{
    public sealed class CrystalPickup : MonoBehaviour
    {
        public int index;
        float nextAttempt;
        Renderer visual;

        void Awake()
        {
            visual = GetComponent<Renderer>();
        }

        void Update()
        {
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            GameManager game = GameManager.Instance;
            if (game == null) return;
            NetworkRace race = NetworkRace.Instance;
            NetworkPlayer local = race != null ? race.LocalPlayer : null;
            uint bit = 1u << index;
            bool collected = ((local != null ? local.CrystalMask.Value : game.SoloCrystalMask) & bit) != 0;
            visual.enabled = !collected;
            if (race != null && race.InLobby) nextAttempt = 0f;
            if (collected || Time.time < nextAttempt || !GameManager.Playing || game.player == null
                || !game.player.Simulates || (transform.position - game.player.transform.position).sqrMagnitude > 1.7f)
                return;
            nextAttempt = Time.time + 0.5f;
            if (local != null) local.CollectCrystal(index);
            else game.CollectCrystal(index);
        }
    }
}
