namespace Pseudo3DRacer;

public enum GameState { MainMenu, Countdown, Playing, Paused, Finished }

public class PlayerState
{
    public string PlayerId { get; set; } = "";
    public float Distance { get; set; }
    public float PlayerCurvature { get; set; }
    public float LateralOffset { get; set; }
    public int SteerDir { get; set; }
    public int LapCount { get; set; }
    public bool IsBoosting { get; set; }
    public string Rank { get; set; } = "C";
    public DateTime LastSeen { get; set; }
    public bool IsLocal { get; set; }
}
