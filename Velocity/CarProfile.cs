namespace Pseudo3DRacer;

public enum CarType { SpeedDemon, DriftKing, NitroBeast }

public class CarProfile
{
    public string Name { get; }
    public string MenuLabel { get; }
    public string HudTag { get; }
    public float MaxSpeed { get; }
    public float Acceleration { get; }
    public float NitroMaxSpeed { get; }
    public float NitroConsumption { get; }
    public float NitroRegen { get; }
    public float DriftBonus { get; }

    public CarProfile(string name, string menuLabel, string hudTag, float maxSpeed, float accel, float nitroMax, float nitroUse, float nitroRegen, float driftBonus)
    {
        Name = name;
        MenuLabel = menuLabel;
        HudTag = hudTag;
        MaxSpeed = maxSpeed;
        Acceleration = accel;
        NitroMaxSpeed = nitroMax;
        NitroConsumption = nitroUse;
        NitroRegen = nitroRegen;
        DriftBonus = driftBonus;
    }

    public static CarProfile Get(CarType type) => type switch
    {
        CarType.SpeedDemon => new CarProfile("極速蜂", "極速蜂 (速度)", "極速鋒", 1.0f, 2.2f, 1.45f, 30f, 12f, 0.8f),
        CarType.DriftKing => new CarProfile("甩尾王", "甩尾王 (甩尾)", "甩尾王", 0.95f, 2.0f, 1.4f, 30f, 12f, 1.4f),
        CarType.NitroBeast => new CarProfile("氮氣狂", "氮氣狂 (氮氣)", "氮氣狂", 0.9f, 1.8f, 1.6f, 25f, 8f, 1.0f),
        _ => Get(CarType.SpeedDemon)
    };
}
