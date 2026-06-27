using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Pseudo3DRacer;

public class GameSaveData
{
    public float BestLapTime { get; set; } = float.MaxValue;
    public int TotalWins { get; set; }
    public int TotalRaces { get; set; }
    public int WinStreak { get; set; }
    public int SRankCount { get; set; }
    public List<string> UnlockedAchievements { get; set; } = new();
    public List<string> UnlockedLiveries { get; set; } = new() { "Solid", "RacingStripe", "Flames", "Carbon", "Checker", "ElectricBlue" };
    public Dictionary<int, float[]> BestSectorByMap { get; set; } = new()
    {
        [0] = new float[] { float.MaxValue, float.MaxValue, float.MaxValue },
        [1] = new float[] { float.MaxValue, float.MaxValue, float.MaxValue },
        [2] = new float[] { float.MaxValue, float.MaxValue, float.MaxValue }
    };
    public string LastChallengeDate { get; set; } = "";
    public bool LastChallengeCompleted { get; set; }
    public float BestLapMap2 { get; set; } = float.MaxValue;

    private static string SavePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Velocity", "save.json");

    public static GameSaveData Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                return JsonSerializer.Deserialize<GameSaveData>(json) ?? new GameSaveData();
            }
        }
        catch { }
        return new GameSaveData();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SavePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SavePath, json);
        }
        catch { }
    }

    public void AutoUnlock()
    {
        if (SRankCount >= 5 && !UnlockedLiveries.Contains("Neon")) UnlockedLiveries.Add("Neon");
        if (TotalWins >= 10 && !UnlockedLiveries.Contains("Flames")) UnlockedLiveries.Add("Flames");
        if (UnlockedAchievements.Count >= 6 && !UnlockedLiveries.Contains("Carbon")) UnlockedLiveries.Add("Carbon");
        if (BestLapMap2 < 60f && BestLapMap2 > 0 && !UnlockedLiveries.Contains("Checker")) UnlockedLiveries.Add("Checker");
    }
}
