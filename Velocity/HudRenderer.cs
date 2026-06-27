using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pseudo3DRacer;

public sealed class HudViewModel
{
    public required string DriverName { get; init; }
    public required string CarTag { get; init; }
    public required Vehicle Vehicle { get; init; }
    public required SectorTimer SectorTimer { get; init; }
    public int TargetLaps { get; init; }
    public float CurrentLapTime { get; init; }
    public float BestLapTime { get; init; }
    public int MapIndex { get; init; }
    public float BestSector3 { get; init; }
    public bool NetEnabled { get; init; }
    public string NetStatus { get; init; } = "";
    public int AiCount { get; init; }
}

public static class HudRenderer
{
    public static void Draw(Graphics g, HudViewModel vm, int w)
    {
        var vehicle = vm.Vehicle;
        using var f = new Font("Arial", 11, FontStyle.Bold);
        using var fSmall = new Font("Arial", 10, FontStyle.Bold);
        string combo = vehicle.Combo > 1 ? $" COMBO x{vehicle.Combo}" : "";
        int sec = vm.SectorTimer.CurrentSectorIndex + 1;
        g.DrawString($"DRIVER : {vm.DriverName} [{vm.CarTag}]{combo} S{sec}/3", f, Brushes.Cyan, 10, 10);
        g.DrawString($"SPEED : {(vehicle.Speed * 320):0} km/h", f, Brushes.Yellow, 10, 30);
        g.DrawString($"LAP : {vehicle.LapCount} / {vm.TargetLaps}", f, Brushes.Lime, 10, 50);
        g.DrawString($"TIME : {vm.CurrentLapTime:F2}s", f, Brushes.White, 10, 70);
        if (vm.BestLapTime < float.MaxValue)
            g.DrawString($"BEST : {vm.BestLapTime:F2}s", f, Brushes.Gold, 10, 90);

        g.DrawString("NITRO :", f, Brushes.OrangeRed, 10, 115);
        int barX = 75, barY = 117, barW = 100, barH = 12;
        g.DrawRectangle(Pens.Gray, barX, barY, barW, barH);
        int nw = (int)Math.Clamp(vehicle.Nitro, 0, 100);
        float pulse = 0.5f + 0.5f * MathF.Sin(vehicle.NitroPhase);
        for (int seg = 0; seg < 10; seg++)
        {
            int segFill = Math.Clamp(nw - seg * 10, 0, 10);
            if (segFill <= 0) continue;
            int sx = barX + 1 + seg * 10;
            Color c = vehicle.IsBoosting
                ? Color.FromArgb(255, (int)(100 + 155 * pulse), (int)(200 + 55 * pulse), 255)
                : vehicle.IsDrifting ? Color.Gold : Color.FromArgb(255, 30, 144, 255);
            using var segBrush = new SolidBrush(c);
            g.FillRectangle(segBrush, sx, barY + 1, segFill, barH - 2);
            if (vehicle.IsBoosting)
                g.DrawRectangle(new Pen(Color.FromArgb((int)(120 * pulse), 255, 255, 255)), sx, barY + 1, segFill, barH - 2);
        }
        if (vehicle.IsBoosting)
            g.DrawString("BOOST!", fSmall, Brushes.Cyan, 185, 112);
        else if (vehicle.IsDrifting)
            g.DrawString(vehicle.DriftTimer > 0.8f ? "DRIFT CHARGED!!" : "DRIFTING", f, Brushes.Gold, 185, 112);

        int statusX = 200;
        if (vehicle.ShieldActive)
            g.DrawString("SHIELD", fSmall, Brushes.Cyan, statusX, 30);
        if (vehicle.HasMagnet)
            g.DrawString("MAGNET", fSmall, Brushes.Magenta, statusX, 50);

        if (vm.NetEnabled)
            g.DrawString($"連線: {vm.NetStatus}", fSmall, Brushes.Lime, 10, 140);
        else
            g.DrawString($"AI RACERS : {vm.AiCount}", fSmall, Brushes.Gray, 10, 140);

        if (vm.BestSector3 < float.MaxValue)
            g.DrawString($"Best S3: {vm.BestSector3:F2}s", fSmall, Brushes.LightGray, 10, 158);

        using var sf = new Font("Arial", 11, FontStyle.Bold);
        g.DrawString($"S{sec}/3", sf, Brushes.White, w - 55, 10);
    }

