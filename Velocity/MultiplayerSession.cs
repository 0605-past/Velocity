using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Pseudo3DRacer;

public sealed class MultiplayerSession : IDisposable
{
    private UdpClient? _udp;
    private IPEndPoint? _serverEp;
    private Thread? _receiveThread;
    private UdpRelayServer? _relay;
    private bool _running;
    private float _sendTimer;

    public bool Enabled { get; private set; }
    public bool IsHost { get; private set; }
    public bool RaceSynced { get; set; }
    public string Status { get; private set; } = "離線";
    public string LocalPlayerId { get; set; } = "Player";
    public ConcurrentDictionary<string, PlayerState> RemotePlayers { get; } = new();

    public event Action<int, int, int, float>? RaceSyncReceived;
    public event Action? RemotePlayerJoined;

    public void StartHostRelay() => (_relay ??= new UdpRelayServer()).Start(NetProtocol.DefaultPort);

    public bool Connect(bool asHost, string hostIp, int mapIndex, int weather, int laps, int carType)
    {
        Enabled = true;
        IsHost = asHost;
        RaceSynced = !Enabled || IsHost;
        Status = "連線中...";
        RemotePlayers.Clear();

        try
        {
            _udp = new UdpClient(0);
            string host = string.IsNullOrWhiteSpace(hostIp) ? "127.0.0.1" : hostIp.Trim();
            _serverEp = new IPEndPoint(IPAddress.Parse(host), NetProtocol.DefaultPort);
            _running = true;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "NetReceive" };
            _receiveThread.Start();
            SendHello(mapIndex, weather, laps, carType);
            if (IsHost) BroadcastRaceSync(mapIndex, weather, laps, 3f);
            _sendTimer = 0;
            return true;
        }
        catch (Exception ex)
        {
            Status = "連線失敗";
            Enabled = false;
            throw new InvalidOperationException("連線失敗: " + ex.Message, ex);
        }
    }

    public void Tick(float dt, Vehicle vehicle, GameState state, float countdown,
        int mapIndex, int weather, int laps)
    {
        if (!Enabled) return;
        _sendTimer += dt;
        if (_sendTimer >= 0.05f)
        {
            _sendTimer = 0;
            SendState(vehicle);
            if (IsHost && state == GameState.Countdown)
                BroadcastRaceSync(mapIndex, weather, laps, countdown);
        }
        Status = $"連線 {RemotePlayers.Count + 1} 人";
    }

    public void PruneStalePlayers(double timeoutSeconds = 4)
    {
        var stale = RemotePlayers
            .Where(p => (DateTime.Now - p.Value.LastSeen).TotalSeconds > timeoutSeconds)
            .Select(p => p.Key).ToList();
        foreach (var k in stale) RemotePlayers.TryRemove(k, out _);
    }

    public void BroadcastRaceSync(int mapIndex, int weather, int laps, float countdown)
    {
        if (!Enabled || !IsHost || _udp == null || _serverEp == null) return;
        try
        {
            var data = NetProtocol.BuildRaceSync(mapIndex, weather, laps, countdown);
            _udp.Send(data, data.Length, _serverEp);
        }
        catch { }
    }

    public void Dispose()
    {
        _running = false;
        try { if (Enabled) SendLeave(); } catch { }
        try { _udp?.Close(); } catch { }
        _udp = null;
        _serverEp = null;
        _relay?.Dispose();
        _relay = null;
        Enabled = false;
    }

    private void SendHello(int mapIndex, int weather, int laps, int carType)
    {
        if (_udp == null || _serverEp == null) return;
        var data = NetProtocol.BuildHello(LocalPlayerId, mapIndex, weather, laps, carType);
        _udp.Send(data, data.Length, _serverEp);
    }

    private void SendState(Vehicle vehicle)
    {
        if (!Enabled || _udp == null || _serverEp == null) return;
        try
        {
            var data = NetProtocol.BuildState(LocalPlayerId, vehicle);
            _udp.Send(data, data.Length, _serverEp);
        }
        catch { Status = "傳送失敗"; }
    }

    private void SendLeave()
    {
        if (_udp == null || _serverEp == null) return;
        var data = NetProtocol.BuildLeave(LocalPlayerId);
        _udp.Send(data, data.Length, _serverEp);
    }

    private void ReceiveLoop()
    {
        while (_running && _udp != null)
        {
            try
            {
                IPEndPoint ep = new(IPAddress.Any, 0);
                byte[] buf = _udp.Receive(ref ep);
                string raw = Encoding.UTF8.GetString(buf);
                if (!NetProtocol.TryParse(raw, out var msg))
                {
                    string[] t = raw.Split(',');
                    if (t.Length >= 5 && t[0] != LocalPlayerId)
                        RemotePlayers.AddOrUpdate(t[0], _ => ParseLegacy(t), (_, _) => ParseLegacy(t));
                    continue;
                }
                switch (msg.Type)
                {
                    case "S" when msg.PlayerId != LocalPlayerId:
                        RemotePlayers.AddOrUpdate(msg.PlayerId, _ => ToState(msg), (_, _) => ToState(msg));
                        break;
                    case "H" when msg.PlayerId != LocalPlayerId:
                        RemotePlayers.AddOrUpdate(msg.PlayerId, _ => new PlayerState
                        {
                            PlayerId = msg.PlayerId,
                            LastSeen = DateTime.Now
                        }, (_, p) => { p.LastSeen = DateTime.Now; return p; });
                        if (IsHost) RemotePlayerJoined?.Invoke();
                        break;
                    case "R" when !IsHost:
                        RaceSyncReceived?.Invoke(msg.MapIndex, msg.Weather, msg.Laps, msg.Countdown);
                        break;
                    case "L":
                        RemotePlayers.TryRemove(msg.PlayerId, out _);
                        break;
                }
            }
            catch (SocketException) { if (!_running) break; }
            catch (ObjectDisposedException) { break; }
            catch { if (!_running) break; }
        }
    }

    private static PlayerState ToState(NetProtocol.NetMessage m) => new()
    {
        PlayerId = m.PlayerId,
        Distance = m.Distance,
        PlayerCurvature = m.Curvature,
        SteerDir = m.SteerDir,
        LapCount = m.LapCount,
        IsBoosting = m.IsBoosting,
        Rank = m.Rank,
        LateralOffset = m.LaneOffset,
        LastSeen = DateTime.Now
    };

    private static PlayerState ParseLegacy(string[] t) => new()
    {
        PlayerId = t[0],
        Distance = float.Parse(t[1], System.Globalization.CultureInfo.InvariantCulture),
        PlayerCurvature = float.Parse(t[2], System.Globalization.CultureInfo.InvariantCulture),
        SteerDir = int.Parse(t[3], System.Globalization.CultureInfo.InvariantCulture),
        LapCount = int.Parse(t[4], System.Globalization.CultureInfo.InvariantCulture),
        IsBoosting = t.Length >= 6 && t[5] == "1",
        Rank = t.Length >= 7 ? t[6] : "C",
        LateralOffset = t.Length >= 8 ? float.Parse(t[7], System.Globalization.CultureInfo.InvariantCulture) : 0,
        LastSeen = DateTime.Now
    };
}
