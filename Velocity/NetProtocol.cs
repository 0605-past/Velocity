using System;
using System.Globalization;
using System.Text;

namespace Pseudo3DRacer;

/// <summary>UDP 封包：S=狀態、H=加入、R=主機開賽同步、L=離開</summary>
public static class NetProtocol
{
    public const int DefaultPort = 9500;

    public static byte[] BuildState(string id, Vehicle v)
    {
        string pkt = string.Join('|',
            "S", id,
            v.Distance.ToString("F2", CultureInfo.InvariantCulture),
            v.PlayerCurvature.ToString("F4", CultureInfo.InvariantCulture),
            v.CurrentSteerDir.ToString(CultureInfo.InvariantCulture),
            v.LapCount.ToString(CultureInfo.InvariantCulture),
            v.IsBoosting ? "1" : "0",
            v.CurrentRank,
            v.GetLaneOffset().ToString("F4", CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(pkt);
    }

    public static byte[] BuildHello(string id, int mapIndex, int weather, int laps, int carType)
    {
        string pkt = string.Join('|', "H", id, mapIndex, weather, laps, carType);
        return Encoding.UTF8.GetBytes(pkt);
    }

    public static byte[] BuildRaceSync(int mapIndex, int weather, int laps, float countdown)
    {
        string pkt = string.Join('|', "R", mapIndex, weather, laps,
            countdown.ToString("F2", CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(pkt);
    }

    public static byte[] BuildLeave(string id) => Encoding.UTF8.GetBytes($"L|{id}");

    public static bool TryParse(string raw, out NetMessage msg)
    {
        msg = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Split('|');
        if (t.Length < 2) return false;

        msg.Type = t[0];
        try
        {
            switch (t[0])
            {
                case "S" when t.Length >= 9:
                    msg.PlayerId = t[1];
                    msg.Distance = ParseF(t[2]);
                    msg.Curvature = ParseF(t[3]);
                    msg.SteerDir = int.Parse(t[4], CultureInfo.InvariantCulture);
                    msg.LapCount = int.Parse(t[5], CultureInfo.InvariantCulture);
                    msg.IsBoosting = t[6] == "1";
                    msg.Rank = t[7];
                    msg.LaneOffset = ParseF(t[8]);
                    return true;
                case "H" when t.Length >= 6:
                    msg.PlayerId = t[1];
                    msg.MapIndex = int.Parse(t[2], CultureInfo.InvariantCulture);
                    msg.Weather = int.Parse(t[3], CultureInfo.InvariantCulture);
                    msg.Laps = int.Parse(t[4], CultureInfo.InvariantCulture);
                    msg.CarType = int.Parse(t[5], CultureInfo.InvariantCulture);
                    return true;
                case "R" when t.Length >= 5:
                    msg.MapIndex = int.Parse(t[1], CultureInfo.InvariantCulture);
                    msg.Weather = int.Parse(t[2], CultureInfo.InvariantCulture);
                    msg.Laps = int.Parse(t[3], CultureInfo.InvariantCulture);
                    msg.Countdown = ParseF(t[4]);
                    return true;
                case "L" when t.Length >= 2:
                    msg.PlayerId = t[1];
                    return true;
            }
        }
        catch { return false; }
        return false;
    }

    private static float ParseF(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public struct NetMessage
    {
        public string Type;
        public string PlayerId;
        public float Distance, Curvature, LaneOffset, Countdown;
        public int SteerDir, LapCount, MapIndex, Weather, Laps, CarType;
        public bool IsBoosting;
        public string Rank;
    }
}

public static class NetworkUtil
{
    public static string GetLanIPv4()
    {
        try
        {
            foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}
