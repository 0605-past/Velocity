using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pseudo3DRacer;

public class TrackSection
{
    public float Curvature { get; set; }
    public float Length { get; set; }
    public TrackSection(float curvature, float length) { Curvature = curvature; Length = length; }
}

public class RoadsideObject
{
    public float Distance { get; set; }
    public bool LeftSide { get; set; }
    public int Type { get; set; }
}

public class Track
{
    public List<TrackSection> Sections { get; } = new();
    public List<TrackPickup> Pickups { get; private set; } = new();
    public List<RoadsideObject> RoadsideObjects { get; } = new();
    public float TotalDistance { get; private set; }
    public string MapName { get; private set; } = "經典新手村";
    public int MapIndex { get; private set; }

    public Color SkyTop { get; private set; } = Color.FromArgb(55, 0, 90);
    public Color SkyBottom { get; private set; } = Color.FromArgb(100, 30, 120);
    public Color GrassBright { get; private set; } = Color.FromArgb(80, 200, 60);
    public Color GrassDark { get; private set; } = Color.FromArgb(20, 110, 30);

    private List<PointF> mapPoints = new();
    private float mapMinX, mapMaxX, mapMinY, mapMaxY;

    public Track() => SetMap(2);

    public void SetMap(int mapIndex)
    {
        MapIndex = mapIndex;
        Sections.Clear();
        RoadsideObjects.Clear();
        TotalDistance = 0;

        int idx = Math.Clamp(mapIndex, 0, TrackMapData.Maps.Length - 1);
        var def = TrackMapData.Maps[idx];
        MapName = def.Name;
        SkyTop = def.SkyTop;
        SkyBottom = def.SkyBottom;
        GrassBright = def.GrassBright;
        GrassDark = def.GrassDark;
        foreach (var (curvature, length) in def.Sections)
            Sections.Add(new TrackSection(curvature, length));

        foreach (var s in Sections) TotalDistance += s.Length;
        Pickups = PickupFactory.Generate(TotalDistance, mapIndex);
        GenerateRoadside();
        CalculateMapPoints();
    }

    private void GenerateRoadside()
    {
        var rng = new Random(MapIndex * 77);
        for (float d = 40; d < TotalDistance; d += 70 + rng.Next(50))
            RoadsideObjects.Add(new RoadsideObject { Distance = d, LeftSide = rng.Next(2) == 0, Type = rng.Next(3) });
    }

    /// <summary>GitHub 雛型小地圖：依路段離散曲率積分路徑。</summary>
    private void CalculateMapPoints()
    {
        mapPoints.Clear();
        float x = 0, y = 0, angle = -(float)Math.PI / 2;
        mapMinX = mapMaxX = x; mapMinY = mapMaxY = y;
        for (float d = 0; d < TotalDistance; d += 2f)
        {
            float curve = GetCurrentDetails(d).curvature;
            angle += curve * 0.005f * 2f;
            x += (float)Math.Cos(angle) * 2f;
            y += (float)Math.Sin(angle) * 2f;
            mapPoints.Add(new PointF(x, y));
            if (x < mapMinX) mapMinX = x; if (x > mapMaxX) mapMaxX = x;
            if (y < mapMinY) mapMinY = y; if (y > mapMaxY) mapMaxY = y;
        }
    }

    public (float curvature, int sectionIndex) GetCurrentDetails(float distance)
    {
        if (TotalDistance <= 0) return (0, 0);
        float loop = distance % TotalDistance;
        if (loop < 0) loop += TotalDistance;
        float offset = 0; int index = 0;
        while (index < Sections.Count) { offset += Sections[index].Length; if (offset > loop) break; index++; }
        int fi = Math.Max(0, Math.Min(index, Sections.Count - 1));
        return (Sections[fi].Curvature, fi);
    }

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

