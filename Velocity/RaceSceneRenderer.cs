using System.Drawing;

namespace Pseudo3DRacer;

public sealed class RaceSceneContext
{
    public required Bitmap FrameBuffer { get; init; }
    public required Track Track { get; init; }
    public required Vehicle Player { get; init; }
    public required string PlayerName { get; init; }
    public required TrackCurvatureController Curvature { get; init; }
    public required WeatherMode Weather { get; init; }
    public required ParticleSystem Particles { get; init; }
    public required GhostRecorder Ghost { get; init; }
    public required SectorTimer SectorTimer { get; init; }
    public required GameSaveData SaveData { get; init; }
    public IEnumerable<PlayerState> RemotePlayers { get; init; } = [];
    public IEnumerable<AiBot> AiBots { get; init; } = [];
    public bool EnableGhost { get; init; }
    public int TargetLaps { get; init; }
    public float CurrentLapTime { get; init; }
    public float BestLapTime { get; init; }
    public float TotalRaceTime { get; init; }
    public string CarTag { get; init; } = "";
    public bool NetEnabled { get; init; }
    public string NetStatus { get; init; } = "";
    public string? BottomBanner { get; init; }
    public string? ToastMessage { get; init; }
    public float ToastTimer { get; init; }
}

public static class RaceSceneRenderer
{
    public static void Draw(RaceSceneContext ctx)
    {
        var fb = ctx.FrameBuffer;
        var track = ctx.Track;
        var vehicle = ctx.Player;
        var curv = ctx.Curvature;
        int w = fb.Width, h = fb.Height;

        track.RenderFrame(fb, vehicle.Distance, curv.Track, curv.Render,
            track.GetCurrentDetails(vehicle.Distance).sectionIndex, ctx.Weather);
        using var g = Graphics.FromImage(fb);

        var renderList = new List<(float rel, Action<Graphics> draw)>();
        if (ctx.EnableGhost && ctx.Ghost.HasGhost && vehicle.LapCount > 0)
        {
            var gf = ctx.Ghost.GetFrameAt(vehicle.Distance, track.TotalDistance);
            if (gf.HasValue && Math.Abs(gf.Value.LateralOffset) < 0.55f)
            {
                renderList.Add((99, gg => vehicle.RenderGhost(gg, w, h, track, curv.Render, curv.Track, gf.Value)));
            }
        }

        foreach (var p in ctx.RemotePlayers)
        {
            float rel = WrapDistance(p.Distance - vehicle.Distance, track.TotalDistance);
            var st = p;
            renderList.Add((rel, gg => vehicle.RenderRemote(gg, w, h, track, curv.Render, curv.Track,
                st.PlayerCurvature, st.SteerDir, st.PlayerId, rel, st.IsBoosting, Color.Red)));
        }
        foreach (var b in ctx.AiBots)
        {
            float rel = WrapDistance(b.Vehicle.Distance - vehicle.Distance, track.TotalDistance);
            var bot = b;
            renderList.Add((rel, gg => bot.Vehicle.RenderRemote(gg, w, h, track, curv.Render, curv.Track,
                bot.Vehicle.PlayerCurvature, bot.Vehicle.CurrentSteerDir, bot.Name, rel,
                bot.Vehicle.IsBoosting, bot.CarColor)));
        }

        renderList.Add((100, gg => vehicle.Render(gg, w, h, track, curv.Render, ctx.PlayerName)));
        foreach (var item in renderList.OrderByDescending(x => x.rel)) item.draw(g);

        ctx.Particles.Render(g);
        if (vehicle.IsBoosting)
            HudRenderer.DrawNitroVignette(g, w, h, vehicle.NitroIntensity, vehicle.NitroPhase);
        WeatherSystem.RenderOverlay(g, w, h, ctx.Weather, ctx.TotalRaceTime);

        var markers = new List<(float, Color)> { (vehicle.Distance, Color.Lime) };
        foreach (var p in ctx.RemotePlayers) markers.Add((p.Distance, Color.Gold));
        foreach (var b in ctx.AiBots) markers.Add((b.Vehicle.Distance, b.CarColor));
        track.RenderMiniMap(g, w, vehicle.Distance, markers);

        var leaderboard = LeaderboardBuilder.Build(ctx.PlayerName, vehicle, ctx.RemotePlayers, ctx.AiBots);
        HudRenderer.DrawLeaderboard(g, leaderboard, ctx.TargetLaps, track.TotalDistance, w);
        HudRenderer.Draw(g, new HudViewModel
        {
            DriverName = ctx.PlayerName,
            CarTag = ctx.CarTag,
            Vehicle = vehicle,
            SectorTimer = ctx.SectorTimer,
            TargetLaps = ctx.TargetLaps,
            CurrentLapTime = ctx.CurrentLapTime,
            BestLapTime = ctx.BestLapTime,
            MapIndex = track.MapIndex,
            BestSector3 = ctx.SaveData.BestSectorByMap[track.MapIndex][2],
            NetEnabled = ctx.NetEnabled,
            NetStatus = ctx.NetStatus,
            AiCount = ctx.AiBots.Count()
        }, w);
        HudRenderer.DrawRankBand(g, vehicle, w);

        if (ctx.SectorTimer.LastToastTimer > 0)
            HudRenderer.DrawSectorBanner(g, w, ctx.SectorTimer.LastToast);
        if (!string.IsNullOrEmpty(ctx.BottomBanner))
            HudRenderer.DrawBottomBanner(g, w, h, ctx.BottomBanner);
        else if (ctx.ToastTimer > 0 && !string.IsNullOrEmpty(ctx.ToastMessage))
            HudRenderer.DrawBottomBanner(g, w, h, ctx.ToastMessage);
    }

    public static float WrapDistance(float d, float totalDistance)
    {
        if (d > totalDistance / 2) d -= totalDistance;
        if (d < -totalDistance / 2) d += totalDistance;
        return d;
    }
}
