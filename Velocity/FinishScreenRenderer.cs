using System.Drawing;

namespace Pseudo3DRacer;

public sealed class FinishScreenViewModel
{
    public int FinishPosition { get; init; }
    public float TotalRaceTime { get; init; }
    public float BestLap { get; init; }
    public int ComboMax { get; init; }
    public int PickupsCollected { get; init; }
    public IReadOnlyList<string> NewAchievements { get; init; } = [];
    public bool HasReplay { get; init; }
}

public static class FinishScreenRenderer
{
    public static void Draw(Graphics g, FinishScreenViewModel vm, int w, int h, out Rectangle btnReplay, out Rectangle btnAgain, out Rectangle btnMenu)
    {
        using (var veil = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
            g.FillRectangle(veil, 0, 0, w, h);

        int mw = 360, mh = 280, mx = (w - mw) / 2, my = (h - mh) / 2;
        using (var panel = new SolidBrush(Color.FromArgb(230, 50, 52, 58)))
        {
            g.FillRectangle(panel, mx, my, mw, mh);
            g.DrawRectangle(Pens.Gray, mx, my, mw, mh);
        }

        using var titleF = new Font("Impact", 32, FontStyle.Bold);
        using var f2 = new Font("微軟正黑體", 12);
        string winTitle = vm.FinishPosition == 1 ? "YOU WIN!" : "FINISHED";
        var ts = g.MeasureString(winTitle, titleF);
        g.DrawString(winTitle, titleF, Brushes.Gold, mx + mw / 2f - ts.Width / 2, my + 16);

        int ty = my + 70;
        g.DrawString($"名次: 第 {vm.FinishPosition} 名", f2, Brushes.White, mx + 24, ty); ty += 26;
        g.DrawString($"總時間: {vm.TotalRaceTime:F2}s | 最佳圈: {(vm.BestLap < float.MaxValue ? vm.BestLap : 0):F2}s", f2, Brushes.White, mx + 24, ty); ty += 26;
        g.DrawString($"Combo 最高 x{vm.ComboMax} | 道具 {vm.PickupsCollected}", f2, Brushes.White, mx + 24, ty); ty += 30;
        foreach (var ach in vm.NewAchievements) { g.DrawString($"成就: {ach}", f2, Brushes.Lime, mx + 24, ty); ty += 22; }

        btnReplay = new Rectangle(mx + 20, my + mh - 55, 100, 34);
        btnAgain = new Rectangle(mx + 130, my + mh - 55, 100, 34);
        btnMenu = new Rectangle(mx + 240, my + mh - 55, 100, 34);
        if (vm.HasReplay)
        {
            g.FillRectangle(Brushes.ForestGreen, btnReplay);
            g.DrawString("重播上一圈", f2, Brushes.White, btnReplay.X + 6, btnReplay.Y + 7);
        }
        g.FillRectangle(Brushes.OrangeRed, btnAgain);
        g.DrawString("再來一局", f2, Brushes.White, btnAgain.X + 18, btnAgain.Y + 7);
        g.FillRectangle(Brushes.DimGray, btnMenu);
        g.DrawString("主選單", f2, Brushes.White, btnMenu.X + 28, btnMenu.Y + 7);
    }
}
