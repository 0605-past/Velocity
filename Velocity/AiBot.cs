using System;
using System.Drawing;

namespace Pseudo3DRacer;

public static class AiRoster
{
    public static readonly (string Name, Color Body, Color TagBg, Color TagBorder, bool RoofBar, CarType Type)[] Bots =
    {
        ("RacerX", Color.Red, Color.Gray, Color.White, true, CarType.SpeedDemon),
        ("Shadow", Color.Red, Color.Black, Color.White, false, CarType.DriftKing),
        ("Blaze", Color.Red, Color.RoyalBlue, Color.White, true, CarType.NitroBeast),
        ("Nitro", Color.Red, Color.DimGray, Color.White, false, CarType.NitroBeast)
    };
}

public class AiBot
{
    public Vehicle Vehicle { get; }
    public string Name { get; }
    public Color CarColor { get; }
    public Color TagBg { get; }
    public Color TagBorder { get; }
    public bool RoofBar { get; }
    public float AiSkill { get; }
    private float _laneBias;
    private float _steerNoise;

    public AiBot((string Name, Color Body, Color TagBg, Color TagBorder, bool RoofBar, CarType Type) def, float startDistance, float playerStartDistance, Track track)
    {
        Name = def.Name;
        CarColor = def.Body;
        TagBg = def.TagBg;
        TagBorder = def.TagBorder;
        RoofBar = def.RoofBar;
        AiSkill = 0.82f + Random.Shared.NextSingle() * 0.12f;
        Vehicle = new Vehicle(CarProfile.Get(def.Type));
        Vehicle.Distance = startDistance;
        Vehicle.PlayerCurvature = track.GetAccumulatedCurvature(playerStartDistance);
        _laneBias = (Random.Shared.NextSingle() - 0.5f) * 0.05f;
    }

    public void Update(Track track, float playerTrackCurvature, float playerSectionCurvature, int screenW, int screenH,
        float elapsedTime, float totalDistance, WeatherMode weather)
    {
        float grip = WeatherSystem.GripMultiplier(weather);
        var bounds = Track.ComputeLaneBounds(screenW, screenH, playerSectionCurvature);
        float center = playerTrackCurvature;
        float laneSpan = bounds.MaxLane - bounds.MinLane;
        float targetLane = _laneBias * laneSpan * (1f - AiSkill);

        _steerNoise += elapsedTime;
        if (_steerNoise > 0.4f)
        {
            _steerNoise = 0;
            _laneBias += (Random.Shared.NextSingle() - 0.5f) * 0.04f;
            _laneBias = Math.Clamp(_laneBias, -0.35f, 0.35f);
        }

        float targetCurve = center + targetLane;
        float curveDiff = targetCurve - Vehicle.PlayerCurvature;
        Vehicle.PlayerCurvature += curveDiff * Math.Min(1f, elapsedTime * 4.5f);
        bool left = curveDiff < -0.012f;
        bool right = curveDiff > 0.012f;
        bool boost = Vehicle.Nitro > 50f && Random.Shared.NextDouble() < 0.015;
        bool drift = Vehicle.Speed > 0.35f && Math.Abs(curveDiff) > 0.08f && Random.Shared.NextDouble() < 0.03;

        Vehicle.Update(true, drift, left, right, boost, drift, center, playerSectionCurvature, screenW, screenH, elapsedTime, totalDistance, grip);
    }
}