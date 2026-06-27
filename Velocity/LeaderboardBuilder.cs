namespace Pseudo3DRacer;

public static class LeaderboardBuilder
{
    public static string FormatLapProgress(int laps, int target, float distance, float totalDistance)
    {
        int pct = totalDistance > 0 ? (int)(distance / totalDistance * 100f) : 0;
        return $"{laps}/{target} ({pct}%)";
    }

    public static List<PlayerState> Build(
        string localId, Vehicle localVehicle,
        IEnumerable<PlayerState> remotePlayers,
        IEnumerable<AiBot> aiBots)
    {
        var list = new List<PlayerState>
        {
            new()
            {
                PlayerId = localId,
                LapCount = localVehicle.LapCount,
                Distance = localVehicle.Distance,
                IsLocal = true
            }
        };
        list.AddRange(remotePlayers);
        foreach (var b in aiBots)
            list.Add(new PlayerState { PlayerId = b.Name, LapCount = b.Vehicle.LapCount, Distance = b.Vehicle.Distance });
        return list.OrderByDescending(p => p.LapCount).ThenByDescending(p => p.Distance).ToList();
    }
}
