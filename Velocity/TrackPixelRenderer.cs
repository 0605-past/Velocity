using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Pseudo3DRacer;

public static class TrackPixelRenderer
{
    public static unsafe void RenderFrame(Bitmap bmp, Track track, float carDistance, float trackCurvature, float sectionCurvature, int sectionIndex, WeatherMode weather)
    {
        int w = bmp.Width, h = bmp.Height;
        int halfH = h / 2;

        Color skyTop = track.SkyTop;
        Color skyMid = Color.FromArgb((skyTop.R + track.SkyBottom.R) / 2, (skyTop.G + track.SkyBottom.G) / 2, (skyTop.B + track.SkyBottom.B) / 2);
        Color skyBot = track.SkyBottom;
        WeatherSystem.ApplySkyTint(weather, ref skyTop, ref skyBot);

        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + y * stride;
                if (y < halfH)
                {
                    float t = (float)y / halfH;
                    Color sky = t < 0.55f
                        ? Lerp(skyTop, skyMid, t / 0.55f)
                        : Lerp(skyMid, skyBot, (t - 0.55f) / 0.45f);
                    byte r = sky.R, g = sky.G, b = sky.B;
                    for (int x = 0; x < w; x++)
                    {
                        byte* px = row + x * 4;
                        px[0] = b; px[1] = g; px[2] = r; px[3] = 255;
                    }
                }
                else
                {
                    int ry = y - halfH;
                    float perspective = (float)ry / halfH;
                    float roadW = (0.1f + perspective * 0.8f) * 0.5f;
                    float clipW = roadW * 0.15f;
                    float mid = 0.5f + sectionCurvature * (float)Math.Pow(1.0 - perspective, 3);
                    int lGrass = (int)((mid - roadW - clipW) * w);
                    int lClip = (int)((mid - roadW) * w);
                    int rClip = (int)((mid + roadW) * w);
                    int rGrass = (int)((mid + roadW + clipW) * w);

                    bool grassBright = ((y / 4) % 2) == 0;
                    Color grassC = grassBright ? track.GrassBright : track.GrassDark;

                    int strip = Math.Max(2, (int)(6 * (1.0 - perspective) + 2));

                    Color roadC = perspective > 0.78f
                        ? Color.FromArgb(235, 235, 235)
                        : Color.FromArgb(25, 25, 25);

                    for (int x = 0; x < w; x++)
                    {
                        bool clipRed = ((x / strip) + (int)(perspective * 8)) % 2 == 0;
                        Color c;
                        if (x < lGrass || x >= rGrass) c = grassC;
                        else if (x < lClip || x >= rClip) c = clipRed ? Color.FromArgb(200, 40, 40) : Color.White;
                        else c = roadC;
                        byte* px = row + x * 4;
                        px[0] = c.B; px[1] = c.G; px[2] = c.R; px[3] = 255;
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}
