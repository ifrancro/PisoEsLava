using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    [RequireComponent(typeof(PlayerController))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public readonly NetworkVariable<bool> Alive = new NetworkVariable<bool>(true);
        public readonly NetworkVariable<bool> Ready = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<byte> Form = new NetworkVariable<byte>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<byte> Cause = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> SavedCheckpointOrder = new NetworkVariable<int>(0);

        public PlayerController Player { get; private set; }
        public string Label => "Jugador " + (Slot.Value + 1);
        public Color RaceColor => ColorForSlot(Slot.Value);
        public Vector3 Direction => direction;

        CharacterController controller;
        NetworkTransform networkTransform;
        Renderer[] modelRenderers;
        MaterialPropertyBlock modelProperties;
        Vector3 direction;
        byte actions;

        void Awake()
        {
            Player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            networkTransform = GetComponent<NetworkTransform>();
            modelRenderers = GetComponentsInChildren<Renderer>(true);
            modelProperties = new MaterialPropertyBlock();
            controller.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            Form.OnValueChanged += FormChanged;
            Alive.OnValueChanged += AliveChanged;
            NetworkRace.Instance.Register(this);
            if (IsServer) Slot.Value = LanMenu.Instance.GetSlot(OwnerClientId);
            Slot.OnValueChanged += SlotChanged;
            SlotChanged(Slot.Value, Slot.Value);
            Player.ApplySize((PlayerController.Size)Form.Value);
            AliveChanged(false, Alive.Value);
            if (IsOwner)
            {
                GameManager.Instance.player = Player;
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Player.view = camera.transform;
                    OrbitCamera orbit = camera.GetComponent<OrbitCamera>();
                    if (orbit != null)
                    {
                        orbit.player = Player;
                        orbit.Align();
                    }
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            Form.OnValueChanged -= FormChanged;
            Alive.OnValueChanged -= AliveChanged;
            Slot.OnValueChanged -= SlotChanged;
            if (NetworkRace.Instance != null) NetworkRace.Instance.Unregister(this);
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            bool allow = Alive.Value && NetworkRace.Instance.Racing
                && !GameManager.Instance.MenuOpen && Application.isFocused;
            direction = allow ? Player.ReadDirection() : Vector3.zero;
            actions = allow ? PlayerController.ReadActions() : (byte)0;
        }

        public byte ConsumeActions()
        {
            byte value = actions;
            actions = 0;
            return value;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SaveCheckpointRpc(int order, RpcParams rpc = default)
        {
            if (order > SavedCheckpointOrder.Value) SavedCheckpointOrder.Value = order;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SetReadyRpc(bool value, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == OwnerClientId && NetworkRace.Instance.InLobby)
                Ready.Value = value;
        }

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        public void ShowMessageRpc(string text, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == Unity.Netcode.NetworkManager.ServerClientId)
                GameManager.Instance.ShowMessage(text);
        }

        public void Eliminate(byte cause)
        {
            if (!IsOwner || !Alive.Value || !NetworkRace.Instance.Racing) return;
            RequestEliminateRpc(cause, transform.position);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void RequestEliminateRpc(byte cause, Vector3 position, RpcParams rpc = default)
        {
            if (!Alive.Value || !NetworkRace.Instance.Racing) return;
            if (cause == 2)
            {
                LavaRise lava = GameManager.Instance != null ? GameManager.Instance.lava : null;
                if (lava == null || position.y > lava.SurfaceHeight + 0.5f) return;
            }
            Cause.Value = cause;
            Alive.Value = false;
            NetworkRace.Instance.CheckSurvivors();
        }

        public void RequestFinish()
        {
            if (!IsOwner || !Alive.Value || !NetworkRace.Instance.Racing) return;
            RequestFinishRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void RequestFinishRpc(RpcParams rpc = default)
        {
            NetworkRace.Instance.TryFinish(this);
        }

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        public void PlayCheckpointSoundRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == Unity.Netcode.NetworkManager.ServerClientId)
                PersistentMusic.Instance?.ReproducirCheckpoint();
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (!IsOwner || !Alive.Value || !NetworkRace.Instance.Racing) return;
            Form.Value = 0;
            direction = Vector3.zero;
            actions = 0;
            Player.ResetState(position, false);
            transform.rotation = rotation;
            networkTransform.Teleport(position, rotation, transform.localScale);
            PersistentMusic.Instance?.ReproducirMuerte(false);
        }

        public void ResetPlayer(Vector3 position)
        {
            if (!IsServer) return;
            Ready.Value = false;
            Alive.Value = true;
            Cause.Value = 0;
            direction = Vector3.zero;
            actions = 0;
            ResetOwnerRpc(position);
        }

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        void ResetOwnerRpc(Vector3 position, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId) return;
            Form.Value = 0;
            Player.ResetState(position);
            transform.rotation = Quaternion.identity;
            networkTransform.Teleport(position, Quaternion.identity, Vector3.one);
        }

        void FormChanged(byte previous, byte current)
        {
            if (!IsOwner) Player.ApplySize((PlayerController.Size)current);
        }

        public static Color ColorForSlot(int slot)
        {
            switch (slot)
            {
                case 1: return new Color32(74, 173, 255, 255);
                case 2: return new Color32(87, 224, 136, 255);
                case 3: return new Color32(255, 164, 66, 255);
                default: return new Color32(255, 102, 170, 255);
            }
        }

        void SlotChanged(int previous, int current)
        {
            foreach (Renderer modelRenderer in modelRenderers)
            {
                modelRenderer.GetPropertyBlock(modelProperties);
                modelProperties.SetColor("_Color", RaceColor);
                modelRenderer.SetPropertyBlock(modelProperties);
            }
        }

        void AliveChanged(bool previous, bool current)
        {
            controller.enabled = IsOwner && current;
            Player.SetModelVisible(current);
            GetComponent<PlayerCollision>()?.RefreshPairs();

            if (previous && !current)
            {
                if (IsOwner)
                {
                    PersistentMusic.Instance?.ReproducirMuerte(true);
                }
                else
                {
                    PersistentMusic.Instance?.ReproducirMuerte(false);
                }
            }
        }
    }
}
