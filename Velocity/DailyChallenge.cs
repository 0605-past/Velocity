using System;

namespace Pseudo3DRacer;

public enum ChallengeType { DailyWin, StuntShow, PerfectLap, ComboMaster, PickupHunter, MapDomination }

public class DailyChallenge
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string RewardLivery { get; set; } = "";
    public ChallengeType Type { get; set; }
    public int TargetMap { get; set; } = -1;

    public static DailyChallenge GetToday()
    {
        var today = DateTime.Today;
        int seed = today.Year * 10000 + today.Month * 100 + today.Day;
        var rng = new Random(seed);
        int pick = rng.Next(6);
        return pick switch
        {
            0 => new DailyChallenge { Type = ChallengeType.DailyWin, Title = "每日冠軍", Description = "贏得一場比賽", RewardLivery = "RacingStripe" },
            1 => new DailyChallenge { Type = ChallengeType.StuntShow, Title = "特技秀", Description = "單場達成 S 評級", RewardLivery = "Flames" },
            2 => new DailyChallenge { Type = ChallengeType.PerfectLap, Title = "完美主義", Description = "不撞牆完成一圈", RewardLivery = "Carbon" },
            3 => new DailyChallenge { Type = ChallengeType.ComboMaster, Title = "Combo 大師", Description = "達成 x5 Combo", RewardLivery = "Neon" },
            4 => new DailyChallenge { Type = ChallengeType.PickupHunter, Title = "道具獵人", Description = "單場拾取 5 個道具", RewardLivery = "Checker" },
            _ => new DailyChallenge { Type = ChallengeType.MapDomination, Title = "地圖制霸", Description = "在秋名山獲勝", RewardLivery = "RacingStripe", TargetMap = 2 }
        };
    }

    public bool IsCompleted(RaceResult result)
    {
        return Type switch
        {
            ChallengeType.DailyWin => result.Position == 1,
            ChallengeType.StuntShow => result.HighestRank == "S",
            ChallengeType.PerfectLap => result.NoWallHit,
            ChallengeType.ComboMaster => result.ComboMax >= 5,
            ChallengeType.PickupHunter => result.PickupsCollected >= 5,
            ChallengeType.MapDomination => result.MapIndex == TargetMap && result.Position == 1,
            _ => false
        };
    }
}