    private static float SmootherStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>路段交界處平滑插值，直道進彎時曲率漸增而非瞬間跳變。</summary>
    public float GetInterpolatedCurvature(float distance, float blendLen = 85f)
    {
        if (Sections.Count == 0 || TotalDistance <= 0) return 0;
        float loop = ((distance % TotalDistance) + TotalDistance) % TotalDistance;
        float traveled = 0f;
        for (int i = 0; i < Sections.Count; i++)
        {
            var seg = Sections[i];
            float segStart = traveled;
            float segEnd = traveled + seg.Length;
            float blend = Math.Min(blendLen, Math.Max(seg.Length * 0.42f, 32f));
            if (loop >= segStart && loop < segEnd)
            {
                float cur = seg.Curvature;
                float prev = i > 0 ? Sections[i - 1].Curvature : Sections[^1].Curvature;
                float next = i < Sections.Count - 1 ? Sections[i + 1].Curvature : Sections[0].Curvature;
                float fromStart = loop - segStart;
                float toEnd = segEnd - loop;
                if (blend > 1f && fromStart < blend)
                {
                    float t = SmootherStep(Math.Clamp(fromStart / blend, 0f, 1f));
                    return prev + (cur - prev) * t;
                }
                if (blend > 1f && toEnd < blend)
                {
                    float t = SmootherStep(Math.Clamp(1f - toEnd / blend, 0f, 1f));
                    return cur + (next - cur) * t;
                }
                return cur;
            }
            traveled = segEnd;
        }
        return Sections[0].Curvature;
    }

    public float GetLookAheadDistance(float carDistance, float perspective)
    {
        float p = Math.Clamp(perspective, 0f, 0.999f);
        return carDistance + (1f - p) * (1f - p) * 220f + 25f;
    }

    public float GetRoadCenterNorm(float carDistance, float perspective)
    {
        float look = GetLookAheadDistance(carDistance, perspective);
        float offset = GetAccumulatedCurvatureDiscrete(look) - GetAccumulatedCurvatureDiscrete(carDistance);
        float bend = GetCurrentDetails(look).curvature * MathF.Pow(1f - perspective, 3f);
        return 0.5f + offset * 0.44f + bend;
    }

    public PointF GetMapPointAt(float distance)
    {
        if (mapPoints.Count == 0 || TotalDistance <= 0) return PointF.Empty;
        float loop = ((distance % TotalDistance) + TotalDistance) % TotalDistance;
        float t = loop / TotalDistance * mapPoints.Count;
        int i = Math.Clamp((int)t, 0, mapPoints.Count - 1);
        int j = (i + 1) % mapPoints.Count;
        float frac = t - i;
        var a = mapPoints[i];
        var b = mapPoints[j];
        return new PointF(a.X + (b.X - a.X) * frac, a.Y + (b.Y - a.Y) * frac);
    }

    public float GetAccumulatedCurvature(float distance) => GetAccumulatedCurvatureDiscrete(distance);

    public float GetAccumulatedCurvatureSmooth(float distance, float step = 4f)
    {
        if (TotalDistance <= 0) return 0;
        float loop = ((distance % TotalDistance) + TotalDistance) % TotalDistance;
        float acc = 0f;
        for (float d = 0f; d < loop; d += step)
            acc += GetInterpolatedCurvature(d) * step / 70f;
        float remainder = loop % step;
        if (remainder > 0.01f)
            acc += GetInterpolatedCurvature(loop - remainder * 0.5f) * remainder / 70f;
        return acc;
    }

    public float GetAccumulatedCurvatureDiscrete(float distance)
    {
        if (TotalDistance <= 0) return 0;
        float loop = ((distance % TotalDistance) + TotalDistance) % TotalDistance;
        float acc = 0f, traveled = 0f;
        foreach (var s in Sections)
        {
            if (traveled + s.Length >= loop)
            {
                acc += s.Curvature * (loop - traveled) / 70f;
                break;
            }
            acc += s.Curvature * s.Length / 70f;
            traveled += s.Length;
        }
        return acc;
    }

    /// <summary>GitHub 雛型透視彎道：整條掃描線共用當前路段曲率，避免道路被切成片段。</summary>
    public static float GetRoadCenterGitHub(float sectionCurvature, float perspective)
    {
        float bend = MathF.Pow(1f - perspective, 3f);
        return 0.5f + sectionCurvature * bend;
    }

