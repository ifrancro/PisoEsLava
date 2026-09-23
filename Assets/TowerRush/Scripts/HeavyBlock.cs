using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-50)]
    public sealed class HeavyBlock : MonoBehaviour
    {
        public Transform destination;
        public float speed = 5.2f;
        public int NetworkIndex { get; set; }
        public bool IsMoving { get; private set; }
        public bool HasMoved { get; private set; }

        Vector3 start;
        float startedAt;

        void Awake()
        {
            start = transform.position;
        }

        public void Push(PlayerController player)
        {
            if (!GameManager.Playing || !player.Simulates || IsMoving || HasMoved || destination == null) return;
            if (player.CurrentSize != PlayerController.Size.Large) return;
            if ((player.transform.position - transform.position).sqrMagnitude > 25f) return;
            if (GameManager.Online) NetworkRace.Instance.PushBlock(this);
            else BeginMove(GameManager.Instance.Elapsed);
        }

        public void BeginMove(float time)
        {
            if (IsMoving || HasMoved) return;
            startedAt = time;
            IsMoving = true;
            PersistentMusic.Instance?.ReproducirCaja();
        }

        public void ResetBlock()
        {
            transform.position = start;
            IsMoving = HasMoved = false;
        }

        void Update()
        {
            if (!IsMoving || destination == null) return;
            float time = Mathf.Max(0f, GameManager.Instance.Elapsed - startedAt);
            transform.position = Vector3.MoveTowards(start, destination.position, speed * time);
            if ((transform.position - destination.position).sqrMagnitude > 0.0001f) return;
            transform.position = destination.position;
            IsMoving = false;
            HasMoved = true;
        }
    }
}
