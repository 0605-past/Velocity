using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Pseudo3DRacer;

public class UdpRelayServer : IDisposable
{
    private UdpClient? _client;
    private Thread? _thread;
    private volatile bool _running;
    private readonly ConcurrentDictionary<string, IPEndPoint> _endpoints = new();

    public int Port { get; private set; }
    public bool IsRunning => _running;
    public int ConnectedPlayers => _endpoints.Count;

    public void Start(int port = NetProtocol.DefaultPort)
    {
        Stop();
        Port = port;
        _client = new UdpClient(port);
        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "UdpRelay" };
        _thread.Start();
    }

    private void ReceiveLoop()
    {
        while (_running && _client != null)
        {
            try
            {
                IPEndPoint ep = new(IPAddress.Any, 0);
                byte[] data = _client.Receive(ref ep);
                string packet = Encoding.UTF8.GetString(data);
                if (!NetProtocol.TryParse(packet, out var msg) && packet.IndexOf(',') >= 0)
                {
                    // 舊版相容
                    string[] tokens = packet.Split(',');
                    if (tokens.Length >= 1) msg.PlayerId = tokens[0];
                    else continue;
                }

                string playerId = msg.PlayerId;
                if (string.IsNullOrEmpty(playerId))
                {
                    string[] tokens = packet.Split('|', ',');
                    if (tokens.Length >= 2) playerId = tokens[1];
                    else continue;
                }

                if (msg.Type == "L")
                    _endpoints.TryRemove(playerId, out _);
                else
                    _endpoints[playerId] = ep;

                foreach (var kv in _endpoints)
                {
                    if (kv.Key == playerId) continue;
                    try { _client.Send(data, data.Length, kv.Value); } catch { }
                }
            }
            catch (SocketException) { if (!_running) break; }
            catch (ObjectDisposedException) { break; }
            catch { if (!_running) break; }
        }
    }

    public void Stop()
    {
        _running = false;
        _endpoints.Clear();
        try { _client?.Close(); } catch { }
        _client = null;
    }

    public void Dispose() => Stop();
}
