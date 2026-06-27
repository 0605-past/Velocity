using System.Drawing;

namespace Pseudo3DRacer;

public enum WeatherMode { Sunny, Rain, Night }

public static class WeatherSystem
{
    public static float GripMultiplier(WeatherMode mode) => mode switch
    {
        WeatherMode.Rain => 0.85f,
        WeatherMode.Night => 0.95f,
        _ => 1.0f
    };

    public static void ApplySkyTint(WeatherMode mode, ref Color top, ref Color bottom)
    {
        if (mode == WeatherMode.Night)
        {
            top = Color.FromArgb(top.R / 3, top.G / 3, top.B / 2);
            bottom = Color.FromArgb(bottom.R / 4, bottom.G / 4, bottom.B / 3);
        }
        else if (mode == WeatherMode.Rain)
        {
            top = Color.FromArgb(top.R * 2 / 3, top.G * 2 / 3, top.B);
            bottom = Color.FromArgb(bottom.R / 2, bottom.G / 2, bottom.B);
        }
    }

    public static void RenderOverlay(Graphics g, int w, int h, WeatherMode mode, float time)
    {
        if (mode == WeatherMode.Rain)
        {
            var rng = Random.Shared;
            using var pen = new Pen(Color.FromArgb(80, 180, 200, 255), 1);
            for (int i = 0; i < 80; i++)
            {
                int x = rng.Next(w);
                int y = (int)((time * 400 + i * 37) % h);
                g.DrawLine(pen, x, y, x - 3, y + 12);
            }
        }
        else if (mode == WeatherMode.Night)
        {
            using var veil = new SolidBrush(Color.FromArgb(60, 0, 0, 40));
            g.FillRectangle(veil, 0, 0, w, h);
        }
    }
}
