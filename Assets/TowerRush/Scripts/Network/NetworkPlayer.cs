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
        public readonly NetworkVariable<byte> Form = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<byte> Cause = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> SavedCheckpointOrder = new NetworkVariable<int>(0);
        readonly NetworkVariable<Vector3> timers = new NetworkVariable<Vector3>(Vector3.zero,
            NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        public PlayerController Player { get; private set; }
        public string Label => "Jugador " + (Slot.Value + 1);
        public Color RaceColor => ColorForSlot(Slot.Value);
        public Vector3 Direction => Time.unscaledTime - lastInput < 0.4f ? direction : Vector3.zero;

        CharacterController controller;
        NetworkTransform networkTransform;
        Renderer[] modelRenderers;
        MaterialPropertyBlock modelProperties;
        Vector3 direction;
        Vector3 sentDirection;
        byte actions;
        uint inputSequence;
        uint receivedSequence;
        bool hasReceivedInput;
        float lastInput;
        float nextInput;
        float nextTimers;

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
            timers.OnValueChanged += TimersChanged;
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
            timers.OnValueChanged -= TimersChanged;
            Slot.OnValueChanged -= SlotChanged;
            if (NetworkRace.Instance != null) NetworkRace.Instance.Unregister(this);
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                bool allow = Alive.Value && NetworkRace.Instance.Racing
                    && !GameManager.Instance.MenuOpen && Application.isFocused;
                SendInput(allow ? Player.ReadDirection() : Vector3.zero,
                    allow ? PlayerController.ReadActions() : (byte)0);
            }
            if (IsServer && Time.unscaledTime >= nextTimers)
            {
                PublishState();
                nextTimers = Time.unscaledTime + 0.1f;
            }
        }

        public void PublishState()
        {
            if (!IsServer) return;
            timers.Value = new Vector3(Player.AbilityRemaining, Player.SmallCooldown, Player.LargeCooldown);
            SavedCheckpointOrder.Value = Player.CheckpointOrder;
        }

        void SendInput(Vector3 input, byte buttons)
        {
            if (IsServer)
            {
                ReceiveInput(input, buttons, ++inputSequence, OwnerClientId);
                return;
            }
            if (buttons == 0 && input == sentDirection && Time.unscaledTime < nextInput) return;
            if (buttons != 0) InputActionsRpc(input, buttons, ++inputSequence);
            else InputDirectionRpc(input, ++inputSequence);
            sentDirection = input;
            nextInput = Time.unscaledTime + 1f / NetworkManager.NetworkConfig.TickRate;
        }

        [Rpc(SendTo.Server, RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
        void InputDirectionRpc(Vector3 input, uint sequence, RpcParams rpc = default)
        {
            ReceiveInput(input, 0, sequence, rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void InputActionsRpc(Vector3 input, byte buttons, uint sequence, RpcParams rpc = default)
        {
            ReceiveInput(input, (byte)(buttons & 31), sequence, rpc.Receive.SenderClientId);
        }

        void ReceiveInput(Vector3 input, byte buttons, uint sequence, ulong sender)
        {
            if (sender != OwnerClientId || !Alive.Value
                || !NetworkRace.Instance.Racing) return;
            if (float.IsNaN(input.x) || float.IsNaN(input.z)
                || float.IsInfinity(input.x) || float.IsInfinity(input.z)) return;
            if (!hasReceivedInput || unchecked((int)(sequence - receivedSequence)) > 0)
            {
                hasReceivedInput = true;
                receivedSequence = sequence;
                input.y = 0f;
                direction = Vector3.ClampMagnitude(input, 1f);
                lastInput = Time.unscaledTime;
            }
            if (buttons != 0)
            {
                actions |= buttons;
                lastInput = Time.unscaledTime;
            }
        }

        public byte ConsumeActions()
        {
            byte value = Time.unscaledTime - lastInput < 0.4f ? actions : (byte)0;
            actions = 0;
            return value;
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
            if (!IsServer || !Alive.Value || !NetworkRace.Instance.Racing) return;
            Cause.Value = cause;
            Alive.Value = false;
            direction = Vector3.zero;
            actions = 0;
            NetworkRace.Instance.CheckSurvivors();
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (!IsServer || !Alive.Value || !NetworkRace.Instance.Racing) return;
            Cause.Value = Form.Value = 0;
            direction = Vector3.zero;
            actions = 0;
            Player.ResetState(position, false);
            PublishState();
            transform.rotation = rotation;
            networkTransform.Teleport(position, rotation, transform.localScale);
        }

        public void ResetPlayer(Vector3 position)
        {
            if (!IsServer) return;
            Ready.Value = false;
            Alive.Value = true;
            Cause.Value = Form.Value = 0;
            direction = Vector3.zero;
            actions = 0;
            Player.ResetState(position);
            PublishState();
            networkTransform.Teleport(position, Quaternion.identity, Vector3.one);
        }

        void FormChanged(byte previous, byte current)
        {
            if (!IsServer) Player.ApplySize((PlayerController.Size)current);
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
            controller.enabled = IsServer && current;
            Player.SetModelVisible(current);
            GetComponent<PlayerCollision>()?.RefreshPairs();
        }

        void TimersChanged(Vector3 previous, Vector3 current)
        {
            if (!IsServer) Player.ApplyRemoteTimers(current);
        }
    }
}
