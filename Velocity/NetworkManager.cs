using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Pseudo3DRacer
{
    public enum NetworkRole { None, Host, Client }

    public class NetworkPlayer
    {
        public int Id;
        public string Name = "";
        public IPEndPoint? EndPoint;
        public DateTime LastSeen;
        public bool Connected;
        public float Distance;
        public float PlayerCurvature;
        public int SteerDir;
    }

    public class RoomInfo
    {
        public string HostName { get; set; } = "";
        public int PlayerCount { get; set; }
        public string IP { get; set; } = "";
        public int Port { get; set; } = 9001;
        public override string ToString() => $"🏎 {HostName} - {PlayerCount}/4";
    }

    public class NetworkManager : IDisposable
    {
        public const int DefaultPort = 9001;
        public const float TimeoutSeconds = 3.0f;

        public NetworkRole Role { get; private set; } = NetworkRole.None;
        public NetworkPlayer[] Players { get; }
        public int LocalPlayerId { get; private set; } = -1;
        public bool GameStarted { get; set; }
        public string? LastError { get; private set; }

        public event Action<int>? OnPlayerJoined;
        public event Action<int>? OnPlayerLeft;
        public event Action? OnGameStarted;
        public event Action? OnLobbyRefresh;
        public event Action<string>? OnJoinRequest;
        public event Action? OnJoinRejected;

        private string? pendingJoinerName;
        private IPEndPoint? pendingJoinerEP;

        private UdpClient? udpClient;
        private Thread? receiveThread;
        private volatile bool isRunning;
        private readonly object sendLock = new();

        private UdpClient? announceClient;
        private Thread? announceThread;

        private string localPlayerName = "";
        private IPEndPoint? serverEP;

        public NetworkManager()
        {
            Players = new NetworkPlayer[4];
            for (int i = 0; i < 4; i++)
                Players[i] = new NetworkPlayer { Id = i, Connected = false };
        }

        public static string GetLocalIP()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(addr.Address))
                            return addr.Address.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        public bool StartHost(int port, string hostName)
        {
            Stop();
            try
            {
                udpClient = new UdpClient(port);
                Role = NetworkRole.Host;
                LocalPlayerId = 0;
                localPlayerName = hostName;

                Players[0].Id = 0;
                Players[0].Name = hostName;
                Players[0].Connected = true;
                Players[0].LastSeen = DateTime.Now;

                GameStarted = false;
                isRunning = true;
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                receiveThread.Start();

                announceClient = new UdpClient(0);
                announceClient.EnableBroadcast = true;
                announceThread = new Thread(AnnounceLoop) { IsBackground = true };
                announceThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool StartClient(string hostIp, int hostPort, int localPort, string playerName)
        {
            Stop();
            try
            {
                udpClient = new UdpClient(localPort);
                serverEP = new IPEndPoint(IPAddress.Parse(hostIp), hostPort);
                localPlayerName = playerName;

                Role = NetworkRole.Client;
                GameStarted = false;

                string join = $"JOIN_REQUEST|{playerName}";
                byte[] joinData = Encoding.UTF8.GetBytes(join);
                udpClient.Send(joinData, joinData.Length, serverEP);

                isRunning = true;
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                receiveThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public void SendPlayerState(float distance, float curvature, int steerDir)
        {
            if (Role == NetworkRole.None || udpClient == null) return;

            if (Role == NetworkRole.Host)
            {
                Players[0].Distance = distance;
                Players[0].PlayerCurvature = curvature;
                Players[0].SteerDir = steerDir;
                Players[0].LastSeen = DateTime.Now;
                BroadcastGameState();
            }
            else if (Role == NetworkRole.Client && serverEP != null)
            {
                string packet = $"STATE|{LocalPlayerId}|{distance},{curvature},{steerDir}";
                byte[] data = Encoding.UTF8.GetBytes(packet);
                try { udpClient.Send(data, data.Length, serverEP); } catch { }
            }
        }

        private void BroadcastGameState()
        {
            if (udpClient == null || Role != NetworkRole.Host) return;

            var sb = new StringBuilder("GAME");
            for (int i = 0; i < 4; i++)
            {
                sb.Append('|');
                if (Players[i].Connected)
                    sb.Append($"{Players[i].Distance},{Players[i].PlayerCurvature},{Players[i].SteerDir}");
                else
                    sb.Append("0,0,0");
            }

            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            lock (sendLock)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == LocalPlayerId || !Players[i].Connected || Players[i].EndPoint == null)
                        continue;
                    try { udpClient.Send(data, data.Length, Players[i].EndPoint); } catch { }
                }
            }
        }

        private void ReceiveLoop()
        {
            while (isRunning && udpClient != null)
            {
                try
                {
                    var senderEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = udpClient.Receive(ref senderEP);
                    string raw = Encoding.UTF8.GetString(buffer);

                    if (Role == NetworkRole.Host)
                        ProcessHostPacket(raw, senderEP);
                    else if (Role == NetworkRole.Client)
                        ProcessClientPacket(raw);
                }
                catch
                {
                    if (isRunning) Thread.Sleep(10);
                }
            }
        }

        private void AnnounceLoop()
        {
            var broadcastEP = new IPEndPoint(IPAddress.Broadcast, DefaultPort);
            while (isRunning && announceClient != null)
            {
                string msg = $"ANNOUNCE|{localPlayerName}|{GetConnectedCount()}";
                byte[] data = Encoding.UTF8.GetBytes(msg);
                try { announceClient.Send(data, data.Length, broadcastEP); } catch { }
                for (int i = 0; i < 30 && isRunning; i++) Thread.Sleep(100);
            }
        }

        private void ProcessHostPacket(string raw, IPEndPoint senderEP)
        {
            string[] parts = raw.Split('|');
            if (parts.Length < 1) return;

            string cmd = parts[0];
            switch (cmd)
            {
                case "JOIN_REQUEST":
                    pendingJoinerName = parts.Length >= 2 ? parts[1] : $"玩家";
                    pendingJoinerEP = senderEP;
                    OnJoinRequest?.Invoke(pendingJoinerName);
                    break;

                case "STATE":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int id)
                        && id >= 1 && id < 4 && Players[id].Connected)
                    {
                        string[] vals = parts[2].Split(',');
                        if (vals.Length == 3 && float.TryParse(vals[0], out float d)
                            && float.TryParse(vals[1], out float c) && int.TryParse(vals[2], out int s))
                        {
                            Players[id].Distance = d;
                            Players[id].PlayerCurvature = c;
                            Players[id].SteerDir = s;
                            Players[id].LastSeen = DateTime.Now;
                            BroadcastGameState();
                        }
                    }
                    break;

                case "QUIT":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int qid)
                        && qid >= 1 && qid < 4)
                    {
                        Players[qid].Connected = false;
                        Players[qid].EndPoint = null;
                        NotifyPlayerList();
                        OnPlayerLeft?.Invoke(qid);
                    }
                    break;

                case "PING":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int pid)
                        && pid >= 1 && pid < 4 && Players[pid].Connected)
                    {
                        Players[pid].LastSeen = DateTime.Now;
                        byte[] pong = Encoding.UTF8.GetBytes("PONG");
                        try { udpClient?.Send(pong, pong.Length, senderEP); } catch { }
                    }
                    break;

                case "DISCOVER":
                    string here = $"HERE|{localPlayerName}|{GetConnectedCount()}|{((IPEndPoint?)udpClient?.Client.LocalEndPoint)?.Port ?? DefaultPort}";
                    byte[] hereData = Encoding.UTF8.GetBytes(here);
                    try { udpClient?.Send(hereData, hereData.Length, senderEP); } catch { }
                    break;
            }
        }

        private void HandleJoin(string[] parts, IPEndPoint senderEP)
        {
            int assignedId = -1;
            for (int i = 1; i < 4; i++)
            {
                if (!Players[i].Connected) { assignedId = i; break; }
            }
            if (assignedId == -1) return;

            string name = parts.Length >= 2 ? parts[1] : $"Player{assignedId}";

            Players[assignedId].Id = assignedId;
            Players[assignedId].Name = name;
            Players[assignedId].EndPoint = senderEP;
            Players[assignedId].Connected = true;
            Players[assignedId].LastSeen = DateTime.Now;

            string welcome = $"WELCOME|{assignedId}|{GetConnectedCount()}";
            byte[] welcomeData = Encoding.UTF8.GetBytes(welcome);
            try { udpClient?.Send(welcomeData, welcomeData.Length, senderEP); } catch { }

            NotifyPlayerList();
            OnPlayerJoined?.Invoke(assignedId);
            OnLobbyRefresh?.Invoke();
        }

        public void ApproveJoin()
        {
            if (pendingJoinerEP == null || string.IsNullOrEmpty(pendingJoinerName)) return;

            int assignedId = -1;
            for (int i = 1; i < 4; i++)
            {
                if (!Players[i].Connected) { assignedId = i; break; }
            }
            if (assignedId == -1) return;

            Players[assignedId].Id = assignedId;
            Players[assignedId].Name = pendingJoinerName;
            Players[assignedId].EndPoint = pendingJoinerEP;
            Players[assignedId].Connected = true;
            Players[assignedId].LastSeen = DateTime.Now;

            string accept = $"JOIN_ACCEPTED|{assignedId}|{GetConnectedCount()}";
            byte[] data = Encoding.UTF8.GetBytes(accept);
            try { udpClient?.Send(data, data.Length, pendingJoinerEP); } catch { }

            pendingJoinerEP = null;
            pendingJoinerName = null;

            NotifyPlayerList();
            OnPlayerJoined?.Invoke(assignedId);
            OnLobbyRefresh?.Invoke();
        }

        public void RejectJoin()
        {
            if (pendingJoinerEP == null) return;
            byte[] data = Encoding.UTF8.GetBytes("JOIN_REJECTED");
            try { udpClient?.Send(data, data.Length, pendingJoinerEP); } catch { }
            pendingJoinerEP = null;
            pendingJoinerName = null;
        }

        private void NotifyPlayerList()
        {
            if (udpClient == null) return;

            var sb = new StringBuilder("PLAYER_LIST");
            for (int i = 0; i < 4; i++)
                if (Players[i].Connected)
                    sb.Append($"|{i}:{Players[i].Name}");

            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            lock (sendLock)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == LocalPlayerId || !Players[i].Connected || Players[i].EndPoint == null)
                        continue;
                    try { udpClient.Send(data, data.Length, Players[i].EndPoint); } catch { }
                }
            }
            OnLobbyRefresh?.Invoke();
        }

        public void SendStartGame()
        {
            if (Role != NetworkRole.Host || udpClient == null) return;
            GameStarted = true;

            byte[] data = Encoding.UTF8.GetBytes("START_GAME");
            lock (sendLock)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == LocalPlayerId || !Players[i].Connected || Players[i].EndPoint == null)
                        continue;
                    try { udpClient.Send(data, data.Length, Players[i].EndPoint); } catch { }
                }
            }
            OnGameStarted?.Invoke();
        }

        private void ProcessClientPacket(string raw)
        {
            string[] parts = raw.Split('|');
            if (parts.Length < 1) return;

            switch (parts[0])
            {
                case "WELCOME":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int wid))
                    {
                        LocalPlayerId = wid;
                        Players[wid].Id = wid;
                        Players[wid].Name = localPlayerName;
                        Players[wid].Connected = true;
                        Players[wid].LastSeen = DateTime.Now;
                    }
                    OnLobbyRefresh?.Invoke();
                    break;

                case "PLAYER_LIST":
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string[] idName = parts[i].Split(':');
                        if (idName.Length == 2 && int.TryParse(idName[0], out int pid))
                        {
                            bool wasConnected = Players[pid].Connected;
                            Players[pid].Id = pid;
                            Players[pid].Name = idName[1];
                            Players[pid].Connected = true;
                            Players[pid].LastSeen = DateTime.Now;
                            if (!wasConnected && pid != LocalPlayerId)
                                OnPlayerJoined?.Invoke(pid);
                        }
                    }
                    OnLobbyRefresh?.Invoke();
                    break;

                case "GAME":
                    for (int i = 0; i < 4 && i + 1 < parts.Length; i++)
                    {
                        string[] vals = parts[i + 1].Split(',');
                        if (vals.Length == 3 && float.TryParse(vals[0], out float d)
                            && float.TryParse(vals[1], out float c) && int.TryParse(vals[2], out int s))
                        {
                            Players[i].Distance = d;
                            Players[i].PlayerCurvature = c;
                            Players[i].SteerDir = s;
                            if (i != LocalPlayerId) Players[i].Connected = true;
                            Players[i].LastSeen = DateTime.Now;
                        }
                    }
                    break;

                case "PLAYER_LEFT":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int plid))
                    {
                        Players[plid].Connected = false;
                        OnPlayerLeft?.Invoke(plid);
                    }
                    OnLobbyRefresh?.Invoke();
                    break;

                case "JOIN_ACCEPTED":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int jaid))
                    {
                        LocalPlayerId = jaid;
                        Players[jaid].Id = jaid;
                        Players[jaid].Name = localPlayerName;
                        Players[jaid].Connected = true;
                        Players[jaid].LastSeen = DateTime.Now;
                    }
                    OnLobbyRefresh?.Invoke();
                    break;

                case "JOIN_REJECTED":
                    OnJoinRejected?.Invoke();
                    OnLobbyRefresh?.Invoke();
                    break;

                case "START_GAME":
                    GameStarted = true;
                    OnGameStarted?.Invoke();
                    break;
            }
        }

        public int GetConnectedCount()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
                if (Players[i].Connected) count++;
            return count;
        }

        public static List<RoomInfo> DiscoverRooms(int timeoutMs = 2000)
        {
            var results = new List<RoomInfo>();
            try
            {
                using var client = new UdpClient(0);
                client.EnableBroadcast = true;
                client.Client.ReceiveTimeout = timeoutMs;
                var broadcastEP = new IPEndPoint(IPAddress.Broadcast, DefaultPort);
                byte[] discover = Encoding.UTF8.GetBytes("DISCOVER");
                client.Send(discover, discover.Length, broadcastEP);

                var seen = new HashSet<string>();
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        var senderEP = new IPEndPoint(IPAddress.Any, 0);
                        byte[] buffer = client.Receive(ref senderEP);
                        string raw = Encoding.UTF8.GetString(buffer);
                        string[] parts = raw.Split('|');
                        if (parts.Length >= 3 && parts[0] == "HERE")
                        {
                            string ip = senderEP.Address.ToString();
                            if (seen.Add(ip))
                            {
                                int port = parts.Length >= 4 && int.TryParse(parts[3], out int p) ? p : DefaultPort;
                                results.Add(new RoomInfo
                                {
                                    HostName = parts[1],
                                    PlayerCount = int.TryParse(parts[2], out int c) ? c : 0,
                                    IP = ip,
                                    Port = port
                                });
                            }
                        }
                    }
                    catch { break; }
                }
            }
            catch { }
            return results;
        }

        public void CheckTimeouts()
        {
            if (Role != NetworkRole.Host) return;

            var now = DateTime.Now;
            for (int i = 1; i < 4; i++)
            {
                if (Players[i].Connected && (now - Players[i].LastSeen).TotalSeconds > TimeoutSeconds)
                {
                    Players[i].Connected = false;
                    Players[i].EndPoint = null;
                    NotifyPlayerList();
                    OnPlayerLeft?.Invoke(i);
                }
            }
        }

        public void Disconnect()
        {
            if (Role == NetworkRole.Client && udpClient != null && serverEP != null)
            {
                string quit = $"QUIT|{LocalPlayerId}";
                byte[] data = Encoding.UTF8.GetBytes(quit);
                try { udpClient.Send(data, data.Length, serverEP); } catch { }
            }
        }

        public void Stop()
        {
            isRunning = false;
            Disconnect();

            if (announceThread != null && announceThread.IsAlive)
                announceThread.Join(500);
            announceClient?.Close();
            announceClient = null;

            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(500);

            udpClient?.Close();
            udpClient = null;

            Role = NetworkRole.None;
            LocalPlayerId = -1;
            GameStarted = false;

            for (int i = 0; i < 4; i++)
            {
                Players[i].Connected = false;
                Players[i].EndPoint = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