    public static void DrawLeaderboard(Graphics g, IReadOnlyList<PlayerState> sorted, int targetLaps, float totalDistance, int w)
    {
        int left = w - 210, top = 130, rh = 22, bh = sorted.Count * rh + 25;
        using (var bg = new SolidBrush(Color.FromArgb(160, Color.Black)))
        {
            g.FillRectangle(bg, left, top, 195, bh);
            g.DrawRectangle(Pens.Gray, left, top, 195, bh);
        }
        using var tf = new Font("微軟正黑體", 9, FontStyle.Bold);
        using var lf = new Font("Consolas", 9, FontStyle.Bold);
        g.DrawString("名次", tf, Brushes.Yellow, left + 8, top + 4);
        g.DrawString("車手", tf, Brushes.Yellow, left + 48, top + 4);
        g.DrawString("圈數", tf, Brushes.Yellow, left + 118, top + 4);
        g.DrawLine(Pens.Gray, left + 5, top + 20, left + 190, top + 20);
        for (int i = 0; i < sorted.Count; i++)
        {
            var p = sorted[i];
            Brush posBrush = i switch { 0 => Brushes.Orange, 1 => Brushes.LightGray, 2 => Brushes.DimGray, _ => Brushes.Gray };
            Brush nameBrush = p.IsLocal ? Brushes.Cyan : i switch { 0 => Brushes.Cyan, 1 => Brushes.OrangeRed, 2 => Brushes.DeepSkyBlue, _ => Brushes.White };
            string pos = i switch { 0 => "1st", 1 => "2nd", 2 => "3rd", _ => $"{i + 1}th" };
            string laps = LeaderboardBuilder.FormatLapProgress(p.LapCount, targetLaps, p.Distance, totalDistance);
            g.DrawString(pos, lf, posBrush, left + 8, top + 24 + i * rh);
            g.DrawString(p.PlayerId.Length > 7 ? p.PlayerId[..7] : p.PlayerId, tf, nameBrush, left + 48, top + 23 + i * rh);
            g.DrawString(laps, lf, nameBrush, left + 118, top + 24 + i * rh);
        }
    }