    public (float left, float right, float mid) GetRoadEdges(float perspective, float sectionCurvature, int width)
    {
        float roadW = (0.1f + perspective * 0.8f) * 0.5f;
        float clipW = roadW * 0.15f;
        float mid = GetRoadCenterGitHub(sectionCurvature, perspective);
        return ((mid - roadW - clipW) * width, (mid + roadW + clipW) * width, mid * width);
    }

    public (float left, float right, float mid) GetRoadEdgesAt(float carDistance, float perspective, int width)
    {
        float sectionCurvature = GetCurrentDetails(carDistance).curvature;
        return GetRoadEdges(perspective, sectionCurvature, width);
    }

    public const int CarScreenOffsetY = 100;
    public const int CarBodyWidthPx = 110;
    public const int CarBodyHeightPx = 55;

    public readonly struct LaneBounds
    {
        public float MinLane { get; init; }
        public float MaxLane { get; init; }
        public float RumbleMinLane { get; init; }
        public float RumbleMaxLane { get; init; }
    }

    /// <summary>依 RenderClassic 與車輛螢幕位置，換算紅白路緣對應的 lane 邊界。</summary>
    public static LaneBounds ComputeLaneBounds(int screenWidth, int screenHeight, float sectionCurvature, float carScale = 1f)
    {
        int halfH = screenHeight / 2;
        int carW = Math.Max(1, (int)(CarBodyWidthPx * carScale));
        int carH = Math.Max(1, (int)(CarBodyHeightPx * carScale));
        int carBaseY = screenHeight - CarScreenOffsetY + carH - (int)(18 * carScale);

        float perspective = Math.Clamp((carBaseY - halfH) / (float)halfH, 0f, 0.999f);
        float roadFull = 0.1f + perspective * 0.8f;
        float roadHalf = roadFull * 0.5f;
        float clipHalf = roadFull * 0.15f;
        float mid = GetRoadCenterGitHub(sectionCurvature, perspective);
        float carHalfNorm = carW / (2f * screenWidth);

        float leftAsphalt = mid - roadHalf;
        float rightAsphalt = mid + roadHalf;
        float leftRumble = mid - roadHalf - clipHalf;
        float rightRumble = mid + roadHalf + clipHalf;

        return new LaneBounds
        {
            MinLane = 2f * (leftAsphalt - 0.5f + carHalfNorm),
            MaxLane = 2f * (rightAsphalt - 0.5f - carHalfNorm),
            RumbleMinLane = 2f * (leftRumble - 0.5f + carHalfNorm),
            RumbleMaxLane = 2f * (rightRumble - 0.5f - carHalfNorm)
        };
    }

    public float GetRoadsideScreenX(float perspective, float sectionCurvature, int width, bool leftSide)
    {
        var (left, right, _) = GetRoadEdges(perspective, sectionCurvature, width);
        return leftSide ? left - 25 * (1 - perspective) : right + 25 * (1 - perspective);
    }

    public void RenderFrame(Bitmap bmp, float carDistance, float trackCurvature, float sectionCurvature, int sectionIndex, WeatherMode weather)
    {
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        RenderClassic(g, bmp.Width, bmp.Height, carDistance, trackCurvature, sectionCurvature, sectionIndex, weather);
        RenderMountains(g, bmp.Width, bmp.Height, trackCurvature);
        RenderRoadside(g, bmp.Width, bmp.Height, carDistance, sectionCurvature);
        RenderPickups(g, bmp.Width, bmp.Height, carDistance, sectionCurvature);
    }

