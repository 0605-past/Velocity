using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pseudo3DRacer;

public static class CountdownRenderer
{
    public static void DrawStartGrid(Graphics g, int w, int h, float sectionCurvature)
    {
        int halfH = h / 2;
        int baseY = h - Track.CarScreenOffsetY + 8;
        for (int row = 0; row < 14; row++)
        {
            int y = baseY + row * 2;
            float perspective = Math.Clamp((y - halfH) / (float)halfH, 0.55f, 0.98f);
            float mid = Track.GetRoadCenterGitHub(sectionCurvature, perspective);
            float roadHalf = (0.1f + perspective * 0.8f) * 0.5f;
            int left = (int)((mid - roadHalf) * w);
            int right = (int)((mid + roadHalf) * w);
            if (right <= left) continue;
            bool light = row % 2 == 0;
            using var brush = new SolidBrush(light ? Color.FromArgb(58, 58, 62) : Color.FromArgb(28, 28, 30));
            g.FillRectangle(brush, left, y, right - left, 2);
        }
    }

    public static void DrawNumber(Graphics g, int w, int h, float timer)
    {
        string txt = timer > 2 ? "3" : timer > 1 ? "2" : timer > 0 ? "1" : "GO!";
        float pulse = 1f + 0.06f * MathF.Sin(timer * 14f);
        float fontSize = (txt == "GO!" ? 76f : 96f) * pulse;

        using var panel = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        g.FillRectangle(panel, w / 2 - 90, 8, 180, 110);
        using var ring = new Pen(Color.FromArgb(200, 255, 200, 60), 3f);
        g.DrawRectangle(ring, w / 2 - 88, 10, 176, 106);

        using var shadowFont = new Font("Impact", fontSize, FontStyle.Bold);
        var ts = g.MeasureString(txt, shadowFont);
        float tx = w / 2f - ts.Width / 2f;
        float ty = 28f;
        using var shadowBrush = new SolidBrush(Color.FromArgb(180, 40, 20, 0));
        g.DrawString(txt, shadowFont, shadowBrush, tx + 3, ty + 3);
        using var fillBrush = new SolidBrush(txt == "GO!" ? Color.Lime : Color.FromArgb(255, 255, 210, 60));
        g.DrawString(txt, shadowFont, fillBrush, tx, ty);
    }

    public static void DrawWaitingHost(Graphics g, string netStatus)
    {
        g.Clear(Color.Black);
        using var wf = new Font("微軟正黑體", 16, FontStyle.Bold);
        g.DrawString("等待主機開賽...", wf, Brushes.Gold, 20, 20);
        g.DrawString(netStatus, wf, Brushes.Cyan, 20, 48);
    }

    public static void DrawLineup(Graphics g, int w, int h, Track track,
        Vehicle player, string playerName, float renderCurvature, float trackCurvature,
        IEnumerable<AiBot> aiBots, IEnumerable<PlayerState> remotePlayers,
        Func<float, float> wrapDistance)
    {
        var lineup = new List<(Vehicle? veh, string name, float rel, Color color, bool local)>
        {
            (player, playerName, 0f, Color.Red, true)
        };
        foreach (var bot in aiBots)
            lineup.Add((bot.Vehicle, bot.Name, bot.Vehicle.Distance - player.Distance, bot.CarColor, false));

        var centers = new List<Point>();
        foreach (var car in lineup.OrderByDescending(c => c.rel))
        {
            if (car.local)
            {
                player.Render(g, w, h, track, renderCurvature, car.name);
                centers.Add(new Point(w / 2, h - Track.CarScreenOffsetY - 20));
            }
            else if (car.veh != null)
            {
                car.veh.RenderRemote(g, w, h, track, renderCurvature, trackCurvature, car.veh.PlayerCurvature, 0,
                    car.name, car.rel, false, car.color);
                float scale = Math.Clamp(1f - car.rel / 130f, 0.15f, 1.05f);
                int cy = h / 2 + (int)((h / 2 - Track.CarScreenOffsetY) * scale);
                float lane = car.veh.PlayerCurvature - trackCurvature;
                float p = Math.Clamp((cy + 35 * scale - h / 2f) / (h / 2f), 0f, 0.999f);
                float cx = (Track.GetRoadCenterGitHub(renderCurvature, p) + lane * scale / 2f) * w;
                centers.Add(new Point((int)cx, cy - 20));
            }
        }
        foreach (var p in remotePlayers)
        {
            float rel = wrapDistance(p.Distance - player.Distance);
            player.RenderRemote(g, w, h, track, renderCurvature, trackCurvature, p.PlayerCurvature, p.SteerDir,
                p.PlayerId, rel, p.IsBoosting, Color.Gold);
        }
        if (centers.Count > 1)
        {
            using var pathPen = new Pen(Color.FromArgb(140, 255, 220, 80), 2f) { DashStyle = DashStyle.Dot };
            for (int i = 0; i < centers.Count - 1; i++)
                g.DrawLine(pathPen, centers[i], centers[i + 1]);
        }
    }
}
