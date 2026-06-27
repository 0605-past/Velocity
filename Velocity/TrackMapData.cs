using System.Drawing;

namespace Pseudo3DRacer;

/// <summary>GitHub 雛型地圖格式：每張圖由 (曲率, 長度) 路段陣列定義。</summary>
public static class TrackMapData
{
    public readonly struct MapDef
    {
        public string Name { get; init; }
        public Color SkyTop { get; init; }
        public Color SkyBottom { get; init; }
        public Color GrassBright { get; init; }
        public Color GrassDark { get; init; }
        public (float Curvature, float Length)[] Sections { get; init; }
    }

    public static readonly MapDef[] Maps =
    {
        new()
        {
            Name = "經典新手村",
            SkyTop = Color.DeepSkyBlue,
            SkyBottom = Color.RoyalBlue,
            GrassBright = Color.FromArgb(100, 220, 80),
            GrassDark = Color.FromArgb(30, 140, 40),
            Sections = new (float, float)[]
            {
                (0.0f, 210f), (1.0f, 200f), (0.0f, 400f),
                (-1.0f, 150f), (0.0f, 200f), (0.4f, 400f)
            }
        },
        new()
        {
            Name = "極速沙漠高架",
            SkyTop = Color.OrangeRed,
            SkyBottom = Color.Gold,
            GrassBright = Color.FromArgb(210, 180, 140),
            GrassDark = Color.FromArgb(139, 69, 19),
            Sections = new (float, float)[]
            {
                (0.0f, 600f), (0.3f, 200f), (0.0f, 500f),
                (-0.3f, 200f), (0.0f, 300f)
            }
        },
        new()
        {
            Name = "秋名山地獄",
            SkyTop = Color.FromArgb(48, 0, 72),
            SkyBottom = Color.FromArgb(90, 25, 110),
            GrassBright = Color.FromArgb(70, 185, 55),
            GrassDark = Color.FromArgb(18, 95, 28),
            Sections = new (float, float)[]
            {
                (0.0f, 100f), (1.5f, 100f), (-1.5f, 100f), (0.0f, 150f),
                (1.8f, 120f), (-1.2f, 150f), (0.5f, 300f)
            }
        }
    };
}