    public void RenderClassic(Graphics g, int width, int height, float carDistance, float currentTrackCurvature, float currentSectionCurvature, int sectionIndex, WeatherMode weather)
    {
        int halfHeight = height / 2;
        Color skyTop = SkyTop, skyBot = SkyBottom;
        WeatherSystem.ApplySkyTint(weather, ref skyTop, ref skyBot);

        using (var top = new SolidBrush(skyTop))
        using (var bot = new SolidBrush(skyBot))
        {
            g.FillRectangle(top, 0, 0, width, halfHeight / 2);
            g.FillRectangle(bot, 0, halfHeight / 2, width, halfHeight / 2);
        }

        for (int x = 0; x < width; x++)
        {
            int hillHeight = (int)(Math.Abs(Math.Sin(x * 0.015f + carDistance * 0.02f) * 20.0f));
            g.DrawLine(Pens.SaddleBrown, x, halfHeight - hillHeight, x, halfHeight);
        }

        using var grassBright = new SolidBrush(GrassBright);
        using var grassDark = new SolidBrush(GrassDark);
        using var roadDark = new SolidBrush(Color.FromArgb(25, 25, 25));
        using var roadNear = new SolidBrush(Color.FromArgb(42, 42, 46));

        for (int y = 0; y < halfHeight; y++)
        {
            float perspective = (float)y / halfHeight;
            float roadWidth = 0.1f + perspective * 0.8f;
            float clipWidth = roadWidth * 0.15f;
            roadWidth *= 0.5f;

            float middlePoint = GetRoadCenterGitHub(currentSectionCurvature, perspective);

            int leftGrass = (int)((middlePoint - roadWidth - clipWidth) * width);
            int leftClip = (int)((middlePoint - roadWidth) * width);
            int rightClip = (int)((middlePoint + roadWidth) * width);
            int rightGrass = (int)((middlePoint + roadWidth + clipWidth) * width);
            int rowY = halfHeight + y;

            bool grassToggle = Math.Sin(20.0f * Math.Pow(1.0f - perspective, 3) + carDistance * 0.1f) > 0;
            bool clipToggle = Math.Sin(80.0f * Math.Pow(1.0f - perspective, 2) + carDistance) > 0;

            Brush grassBrush = grassToggle ? grassBright : grassDark;
            Brush clipBrush = clipToggle ? Brushes.Red : Brushes.Silver;
            Brush roadBrush = perspective > 0.88f ? roadNear : roadDark;

            g.FillRectangle(grassBrush, 0, rowY, leftGrass, 1);
            g.FillRectangle(clipBrush, leftGrass, rowY, leftClip - leftGrass, 1);
            g.FillRectangle(roadBrush, leftClip, rowY, rightClip - leftClip, 1);
            g.FillRectangle(clipBrush, rightClip, rowY, rightGrass - rightClip, 1);
            g.FillRectangle(grassBrush, rightGrass, rowY, width - rightGrass, 1);
        }
    }

    private void RenderMountains(Graphics g, int w, int h, float trackCurvature)
    {
        int halfH = h / 2;
        var pts = new List<Point> { new(0, halfH) };
        for (int x = 0; x < w; x += 8)
        {
            int hill = (int)(Math.Abs(Math.Sin(x * 0.02f + trackCurvature * 0.5f) * 18) + Math.Abs(Math.Sin(x * 0.05f) * 8));
            pts.Add(new Point(x, halfH - hill));
        }
        pts.Add(new Point(w, halfH));
        using var hillBrush = new SolidBrush(Color.FromArgb(120, 60, 35));
        g.FillPolygon(hillBrush, pts.ToArray());
    }

    public void RenderGoSigns(Graphics g, int w, int h, float sectionCurvature)
    {
        int halfH = h / 2;
        float persp = 0.88f;
        int y = halfH + (int)(halfH * persp);
        foreach (bool left in new[] { true, false })
        {
            float sx = GetRoadsideScreenX(persp, sectionCurvature, w, left);
            int sh = Math.Max(16, (int)(28 * persp));
            g.FillRectangle(Brushes.DimGray, (int)sx - 2, y - sh, 4, sh);
            using var font = new Font("Arial", 7, FontStyle.Bold);
            g.DrawString("GO!", font, Brushes.White, (int)sx - 10, y - sh - 2);
        }
    }

