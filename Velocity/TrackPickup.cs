using System;
using System.Collections.Generic;

namespace Pseudo3DRacer;

public enum PickupType { Nitro, SpeedBoost, Shield, SlowTrap, Magnet }

public class TrackPickup
{
    public float Distance { get; set; }
    public PickupType Type { get; set; }
    public bool Active { get; set; } = true;
}

public static class PickupFactory
{
    public static List<TrackPickup> Generate(float totalDistance, int mapIndex)
    {
        var list = new List<TrackPickup>();
        int count = 8 + mapIndex * 2;
        var rng = new Random(mapIndex * 1000 + 42);
        var types = Enum.GetValues<PickupType>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new TrackPickup
            {
                Distance = totalDistance * (i + 1) / (count + 1) + rng.Next(-30, 30),
                Type = types[rng.Next(types.Length)]
            });
        }
        return list;
    }

    public static string GetToast(PickupType type) => type switch
    {
        PickupType.Nitro => "氮氣 +40!",
        PickupType.SpeedBoost => "瞬間加速!",
        PickupType.Shield => "護盾 6 秒!",
        PickupType.SlowTrap => "陷阱! 降速 2.5 秒",
        PickupType.Magnet => "磁鐵 5 秒!",
        _ => ""
    };
}
