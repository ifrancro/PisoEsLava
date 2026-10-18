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
            && networkPlayer.IsOwner && networkPlayer.Alive.Value && !networkPlayer.Finished.Value;
        public bool IsLocal => networkPlayer == null || networkPlayer.IsOwner;
        public bool IsAlive => networkPlayer == null || networkPlayer.Alive.Value && !networkPlayer.Finished.Value;

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
        readonly VisualRig[] visualRigs = new VisualRig[3];
        Vector3 previousVisualPosition;
        float visualRun;
        float visualAir;
        float visualLanding;
        float visualPhase;
        float visualAppearance;
        bool wasVisuallyAirborne;

        sealed class VisualRig
        {
            public Transform root;
            public Transform leftArm;
            public Transform rightArm;
            public Transform leftLeg;
            public Transform rightLeg;
            public Vector3 position;
            public Vector3 scale;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            networkPlayer = GetComponent<NetworkPlayer>();
            playerCollision = GetComponent<PlayerCollision>();
            baseHeight = controller.height;
            baseRadius = controller.radius;
            GroundHeight = transform.position.y;
            modelScale = model != null ? model.localScale : Vector3.one;
            visualRigs[0] = BuildVisualRig(model);
            visualRigs[1] = BuildVisualRig(smallModel);
            visualRigs[2] = BuildVisualRig(largeModel);
            RestartAnimation();
            SetModelVisible(true);
            if (view == null && Camera.main != null) view = Camera.main.transform;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            Vector3 displacement = position - previousVisualPosition;
            previousVisualPosition = position;
            if (dt <= 0f || networkPlayer != null && !networkPlayer.Alive.Value) return;

            VisualRig rig = visualRigs[(int)CurrentSize];
            if (rig == null || rig.leftArm == null || rig.rightArm == null
                || rig.leftLeg == null || rig.rightLeg == null) return;
            float horizontalSpeed = new Vector2(displacement.x, displacement.z).magnitude / dt;
            float targetRun = GameManager.Playing && IsAlive
                ? Mathf.InverseLerp(0.35f, moveSpeed, horizontalSpeed) : 0f;
            visualRun = Mathf.MoveTowards(visualRun, targetRun, 7f * dt);
            bool airborne = GameManager.Playing && IsAlive && (IsLocal ? !grounded
                : !Physics.Raycast(position + Vector3.up * 0.15f, Vector3.down,
                    0.15f + Mathf.Max(0.16f, Height * 0.08f), obstacleMask,
                    QueryTriggerInteraction.Ignore));
            if (wasVisuallyAirborne && !airborne && visualAir > 0.35f) visualLanding = 1f;
            wasVisuallyAirborne = airborne;
            visualLanding = Mathf.MoveTowards(visualLanding, 0f, 5f * dt);
            visualAir = Mathf.MoveTowards(visualAir, airborne ? 1f : 0f, 8f * dt);
            visualPhase += horizontalSpeed * 1.8f * dt;
            visualAppearance = Mathf.Min(1f, visualAppearance + dt / 0.45f);

            float swing = Mathf.Sin(visualPhase) * 38f * visualRun;
            float jump = displacement.y >= 0f ? -35f : 20f;
            rig.leftLeg.localRotation = Quaternion.Euler(Mathf.Lerp(swing, jump * 0.55f, visualAir), 0f, 0f);
            rig.rightLeg.localRotation = Quaternion.Euler(Mathf.Lerp(-swing, jump, visualAir), 0f, 0f);
            rig.leftArm.localRotation = Quaternion.Euler(Mathf.Lerp(-swing * 0.8f, -45f, visualAir), 0f, 0f);
            rig.rightArm.localRotation = Quaternion.Euler(Mathf.Lerp(swing * 0.8f, -45f, visualAir), 0f, 0f);

            float entrance = 1f - Mathf.Pow(1f - visualAppearance, 3f);
            float bob = Mathf.Abs(Mathf.Sin(visualPhase)) * 0.045f * visualRun * (1f - visualAir)
                + Mathf.Sin(Time.time * 2.6f) * 0.012f * (1f - visualRun) * (1f - visualAir);
            rig.root.localPosition = rig.position + Vector3.up * (bob - 0.3f * (1f - entrance)
                - 0.08f * visualLanding);
            float size = 0.7f + 0.3f * entrance;
            rig.root.localScale = Vector3.Scale(rig.scale,
                new Vector3(size * (1f + 0.08f * visualLanding),
                    size * (1f - 0.12f * visualLanding),
                    size * (1f + 0.08f * visualLanding)));
        }

        static VisualRig BuildVisualRig(Transform root)
        {
            if (root == null) return null;
            return new VisualRig
            {
                root = root,
                position = root.localPosition,
                scale = root.localScale,
                leftArm = MakePivot(root, "Brazo_Izq", "Mano_Izq", 1.08f),
                rightArm = MakePivot(root, "Brazo_Der", "Mano_Der", 1.08f),
                leftLeg = MakePivot(root, "Pierna_Izq", "Bota_Izq", 0.55f),
                rightLeg = MakePivot(root, "Pierna_Der", "Bota_Der", 0.55f)
            };
        }

        static Transform MakePivot(Transform root, string body, string end, float height)
        {
            Transform first = root.Find(body);
            Transform second = root.Find(end);
            if (first == null || second == null) return null;
            Transform pivot = new GameObject(body + "_Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(first.localPosition.x, height, first.localPosition.z);
            first.SetParent(pivot, true);
            second.SetParent(pivot, true);
            return pivot;
        }

        public void RestartAnimation()
        {
            previousVisualPosition = transform.position;
            visualRun = visualAir = visualLanding = visualPhase = visualAppearance = 0f;
            wasVisuallyAirborne = false;
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

            if (GameManager.Instance != null && GameManager.Instance.lava != null
                && transform.position.y <= GameManager.Instance.lava.SurfaceHeight)
            {
                bool fell = transform.position.y < GroundHeight - fallTolerance;
                Eliminate(fell ? (byte)1 : (byte)2,
                    fell ? "Caíste a la lava. No hay un checkpoint disponible." : "La lava te alcanzó.");
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
            if (cause == 1) networkPlayer?.RecordFall();
            if (TryRespawn())
            {
                if (networkPlayer == null)
                {
                    PersistentMusic.Instance?.ReproducirMuerte(false);
                }
                return;
            }

            if (networkPlayer != null)
            {
                networkPlayer.Eliminate(cause);
            }
            else
            {
                PersistentMusic.Instance?.ReproducirMuerte(true);
                GameManager.Instance?.Lose(text);
            }
        }

        public void SaveCheckpoint(Checkpoint value)
        {
            if (!Simulates || !GameManager.Playing || value == null) return;
            if (checkpoint != null && value.order <= checkpoint.order) return;
            if (value.GetRespawnPoint(this) == null) return;
            checkpoint = value;
            networkPlayer?.SaveCheckpointRpc(value.order);
            PersistentMusic.Instance?.ReproducirCheckpoint();
            GameManager.Instance?.ShowMessage("CHECKPOINT GUARDADO");
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
            Size previousSize = CurrentSize;
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
            {
                model.localScale = modelScale * (size == Size.Small && smallModel == null
                    || size == Size.Large && largeModel == null ? scale : 1f);
                if (visualRigs[0] != null) visualRigs[0].scale = model.localScale;
            }
            SetModelVisible(modelVisible);
            playerCollision?.RefreshPairs();

            if (IsLocal && previousSize != size)
            {
                if (size == Size.Small)
                {
                    PersistentMusic.Instance?.ReproducirMini();
                }
                else if (size == Size.Large)
                {
                    PersistentMusic.Instance?.ReproducirGrande();
                }
            }
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
            RestartAnimation();
            controller.enabled = Simulates;
            playerCollision?.RefreshPairs();
        }

        public void RefreshSupportPoint()
        {
            if (grounded && support != null)
                supportLocalPoint = support.InverseTransformPoint(transform.position);
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