    private void RenderRoadside(Graphics g, int w, int h, float carDistance, float sectionCurvature)
    {
        int halfH = h / 2;
        foreach (var obj in RoadsideObjects)
        {
            float rel = obj.Distance - carDistance;
            if (rel < 0) rel += TotalDistance;
            if (rel > TotalDistance / 2) rel -= TotalDistance;
            if (rel < 5 || rel > 280) continue;

            float perspective = 1f - rel / 280f;
            if (perspective < 0.04f) continue;
            int y = halfH + (int)(halfH * perspective);
            float sx = GetRoadsideScreenX(perspective, sectionCurvature, w, obj.LeftSide);
            int size = Math.Max(6, (int)(22 * perspective));

            if (obj.Type == 0)
            {
                g.FillEllipse(Brushes.ForestGreen, (int)sx - size / 2, y - size, size, size);
                g.FillEllipse(Brushes.DarkGreen, (int)sx - size / 3, y - size - size / 4, size / 2, size / 2);
            }
            else if (obj.Type == 1)
            {
                g.FillRectangle(Brushes.SaddleBrown, (int)sx - 1, y - size, 3, size);
                g.FillEllipse(Brushes.LimeGreen, (int)sx - size / 2, y - size - 4, size, size / 2);
            }
        }
    }

    private void RenderPickups(Graphics g, int w, int h, float carDistance, float sectionCurvature)
    {
        int halfH = h / 2;
        foreach (var p in Pickups)
        {
            if (!p.Active) continue;
            float rel = p.Distance - carDistance;
            if (rel < 0) rel += TotalDistance;
            if (rel > TotalDistance / 2) rel -= TotalDistance;
            if (rel < 3 || rel > 250) continue;

            float perspective = 1f - rel / 250f;
            int y = halfH + (int)(halfH * perspective);
            var (_, _, mid) = GetRoadEdges(perspective, sectionCurvature, w);
            int size = Math.Max(4, (int)(12 * perspective));
            Color c = p.Type switch
            {
                PickupType.Nitro => Color.DeepSkyBlue,
                PickupType.Shield => Color.Cyan,
                PickupType.Magnet => Color.Magenta,
                _ => Color.Gold
            };
            using var brush = new SolidBrush(Color.FromArgb(200, c));
            g.FillEllipse(brush, (int)mid - size / 2, y - size / 2, size, size);
            g.DrawEllipse(Pens.White, (int)mid - size / 2, y - size / 2, size, size);
        }
    }

    public void RenderMiniMap(Graphics g, int screenWidth, float carDistance, List<(float dist, Color color)> markers)
    {
        int mapSize = 100, padding = 20;
        int mapLeft = screenWidth - mapSize - padding, mapTop = padding;
        using (var bg = new SolidBrush(Color.FromArgb(150, Color.Black)))
        {
            g.FillRectangle(bg, mapLeft - 5, mapTop - 5, mapSize + 10, mapSize + 10);
            g.DrawRectangle(Pens.White, mapLeft - 5, mapTop - 5, mapSize + 10, mapSize + 10);
        }
        if (mapPoints.Count < 2 || TotalDistance <= 0) return;

        float tw = mapMaxX - mapMinX, th = mapMaxY - mapMinY;
        float scale = Math.Min(mapSize / tw, mapSize / th) * 0.85f;
        float ox = mapLeft + (mapSize - tw * scale) / 2f - mapMinX * scale;
        float oy = mapTop + (mapSize - th * scale) / 2f - mapMinY * scale;

        var pts = new PointF[mapPoints.Count];
        for (int i = 0; i < mapPoints.Count; i++)
            pts[i] = new PointF(mapPoints[i].X * scale + ox, mapPoints[i].Y * scale + oy);

        using (var pen = new Pen(Color.LightGray, 2))
        {
            g.DrawLines(pen, pts);
            g.DrawLine(pen, pts[^1], pts[0]);
        }

        foreach (var m in markers)
        {
            var p = GetMapPointAt(m.dist);
            float mx = p.X * scale + ox;
            float my = p.Y * scale + oy;
            using var b = new SolidBrush(m.color);
            g.FillEllipse(b, mx - 3, my - 3, 6, 6);
        }
    }

    public TrackPickup? CheckPickup(float distance, float lateralOffset)
    {
        foreach (var p in Pickups)
        {
            if (!p.Active) continue;
            float d = Math.Abs((distance % TotalDistance) - (p.Distance % TotalDistance));
            if (d > TotalDistance / 2) d = TotalDistance - d;
            if (d < 8f && Math.Abs(lateralOffset) < 0.35f)
            {
                p.Active = false;
                return p;
            }
        }
        return null;
    }
}
