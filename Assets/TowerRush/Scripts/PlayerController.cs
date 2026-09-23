using UnityEngine;

namespace HeatRise
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public enum Size { Normal, Small, Large }

        public Transform model;
        public Transform smallModel;
        public Transform largeModel;
        public Transform view;
        public float moveSpeed = 5.8f;
        public float jumpHeight = 2.6f;
        public float gravity = -22f;
        public float smallScale = 0.46f;
        public float largeScale = 1.55f;
        public float abilityDuration = 7f;
        public float cooldown = 4f;
        public float fallTolerance = 0.36f;
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        public Size CurrentSize { get; private set; }
        public float AbilityRemaining { get; private set; }
        public float SmallCooldown { get; private set; }
        public float LargeCooldown { get; private set; }
        public float GroundHeight { get; private set; }
        public float Height => controller.height;
        public float Radius => controller.radius;
        public int CheckpointOrder => checkpoint != null ? checkpoint.order : 0;
        public bool Simulates => networkPlayer == null || networkPlayer.IsSpawned
            && networkPlayer.IsServer && networkPlayer.Alive.Value;
        public bool IsLocal => networkPlayer == null || networkPlayer.IsOwner;

        readonly Collider[] overlaps = new Collider[32];
        CharacterController controller;
        NetworkPlayer networkPlayer;
        PlayerCollision playerCollision;
        Checkpoint checkpoint;
        Transform support;
        Transform nextSupport;
        Vector3 supportLocalPoint;
        Vector3 pushVelocity;
        Vector3 modelScale;
        float baseHeight;
        float baseRadius;
        float verticalSpeed;
        float hitCooldown;
        float jumpBuffer;
        bool grounded = true;
        bool modelVisible = true;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            networkPlayer = GetComponent<NetworkPlayer>();
            playerCollision = GetComponent<PlayerCollision>();
            baseHeight = controller.height;
            baseRadius = controller.radius;
            GroundHeight = transform.position.y;
            modelScale = model != null ? model.localScale : Vector3.one;
            SetModelVisible(true);
            if (view == null && Camera.main != null) view = Camera.main.transform;
        }

        void Update()
        {
            if (!Simulates || !GameManager.Playing) return;
            float dt = Time.deltaTime;
            Physics.SyncTransforms();
            byte actions = networkPlayer != null ? networkPlayer.ConsumeActions() : ReadActions();
            hitCooldown = Mathf.Max(0f, hitCooldown - dt);
            jumpBuffer = (actions & 1) != 0 ? 0.12f : Mathf.Max(0f, jumpBuffer - dt);
            SmallCooldown = Mathf.Max(0f, SmallCooldown - dt);
            LargeCooldown = Mathf.Max(0f, LargeCooldown - dt);

            if (CurrentSize != Size.Normal)
            {
                AbilityRemaining = Mathf.Max(0f, AbilityRemaining - dt);
                if (AbilityRemaining <= 0f) ChangeSize(Size.Normal, true);
            }

            if ((actions & 2) != 0) ChangeSize(Size.Small);
            if ((actions & 4) != 0) ChangeSize(Size.Large);
            if ((actions & 8) != 0) ChangeSize(Size.Normal);
            if ((actions & 16) != 0) PushBlock();

            Vector3 carry = Vector3.zero;
            if (grounded && support != null)
            {
                carry = support.TransformPoint(supportLocalPoint) - transform.position;
                GroundHeight += carry.y;
            }

            Vector3 direction = networkPlayer != null ? networkPlayer.Direction : ReadDirection();
            float speed = moveSpeed * (CurrentSize == Size.Small ? 1.35f : CurrentSize == Size.Large ? 0.74f : 1f);

            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (grounded && jumpBuffer > 0f)
            {
                verticalSpeed = Mathf.Sqrt(-2f * gravity * jumpHeight * (CurrentSize == Size.Large ? 0.93f : 1f));
                grounded = false;
                jumpBuffer = 0f;
                support = null;
            }

            verticalSpeed += gravity * dt;
            nextSupport = null;
            Vector3 movement = direction * speed + pushVelocity + Vector3.up * verticalSpeed;
            CollisionFlags flags = controller.Move(movement * dt + carry);

            if (transform.position.y < GroundHeight - fallTolerance)
            {
                Eliminate(1, "Te caiste. No hay un checkpoint disponible.");
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.lava != null
                && transform.position.y <= GameManager.Instance.lava.SurfaceHeight)
            {
                Eliminate(2, "La lava te alcanzo.");
                return;
            }

            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            grounded = (flags & CollisionFlags.Below) != 0 && verticalSpeed <= 0f;
            if (grounded)
            {
                verticalSpeed = -2f;
                GroundHeight = transform.position.y;
                support = nextSupport;
                if (support != null) supportLocalPoint = support.InverseTransformPoint(transform.position);
            }
            else support = null;

            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, 12f * dt);
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * dt);
        }

        public Vector3 ReadDirection()
        {
            Vector2 input = GameInput.Move;
            Vector3 forward = view != null ? view.forward : Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            return forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
        }

        public static byte ReadActions()
        {
            return (byte)((GameInput.Jump ? 1 : 0) | (GameInput.Small ? 2 : 0)
                | (GameInput.Large ? 4 : 0) | (GameInput.Normal ? 8 : 0) | (GameInput.Push ? 16 : 0));
        }

        void Eliminate(byte cause, string text)
        {
            if (TryRespawn()) return;
            if (networkPlayer != null) networkPlayer.Eliminate(cause);
            else GameManager.Instance?.Lose(text);
        }

        public void SaveCheckpoint(Checkpoint value)
        {
            if (!Simulates || !GameManager.Playing || value == null) return;
            if (checkpoint != null && value.order <= checkpoint.order) return;
            if (value.GetRespawnPoint(this) == null) return;
            checkpoint = value;
            Notify("Checkpoint guardado.");
        }

        bool TryRespawn()
        {
            if (!Simulates || !GameManager.Playing || checkpoint == null) return false;
            Transform point = checkpoint.GetRespawnPoint(this);
            if (point == null) return false;
            Vector3 position = point.position;
            LavaRise lava = GameManager.Instance != null ? GameManager.Instance.lava : null;
            if (lava != null && position.y <= lava.SurfaceHeight + 0.1f) return false;
            Quaternion rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);

            if (networkPlayer != null) networkPlayer.Respawn(position, rotation);
            else
            {
                ResetState(position, false);
                transform.rotation = rotation;
            }

            Notify("Volviste al checkpoint.");
            return true;
        }

        void Notify(string text)
        {
            if (networkPlayer != null) networkPlayer.ShowMessageRpc(text);
            else GameManager.Instance?.ShowMessage(text);
        }

        public bool ChangeSize(Size size, bool automatic = false)
        {
            if (size == CurrentSize) return false;
            if (size == Size.Small && SmallCooldown > 0f || size == Size.Large && LargeCooldown > 0f)
            {
                if (!automatic) Notify("La habilidad esta recargando.");
                return false;
            }

            float scale = size == Size.Small ? smallScale : size == Size.Large ? largeScale : 1f;
            float height = baseHeight * scale;
            float radius = baseRadius * scale;
            if (height > controller.height || radius > controller.radius)
            {
                if (playerCollision != null && !playerCollision.CanResize(height, radius))
                {
                    if (!automatic) Notify("Alejate un poco del otro jugador para crecer.");
                    return false;
                }
                Vector3 bottom = transform.position + Vector3.up * (radius + 0.035f);
                Vector3 top = transform.position + Vector3.up * (height - radius - 0.035f);
                int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
                    obstacleMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (overlaps[i].transform.IsChildOf(transform)) continue;
                    if (!automatic) Notify("No hay espacio para crecer. Sali del conducto.");
                    return false;
                }
            }

            if (CurrentSize == Size.Small) SmallCooldown = cooldown;
            if (CurrentSize == Size.Large) LargeCooldown = cooldown;
            AbilityRemaining = size == Size.Normal ? 0f : abilityDuration;
            ApplySize(size);
            if (networkPlayer != null) networkPlayer.Form.Value = (byte)size;
            return true;
        }

        public void ApplySize(Size size)
        {
            CurrentSize = size;
            float scale = size == Size.Small ? smallScale : size == Size.Large ? largeScale : 1f;
            float height = baseHeight * scale;
            float radius = baseRadius * scale;
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.up * (height * 0.5f);
            controller.skinWidth = Mathf.Min(0.025f, radius * 0.1f);
            controller.stepOffset = Mathf.Min(0.1f, height * 0.15f);
            if (model != null)
                model.localScale = modelScale * (size == Size.Small && smallModel == null
                    || size == Size.Large && largeModel == null ? scale : 1f);
            SetModelVisible(modelVisible);
            playerCollision?.RefreshPairs();
        }

        public void SetModelVisible(bool visible)
        {
            modelVisible = visible;
            Transform selected = CurrentSize == Size.Small && smallModel != null ? smallModel
                : CurrentSize == Size.Large && largeModel != null ? largeModel : model;
            if (model != null) model.gameObject.SetActive(visible && selected == model);
            if (smallModel != null) smallModel.gameObject.SetActive(visible && selected == smallModel);
            if (largeModel != null) largeModel.gameObject.SetActive(visible && selected == largeModel);
        }

        public void ResetState(Vector3 position, bool clearCheckpoint = true)
        {
            if (clearCheckpoint) checkpoint = null;
            controller.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.identity;
            verticalSpeed = 0f;
            pushVelocity = Vector3.zero;
            jumpBuffer = hitCooldown = 0f;
            SmallCooldown = LargeCooldown = AbilityRemaining = 0f;
            grounded = true;
            support = nextSupport = null;
            GroundHeight = position.y;
            ApplySize(Size.Normal);
            controller.enabled = Simulates;
            playerCollision?.RefreshPairs();
        }

        public void RefreshSupportPoint()
        {
            if (grounded && support != null)
                supportLocalPoint = support.InverseTransformPoint(transform.position);
        }

        public void ApplyRemoteTimers(Vector3 timers)
        {
            AbilityRemaining = timers.x;
            SmallCooldown = timers.y;
            LargeCooldown = timers.z;
        }

        void PushBlock()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up,
                3.6f, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
            HeavyBlock nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                HeavyBlock block = overlaps[i].GetComponentInParent<HeavyBlock>();
                if (block == null || block.IsMoving || block.HasMoved) continue;
                float distance = (block.transform.position - transform.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = block;
            }

            if (nearest == null) return;
            if (CurrentSize != Size.Large)
            {
                Notify("Usa E para hacerte gigante, despues F para empujar.");
                return;
            }
            nearest.Push(this);
        }

        public void Knockback(Vector3 direction, float strength)
        {
            if (!Simulates || !GameManager.Playing || hitCooldown > 0f) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = -transform.forward;
            pushVelocity = direction.normalized * strength * (CurrentSize == Size.Large ? 0.55f : 1f);
            hitCooldown = 0.65f;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!Simulates || hit.collider.GetComponentInParent<PlayerController>() != null) return;
            if (hit.normal.y > 0.55f)
            {
                nextSupport = hit.collider.transform;
                hit.collider.GetComponentInParent<FragilePlatform>()?.Activate();
            }

            hit.collider.GetComponentInParent<KnockbackObstacle>()?.Hit(this);
        }
    }
}
