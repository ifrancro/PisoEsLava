using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-120)]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRace : NetworkBehaviour
    {
        public static NetworkRace Instance { get; private set; }
        public HeavyBlock[] blocks;
        public FragilePlatform[] fragilePlatforms;
        public Transform[] spawnPoints;
        public readonly List<NetworkPlayer> Players = new List<NetworkPlayer>(4);
        public readonly NetworkVariable<byte> Stage = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<ulong> Winner = new NetworkVariable<ulong>(ulong.MaxValue);
        public readonly NetworkVariable<int> WinnerSlot = new NetworkVariable<int>(-1);
        readonly NetworkVariable<double> startTime = new NetworkVariable<double>(0);
        readonly NetworkVariable<double> finishTime = new NetworkVariable<double>(0);

        public bool InLobby => IsSpawned && Stage.Value == 0;
        public bool Racing => IsSpawned && Stage.Value == 2;
        public float Elapsed => !IsSpawned || Stage.Value == 0 ? 0f :
            (float)System.Math.Max(0, (Stage.Value == 3 ? finishTime.Value : NetworkManager.ServerTime.Time) - startTime.Value);
        public int Countdown => IsSpawned ? Mathf.Max(0,
            Mathf.CeilToInt((float)(startTime.Value - NetworkManager.ServerTime.Time))) : 0;
        public NetworkPlayer LocalPlayer => Players.Find(p => p.IsOwner);
        public bool AllReady
        {
            get
            {
                if (!InLobby || Players.Count < 2 || Players.Count != NetworkManager.ConnectedClients.Count) return false;
                foreach (NetworkPlayer player in Players) if (!player.Ready.Value) return false;
                return true;
            }
        }

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < blocks.Length; i++) blocks[i].NetworkIndex = i;
            for (int i = 0; i < fragilePlatforms.Length; i++) fragilePlatforms[i].NetworkIndex = i;
        }

        public override void OnNetworkSpawn()
        {
            GameManager.Instance.ResetNetworkView();
            WinnerSlot.OnValueChanged += WinnerSlotChanged;
            Stage.OnValueChanged += StageChanged;
            if (Stage.Value == 2) PersistentMusic.Instance?.ReiniciarMusicaAmbiente();
            else PersistentMusic.Instance?.ReproducirIntro();
        }

        public override void OnNetworkDespawn()
        {
            WinnerSlot.OnValueChanged -= WinnerSlotChanged;
            Stage.OnValueChanged -= StageChanged;
        }

        void StageChanged(byte previous, byte current)
        {
            if (current == 2)
            {
                PersistentMusic.Instance?.ReiniciarMusicaAmbiente();
            }
            else if (current == 0 || current == 1)
            {
                PersistentMusic.Instance?.ReproducirIntro();
            }
        }

        void WinnerSlotChanged(int previous, int current)
        {
            if (current >= 0)
            {
                PersistentMusic.Instance?.ReproducirVictoria();
            }
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            if (Stage.Value == 1)
            {
                if (Players.Count < 2)
                {
                    Stage.Value = 0;
                    startTime.Value = 0;
                    foreach (NetworkPlayer player in Players) player.Ready.Value = false;
                }
                else if (NetworkManager.ServerTime.Time >= startTime.Value)
                    Stage.Value = 2;
            }
            if (Racing) CheckSurvivors();
        }

        public void Register(NetworkPlayer player)
        {
            if (!Players.Contains(player)) Players.Add(player);
        }

        public void Unregister(NetworkPlayer player)
        {
            Players.Remove(player);
        }

        public void StartRace()
        {
            if (!IsServer || !AllReady) return;
            startTime.Value = NetworkManager.ServerTime.Time + 3.0;
            Stage.Value = 1;
        }

        public void TryFinish(NetworkPlayer player)
        {
            if (!IsServer || !Racing || !player.Alive.Value) return;
            Winner.Value = player.OwnerClientId;
            WinnerSlot.Value = player.Slot.Value;
            finishTime.Value = NetworkManager.ServerTime.Time;
            Stage.Value = 3;
        }

        public void CheckSurvivors()
        {
            if (!IsServer || !Racing) return;
            foreach (NetworkPlayer player in Players) if (player.Alive.Value) return;
            Winner.Value = ulong.MaxValue;
            WinnerSlot.Value = -1;
            finishTime.Value = NetworkManager.ServerTime.Time;
            Stage.Value = 3;
        }

        public void PushBlock(HeavyBlock block)
        {
            if (!IsSpawned || !Racing || block.IsMoving || block.HasMoved) return;
            if (IsServer) MoveBlockRpc(block.NetworkIndex, Elapsed);
            else RequestPushBlockRpc(block.NetworkIndex);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void RequestPushBlockRpc(int index, RpcParams rpc = default)
        {
            if (!Racing || index < 0 || index >= blocks.Length) return;
            HeavyBlock block = blocks[index];
            if (block.IsMoving || block.HasMoved) return;
            MoveBlockRpc(index, Elapsed);
        }

        [Rpc(SendTo.Everyone, RequireOwnership = true)]
        void MoveBlockRpc(int index, float time, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId
                || index < 0 || index >= blocks.Length) return;
            blocks[index].BeginMove(time);
        }

        public void ActivatePlatform(FragilePlatform platform)
        {
            if (!IsSpawned || !Racing || platform.Activated) return;
            if (IsServer) BreakPlatformRpc(platform.NetworkIndex, Elapsed + platform.delay);
            else RequestActivatePlatformRpc(platform.NetworkIndex);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void RequestActivatePlatformRpc(int index, RpcParams rpc = default)
        {
            if (!Racing || index < 0 || index >= fragilePlatforms.Length) return;
            FragilePlatform platform = fragilePlatforms[index];
            if (platform.Activated) return;
            BreakPlatformRpc(index, Elapsed + platform.delay);
        }

        [Rpc(SendTo.Everyone, RequireOwnership = true)]
        void BreakPlatformRpc(int index, float time, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId
                || index < 0 || index >= fragilePlatforms.Length) return;
            fragilePlatforms[index].BeginBreak(time);
        }

        public void ReturnToLobby()
        {
            if (!IsServer || Stage.Value != 3) return;
            Stage.Value = 0;
            startTime.Value = finishTime.Value = 0;
            Winner.Value = ulong.MaxValue;
            WinnerSlot.Value = -1;
            ResetWorldRpc();
            foreach (NetworkPlayer player in Players)
                player.ResetPlayer(spawnPoints[player.Slot.Value].position);
        }

        [Rpc(SendTo.Everyone, RequireOwnership = true)]
        void ResetWorldRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId) return;
            foreach (HeavyBlock block in blocks) block.ResetBlock();
            foreach (FragilePlatform platform in fragilePlatforms) platform.ResetPlatform();
            GameManager.Instance.ResetNetworkView();
            PersistentMusic.Instance?.ReiniciarMusicaAmbiente();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
    }
}
