using System;
using System.Collections.Generic;

namespace Pseudo3DRacer;

public class AchievementDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public Func<RaceResult, GameSaveData, bool> Check { get; set; } = (_, _) => false;
}

public class RaceResult
{
    public int Position { get; set; }
    public int TotalRacers { get; set; }
    public float LapTime { get; set; }
    public int MapIndex { get; set; }
    public string HighestRank { get; set; } = "C";
    public int ComboMax { get; set; }
    public int PickupsCollected { get; set; }
    public bool NoWallHit { get; set; }
    public bool BeatGhost { get; set; }
}

public static class AchievementTracker
{
    public static readonly List<AchievementDef> All = new()
    {
        new() { Id = "first_race", Title = "初出茅廬", Check = (_, s) => s.TotalRaces >= 1 },
        new() { Id = "first_win", Title = "首戰告捷", Check = (r, _) => r.Position == 1 },
        new() { Id = "akina_god", Title = "秋名車神", Check = (r, s) => r.MapIndex == 2 && r.Position == 1 && r.LapTime < s.BestLapMap2 + 0.01f && r.LapTime > 0 },
        new() { Id = "s_master", Title = "S 級達人", Check = (r, _) => r.HighestRank == "S" },
        new() { Id = "perfect_lap", Title = "完美一圈", Check = (r, _) => r.NoWallHit },
        new() { Id = "combo_master", Title = "Combo 大師", Check = (r, _) => r.ComboMax >= 5 },
        new() { Id = "win_streak", Title = "連勝三場", Check = (_, s) => s.WinStreak >= 3 },
        new() { Id = "legend", Title = "傳奇車手", Check = (_, s) => s.TotalRaces >= 50 },
        new() { Id = "pickup_hunter", Title = "道具獵人", Check = (r, _) => r.PickupsCollected >= 5 },
        new() { Id = "ghost_slayer", Title = "幽靈剋星", Check = (r, _) => r.BeatGhost },
        new() { Id = "map_dominator", Title = "地圖制霸", Check = (r, _) => r.MapIndex == 1 && r.Position == 1 },
        new() { Id = "nitro_king", Title = "氮氣之王", Check = (_, s) => s.SRankCount >= 10 }
    };

    public static List<string> Evaluate(RaceResult result, GameSaveData save)
    {
        var unlocked = new List<string>();
        foreach (var ach in All)
        {
            if (save.UnlockedAchievements.Contains(ach.Id)) continue;
            if (ach.Check(result, save))
            {
                save.UnlockedAchievements.Add(ach.Id);
                unlocked.Add(ach.Title);
            }
        }
        return unlocked;
    }
}