    public static void DrawNitroVignette(Graphics g, int w, int h, float intensity, float phase)
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(phase * 2f);
        int alpha = (int)(28 * intensity * pulse);
        using var left = new SolidBrush(Color.FromArgb(alpha, 0, 120, 255));
        using var right = new SolidBrush(Color.FromArgb(alpha, 0, 120, 255));
        g.FillRectangle(left, 0, 0, 40, h);
        g.FillRectangle(right, w - 40, 0, 40, h);
        using var top = new SolidBrush(Color.FromArgb(alpha / 2, 0, 180, 255));
        g.FillRectangle(top, 0, 0, w, 18);
    }

    public static void DrawRankBand(Graphics g, Vehicle vehicle, int w)
    {
        if (vehicle.CurrentRank == "C") return;

        float flash = vehicle.RankFlashTimer > 0 ? vehicle.RankFlashTimer / 1f : 0;
        float pulse = 0.5f + 0.5f * MathF.Sin(vehicle.RankRingPhase);
        int bandY = 168, bandH = 44;

        Color bandColor = vehicle.CurrentRank switch
        {
            "S" => Color.FromArgb((int)(170 + 60 * flash), 0, 55, 95),
            "A" => Color.FromArgb((int)(170 + 50 * flash), 95, 55, 0),
            "B" => Color.FromArgb((int)(160 + 40 * flash), 45, 45, 75),
            _ => Color.FromArgb(170, 70, 0, 110)
        };
        using (var band = new SolidBrush(bandColor))
            g.FillRectangle(band, 0, bandY, w, bandH);

        Color edge = vehicle.CurrentRank switch
        {
            "S" => Color.FromArgb((int)(180 + 75 * pulse), 0, 255, 255),
            "A" => Color.FromArgb((int)(180 + 75 * pulse), 255, 210, 0),
            "B" => Color.FromArgb((int)(160 + 60 * pulse), 180, 210, 255),
            _ => Color.Gold
        };
        using (var edgePen = new Pen(edge, 2f))
        {
            g.DrawLine(edgePen, 0, bandY, w, bandY);
            g.DrawLine(edgePen, 0, bandY + bandH, w, bandY + bandH);
        }

        if (vehicle.CurrentRank == "S")
        {
            using var vignette = new SolidBrush(Color.FromArgb((int)(35 * flash), 180, 255, 255));
            g.FillRectangle(vignette, 0, 0, w, bandY);
            g.FillRectangle(vignette, 0, bandY + bandH, w, 200);
        }

        float scale = vehicle.RankAnimScale;
        using var rf = new Font("Impact", 26 * scale, FontStyle.Bold | FontStyle.Italic);
        string txt = $"RANK {vehicle.CurrentRank}";
        var sz = g.MeasureString(txt, rf);
        float tx = w / 2f - sz.Width / 2;
        float ty = bandY + (bandH - sz.Height) / 2f;

        using (var shadow = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
            g.DrawString(txt, rf, shadow, tx + 2, ty + 2);

        Brush rankBrush = vehicle.CurrentRank switch
        {
            "S" => new SolidBrush(Color.FromArgb(255, (int)(180 + 75 * pulse), 255, 255)),
            "A" => new SolidBrush(Color.FromArgb(255, 255, (int)(180 + 60 * pulse), 40)),
            "B" => new SolidBrush(Color.FromArgb(255, 220, 235, 255)),
            _ => Brushes.Gold
        };
        g.DrawString(txt, rf, rankBrush, tx, ty);
        if (rankBrush is SolidBrush sb) sb.Dispose();

        if (vehicle.Combo > 1)
        {
            using var cf = new Font("Arial", 11, FontStyle.Bold);
            string comboTxt = $"COMBO x{vehicle.Combo}";
            var csz = g.MeasureString(comboTxt, cf);
            using var cbg = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            g.FillRectangle(cbg, w - csz.Width - 18, bandY + 12, csz.Width + 10, csz.Height + 4);
            g.DrawString(comboTxt, cf, Brushes.Gold, w - csz.Width - 14, bandY + 14);
        }

        if (flash > 0.3f)
        {
            using var streak = new Pen(Color.FromArgb((int)(80 * flash), 255, 255, 255), 3f);
            for (int i = 0; i < 5; i++)
            {
                int sx = (int)(w * (0.1f + i * 0.18f));
                g.DrawLine(streak, sx, bandY + 4, sx + 30, bandY + bandH - 4);
            }
        }
    }

    public static void DrawSectorBanner(Graphics g, int w, string msg)
    {
        using var f = new Font("Arial", 12, FontStyle.Bold);
        var sz = g.MeasureString(msg, f);
        g.DrawString(msg, f, Brushes.Cyan, w / 2f - sz.Width / 2, 42);
    }

    public static void DrawBottomBanner(Graphics g, int w, int h, string msg)
    {
        using var f = new Font("Impact", 18, FontStyle.Bold);
        var sz = g.MeasureString(msg, f);
        g.DrawString(msg, f, Brushes.Black, w / 2f - sz.Width / 2 + 2, h - 58);
        g.DrawString(msg, f, Brushes.White, w / 2f - sz.Width / 2, h - 60);
    }
}
