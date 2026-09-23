using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise
{
    public sealed class LanMenu : MonoBehaviour
    {
        public static LanMenu Instance { get; private set; }
        public NetworkManager network;
        public UnityTransport transport;
        public ushort port = 7777;
        public string hostAddress = "";
        public bool useInternet = true;
        public bool encryptOnline = true;
        public string joinCode = "";
        public string RoomCode { get; private set; } = "";
        public string RelayConnection => encryptOnline ? "dtls" : "udp";

        public const string Version = "3.0";
        public static string VersionLabel => "EL PISO ES LAVA · " + Version;
        const string Protocol = "HeatRise-LAN-" + Version;
        public event Action OnConnected;
        public bool Connecting => connecting;
        public bool Leaving => leaving;
        public bool IsHost => network != null && network.IsHost;
        public bool IsConnected => network != null && network.IsConnectedClient;
        public string Message => message;
        public string Addresses => addresses;
        readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();
        static string previousMessage = "";
        string message;
        string addresses;
        bool connecting;
        bool leaving;
        Task openingConnection;
        ulong localId;

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
            if (NetworkManager.Singleton != null && network != NetworkManager.Singleton)
            {
                if (network != null) Destroy(network.gameObject);
                network = NetworkManager.Singleton;
                transport = network.GetComponent<UnityTransport>();
            }
            message = previousMessage;
            previousMessage = "";
            addresses = LocalAddresses();
            localId = network.LocalClientId;
            if (!network.IsListening)
            {
                network.NetworkConfig.ConnectionApproval = true;
                network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Protocol);
            }
            network.ConnectionApprovalCallback += Approve;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += TransportFailed;
            network.OnClientStarted += ClientStarted;
        }

        public int GetSlot(ulong clientId)
        {
            return slots.TryGetValue(clientId, out int slot) ? slot : 0;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            NetworkRace race = NetworkRace.Instance;
            bool compatible = request.Payload != null && Encoding.UTF8.GetString(request.Payload) == Protocol;
            int slot = 0;
            while (slot < race.spawnPoints.Length && slots.ContainsValue(slot)) slot++;
            response.Approved = compatible && race.Stage.Value == 0 && slot < 4 && slot < race.spawnPoints.Length;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            if (!response.Approved)
            {
                response.Reason = !compatible ? "Usen la misma version del juego."
                    : race.Stage.Value != 0 ? "La carrera ya comenzo. Espera a la siguiente ronda."
                    : "La sala esta llena (4 jugadores).";
                return;
            }
            slots[request.ClientNetworkId] = slot;
            response.Position = race.spawnPoints[slot].position;
            response.Rotation = Quaternion.identity;
        }

        public void CreateMatch()
        {
            if (connecting || network.IsListening || leaving) return;
            if (useInternet)
            {
                ConnectInternet(true);
                return;
            }
            addresses = LocalAddresses();
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            message = "";
            if (!network.StartHost()) message = "No se pudo crear la partida. Revisa si el puerto esta ocupado.";
        }

        public void JoinMatch()
        {
            if (connecting || network.IsListening || leaving) return;
            if (useInternet)
            {
                joinCode = joinCode.Trim().ToUpperInvariant();
                if (joinCode.Length == 0)
                {
                    message = "Escribí el código que te compartió el host.";
                    return;
                }
                ConnectInternet(false);
                return;
            }
            if (!IPAddress.TryParse(hostAddress.Trim(), out IPAddress address)
                || address.AddressFamily != AddressFamily.InterNetwork || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.Broadcast))
            {
                message = "Escribi una direccion IPv4 del host, por ejemplo 192.168.1.20.";
                return;
            }
            transport.SetConnectionData(address.ToString(), port);
            transport.ConnectTimeoutMS = 1000;
            transport.MaxConnectAttempts = 12;
            message = $"Conectando a {address}:{port}...";
            connecting = network.StartClient();
            if (!connecting) message = "No se pudo iniciar la conexion.";
        }

        void ConnectInternet(bool hosting)
        {
            connecting = true;
            message = hosting ? "Creando partida online..." : "Conectando por código...";
            transport.ConnectTimeoutMS = 1000;
            transport.MaxConnectAttempts = 12;
            openingConnection = OpenInternet(hosting);
        }

        async Task OpenInternet(bool hosting)
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (this == null || leaving) return;
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (this == null || leaving) return;
                if (hosting)
                {
                    Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
                    if (this == null || leaving) return;
                    string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    if (this == null || leaving) return;
                    transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnection));
                    RoomCode = code;
                    if (!network.StartHost()) throw new InvalidOperationException("No se pudo iniciar el host.");
                }
                else
                {
                    JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                    if (this == null || leaving) return;
                    transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnection));
                    RoomCode = joinCode;
                    if (!network.StartClient()) throw new InvalidOperationException("No se pudo iniciar el cliente.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Conexión online: " + exception.Message);
                if (this != null && !leaving)
                {
                    message = InternetError(exception);
                    if (network.IsListening) StartCoroutine(Leave(message));
                }
            }
            finally
            {
                if (this != null && !network.IsListening) connecting = false;
            }
        }

        static string InternetError(Exception exception)
        {
            if (exception is RelayServiceException error)
            {
                if (error.Reason == RelayExceptionReason.JoinCodeNotFound
                    || error.Reason == RelayExceptionReason.AllocationNotFound
                    || error.Reason == RelayExceptionReason.EntityNotFound)
                    return "Código incorrecto o partida cerrada. Pedile al host un código nuevo.";
                if (error.Reason == RelayExceptionReason.RateLimited)
                    return "Demasiados intentos. Esperá unos segundos y volvé a probar.";
                if (error.Reason == RelayExceptionReason.InvalidArgument || error.Reason == RelayExceptionReason.InvalidRequest)
                    return "Revisá el código e intentá nuevamente.";
            }
            return "No se pudo conectar online. Revisá Internet, el código y que la sala siga abierta y tenga lugar.";
        }

        void ClientStarted()
        {
            if (!network.IsServer)
                network.NetworkTimeSystem.ServerBufferSec = 2.0 / network.NetworkConfig.TickRate;
        }

        void Connected(ulong clientId)
        {
            if (clientId != network.LocalClientId) return;
            connecting = false;
            localId = clientId;
            message = "";
            OnConnected?.Invoke();
        }

        public void CancelConnect()
        {
            if (connecting) StartCoroutine(Leave(""));
        }

        public void LeaveMatch()
        {
            if (!leaving) StartCoroutine(Leave(""));
        }

        public void RefreshAddresses()
        {
            addresses = LocalAddresses();
        }

        void Disconnected(ulong clientId)
        {
            slots.Remove(clientId);
            if (leaving || clientId != localId && !connecting) return;
            string reason = network.DisconnectReason;
            StartCoroutine(Leave(string.IsNullOrWhiteSpace(reason)
                ? connecting
                    ? useInternet ? "No se pudo entrar. Revisá código, conexión y misma versión del juego."
                        : $"No se pudo conectar a {transport.ConnectionData.Address}:{port}. Revisa misma red, IP actual del host, firewall y misma version del juego."
                    : "Se perdió la conexión con el host. Revisá que siga abierto y conectado."
                : reason));
        }

        void TransportFailed()
        {
            if (!leaving) StartCoroutine(Leave(useInternet ? "Falló la conexión online. Revisá Internet e intentá nuevamente."
                : $"No se pudo usar la conexion de red o el puerto UDP {port}."));
        }

        IEnumerator Leave(string reason)
        {
            if (leaving) yield break;
            leaving = true;
            previousMessage = reason;
            string scene = SceneManager.GetActiveScene().name;
            while (openingConnection != null && !openingConnection.IsCompleted) yield return null;
            RoomCode = "";
            network.Shutdown();
            while (network.ShutdownInProgress) yield return null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (network == null) return;
            network.ConnectionApprovalCallback -= Approve;
            network.OnClientConnectedCallback -= Connected;
            network.OnClientDisconnectCallback -= Disconnected;
            network.OnTransportFailure -= TransportFailed;
            network.OnClientStarted -= ClientStarted;
        }

        static string LocalAddresses()
        {
            List<string> result = new List<string>();
            List<string> fallback = new List<string>();
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    bool hasGateway = false;
                    foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                        if (gateway.Address.AddressFamily == AddressFamily.InterNetwork
                            && !gateway.Address.Equals(IPAddress.Any)) hasGateway = true;
                    foreach (UnicastIPAddressInformation entry in properties.UnicastAddresses)
                    {
                        IPAddress address = entry.Address;
                        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)
                            || address.Equals(IPAddress.Any) || address.Equals(IPAddress.Broadcast)) continue;
                        byte[] bytes = address.GetAddressBytes();
                        if (bytes[0] == 169 && bytes[1] == 254) continue;
                        string label = address + " (" + adapter.Name + ")";
                        (hasGateway ? result : fallback).Add(label);
                    }
                }
            }
            catch (System.Exception)
            {
                return "consulta IPv4 con ipconfig";
            }
            if (result.Count == 0) result = fallback;
            return result.Count > 0 ? string.Join(" / ", result) : "consulta IPv4 con ipconfig";
        }
    }
}
