using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pseudo3DRacer;

/// <summary>原版像素風後視角車輛繪製（對照 Velocity.exe 截圖）</summary>
public static class CarSpriteRenderer
{
    public struct DrawOpts
    {
        public Color BodyColor;
        public LiveryType Livery;
        public Color TagBackground;
        public Color TagBorder;
        public bool RoofBar;
        public bool Boosting;
        public bool Shield;
        public bool Magnet;
        public bool Ghost;
    }

    public static void Draw(Graphics g, int carX, int carY, float scale, string name, DrawOpts opt)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        int w = Math.Max(28, (int)(72 * scale));
        int h = Math.Max(18, (int)(38 * scale));
        int x = carX - w / 2;
        int y = carY - h;

        DrawNameTag(g, x, y, w, scale, name, opt.TagBackground, opt.TagBorder);

        if (opt.Ghost)
        {
            using var gb = new SolidBrush(Color.FromArgb(90, 180, 220, 255));
            DrawBody(g, x, y, w, h, scale, gb, opt);
            return;
        }

        using var body = new SolidBrush(opt.BodyColor);
        DrawBody(g, x, y, w, h, scale, body, opt);

        if (opt.Shield || opt.Magnet)
        {
            using var ring = new Pen(Color.FromArgb(180, 0, 220, 255), Math.Max(2f, 2.5f * scale));
            g.DrawEllipse(ring, x - (int)(4 * scale), y - (int)(4 * scale), w + (int)(8 * scale), h + (int)(8 * scale));
        }

        if (opt.Boosting)
            DrawBoostFlames(g, x, y, w, h, scale);
    }

    private static void DrawBody(Graphics g, int x, int y, int w, int h, float scale, Brush body, DrawOpts opt)
    {
        g.FillRectangle(body, x, y + (int)(6 * scale), w, h - (int)(10 * scale));

        if (opt.RoofBar)
            g.FillRectangle(Brushes.DeepSkyBlue, x + (int)(8 * scale), y + (int)(2 * scale), w - (int)(16 * scale), (int)(5 * scale));

        int roofH = Math.Max(3, (int)(8 * scale));
        g.FillRectangle(Brushes.Black, x + (int)(10 * scale), y + (int)(6 * scale), w - (int)(20 * scale), roofH);

        if (opt.Livery == LiveryType.RacingStripe || opt.Livery == LiveryType.ElectricBlue)
        {
            int sw = Math.Max(3, (int)(10 * scale));
            using var stripe = new SolidBrush(opt.Livery == LiveryType.ElectricBlue ? Color.DeepSkyBlue : Color.White);
            g.FillRectangle(stripe, x + w / 2 - sw / 2, y + (int)(6 * scale), sw, h - (int)(12 * scale));
        }
        else if (opt.Livery != LiveryType.Solid)
            CarLivery.Render(g, x, y + (int)(6 * scale), w, h, opt.Livery, opt.BodyColor, scale);

        int bumperY = y + h - (int)(14 * scale);
        int bumperH = Math.Max(4, (int)(10 * scale));
        g.FillRectangle(Brushes.Black, x + (int)(6 * scale), bumperY, w - (int)(12 * scale), bumperH);

        int light = Math.Max(3, (int)(5 * scale));
        int ly = bumperY + bumperH / 2 - light / 2;
        g.FillEllipse(Brushes.Orange, x + (int)(10 * scale), ly, light, light);
        g.FillEllipse(Brushes.Red, x + (int)(16 * scale), ly, light, light);
        g.FillEllipse(Brushes.Red, x + w - (int)(21 * scale), ly, light, light);
        g.FillEllipse(Brushes.Orange, x + w - (int)(15 * scale), ly, light, light);

        int wh = Math.Max(4, (int)(10 * scale));
        int ww = Math.Max(6, (int)(14 * scale));
        int wy = y + h - wh;
        g.FillRectangle(Brushes.DimGray, x + (int)(4 * scale), wy, ww, wh);
        g.FillRectangle(Brushes.DimGray, x + w - ww - (int)(4 * scale), wy, ww, wh);
    }

    private static void DrawNameTag(Graphics g, int x, int y, int w, float scale, string name, Color bg, Color border)
    {
        using var font = new Font("Arial", Math.Max(6f, 7.5f * scale), FontStyle.Bold);
        var sz = g.MeasureString(name, font);
        int tw = (int)sz.Width + (int)(10 * scale);
        int th = (int)sz.Height + (int)(4 * scale);
        int tx = x + w / 2 - tw / 2;
        int ty = y - th - (int)(4 * scale);
        using var bb = new SolidBrush(Color.FromArgb(200, bg));
        g.FillRectangle(bb, tx, ty, tw, th);
        using var pen = new Pen(border, Math.Max(1f, scale));
        g.DrawRectangle(pen, tx, ty, tw, th);
        g.DrawString(name, font, Brushes.White, tx + (int)(4 * scale), ty + (int)(1 * scale));
    }

    private static void DrawBoostFlames(Graphics g, int x, int y, int w, int h, float scale)
    {
        int ey = y + h - (int)(6 * scale);
        int flameH = Math.Max(8, (int)(22 * scale));
        int fw = Math.Max(5, (int)(10 * scale));
        foreach (int cx in new[] { x + (int)(18 * scale), x + w - (int)(18 * scale) })
        {
            using var outer = new SolidBrush(Color.FromArgb(220, 0, 200, 255));
            using var inner = new SolidBrush(Color.White);
            g.FillPolygon(outer, new[]
            {
                new Point(cx - fw / 2, ey),
                new Point(cx + fw / 2, ey),
                new Point(cx, ey + flameH)
            });
            g.FillPolygon(inner, new[]
            {
                new Point(cx - fw / 4, ey),
                new Point(cx + fw / 4, ey),
                new Point(cx, ey + flameH / 2)
            });
        }
    }

    public static int CarScreenY(int screenH, float relDistance, bool isLocalPlayer = false)
    {
        if (isLocalPlayer) return screenH - 100;
        int halfH = screenH / 2;
        float persp = Math.Clamp(1f - relDistance / 200f, 0.12f, 1.2f);
        return halfH + (int)((screenH - halfH - 100) * persp);
    }

    public static int CarScreenX(int screenW, float trackCurve, float carCurve, float scale, bool includeLateral = false, float lateral = 0)
    {
        float rel = carCurve - trackCurve + (includeLateral ? lateral : 0f);
        return screenW / 2 + (int)(screenW * rel * scale / 2.0f);
    }
}
