using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pseudo3DRacer;

public enum LiveryType { Solid, RacingStripe, Flames, Carbon, Checker, ElectricBlue, Neon }

public static class LiveryCatalog
{
    public static readonly (LiveryType Type, string Key, string Display)[] All =
    {
        (LiveryType.Solid, "Solid", "1. 原廠漆"),
        (LiveryType.RacingStripe, "RacingStripe", "2. 賽道條紋"),
        (LiveryType.Flames, "Flames", "3. 烈焰"),
        (LiveryType.Carbon, "Carbon", "4. 碳纖維"),
        (LiveryType.Checker, "Checker", "5. 棋盤格"),
        (LiveryType.ElectricBlue, "ElectricBlue", "6. 電光藍"),
        (LiveryType.Neon, "Neon", "7. 霓虹")
    };

    public static string GetDisplay(LiveryType t)
    {
        foreach (var e in All) if (e.Type == t) return e.Display;
        return "1. 原廠漆";
    }

    public static LiveryType Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return LiveryType.Solid;
        foreach (var e in All)
            if (text == e.Display || text == e.Key || text.Contains(e.Display.Split(' ')[^1]))
                return e.Type;
        return Enum.TryParse<LiveryType>(text, out var t) ? t : LiveryType.Solid;
    }

    public static IEnumerable<string> UnlockedDisplays(IEnumerable<string> keys)
    {
        var set = new HashSet<string>(keys);
        foreach (var e in All)
            if (set.Contains(e.Key) || set.Contains(e.Display)) yield return e.Display;
    }
}

public static class CarLivery
{
    public static void Render(Graphics g, int carX, int carY, int carW, int carH, LiveryType livery, Color baseColor, float scale)
    {
        using var baseBrush = new SolidBrush(baseColor);
        g.FillRectangle(baseBrush, carX, carY, carW, carH - (int)(15 * scale));

        switch (livery)
        {
            case LiveryType.RacingStripe:
            case LiveryType.ElectricBlue:
                using (var stripe = new SolidBrush(livery == LiveryType.ElectricBlue ? Color.DeepSkyBlue : Color.White))
                {
                    int sw = Math.Max(2, (int)(10 * scale));
                    g.FillRectangle(stripe, carX + carW / 2 - sw / 2, carY, sw, carH - (int)(15 * scale));
                }
                break;
            case LiveryType.Flames:
                using (var flame1 = new SolidBrush(Color.OrangeRed))
                using (var flame2 = new SolidBrush(Color.Yellow))
                {
                    g.FillEllipse(flame1, carX + (int)(10 * scale), carY + (int)(5 * scale), (int)(20 * scale), (int)(15 * scale));
                    g.FillEllipse(flame2, carX + carW - (int)(30 * scale), carY + (int)(8 * scale), (int)(18 * scale), (int)(12 * scale));
                }
                break;
            case LiveryType.Carbon:
                using (var hatch = new HatchBrush(HatchStyle.DarkVertical, Color.FromArgb(60, 60, 60), baseColor))
                    g.FillRectangle(hatch, carX, carY, carW, carH - (int)(15 * scale));
                break;
            case LiveryType.Checker:
                int cell = Math.Max(3, (int)(6 * scale));
                for (int cx = 0; cx < carW; cx += cell)
                    for (int cy = 0; cy < carH - (int)(15 * scale); cy += cell)
                        if ((cx / cell + cy / cell) % 2 == 0)
                            g.FillRectangle(Brushes.White, carX + cx, carY + cy, cell, cell);
                break;
            case LiveryType.Neon:
                using (var neon = new Pen(Color.Cyan, Math.Max(2f, 3 * scale)))
                    g.DrawRectangle(neon, carX + 2, carY + 2, carW - 4, carH - (int)(19 * scale));
                break;
        }
    }
}
