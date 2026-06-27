using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Pseudo3DRacer;

public class Vehicle
{
    public float Distance { get; set; }
    public float PlayerCurvature { get; set; }
    public float LateralOffset { get; set; }
    public int LapCount { get; set; }
    public float Speed { get; private set; }
    public int CurrentSteerDir { get; private set; }
    public float Nitro { get; private set; } = 100f;
    public bool IsBoosting { get; private set; }
    public bool IsDrifting { get; private set; }
    public float DriftTimer { get; private set; }
    public float DriftMomentum { get; private set; }
    public string CurrentRank { get; private set; } = "C";
    public int Combo { get; private set; }
    public int ComboMax { get; private set; }
    public float RankAnimScale { get; private set; } = 1f;
    public float RankFlashTimer { get; private set; }
    public float RankRingPhase { get; private set; }
    public float NitroPhase { get; private set; }
    public float NitroIntensity { get; private set; }
    public bool ShieldActive { get; private set; }
    public float ShieldTimer { get; private set; }
    public float SlowTimer { get; private set; }
    public float MagnetTimer { get; private set; }
    public bool HasMagnet => MagnetTimer > 0;
    public string ActiveBanner { get; private set; } = "";
    public bool WallHitThisLap { get; private set; }
    public int PickupsCollected { get; set; }

    private readonly CarProfile _profile;
    private LiveryType _livery = LiveryType.ElectricBlue;
    private Color _carColor = Color.Red;
    private float fLeanAngle;
    private float fSmokeTimer;
    private float fFlameTimer;
    private float fDriftSparkTimer;
    private float fLaneVelocity;
    private float _trackCenter;
    private float rankHoldTimer;
    private string pendingRank = "C";
    private bool rankBurstPending;
    private bool wasBoosting;
    private bool nitroStartPending;

    public Vehicle(CarProfile? profile = null)
    {
        _profile = profile ?? CarProfile.Get(CarType.SpeedDemon);
        Nitro = 100f;
    }

    public void SetLivery(LiveryType livery, Color color) { _livery = livery; _carColor = color; }
    public void ResetLapFlags() => WallHitThisLap = false;

    public void ApplyPickup(PickupType type)
    {
        PickupsCollected++;
        switch (type)
        {
            case PickupType.Nitro: Nitro = Math.Min(100, Nitro + 40); ActiveBanner = "NITRO!"; break;
            case PickupType.SpeedBoost: Speed = Math.Min(_profile.NitroMaxSpeed, Speed + 0.3f); ActiveBanner = "BOOST!"; break;
            case PickupType.Shield: ShieldActive = true; ShieldTimer = 6f; ActiveBanner = "SHIELD!"; break;
            case PickupType.SlowTrap: SlowTimer = 2.5f; ActiveBanner = "SLOW!"; break;
            case PickupType.Magnet: MagnetTimer = 5f; ActiveBanner = "MAGNET!"; break;
        }
    }

    public void Update(bool up, bool down, bool left, bool right, bool boost, bool drifting,
        float trackCenterCurvature, float sectionCurvature, int screenWidth, int screenHeight,
        float elapsedTime, float totalTrackDistance, float gripMult = 1f)
    {
        var bounds = Track.ComputeLaneBounds(screenWidth, screenHeight, sectionCurvature);
        _trackCenter = trackCenterCurvature;
        CurrentSteerDir = left ? -1 : right ? 1 : 0;
        if (ShieldTimer > 0) { ShieldTimer -= elapsedTime; if (ShieldTimer <= 0) ShieldActive = false; }
        if (SlowTimer > 0) SlowTimer -= elapsedTime;

        if (MagnetTimer > 0)
        {
            MagnetTimer -= elapsedTime;
            PlayerCurvature += (trackCenterCurvature - PlayerCurvature) * elapsedTime * 3f;
            ActiveBanner = "MAGNET!";
        }
        else if (ActiveBanner == "MAGNET!") ActiveBanner = "";
        if (ShieldTimer <= 0 && ActiveBanner == "SHIELD!") ActiveBanner = "";

        IsDrifting = drifting && down && Speed > 0.15f && (left || right);
        if (IsDrifting)
        {
            DriftTimer += elapsedTime;
            fDriftSparkTimer += elapsedTime * 35f;
        }
        else
        {
            if (DriftTimer > 0.8f)
                Speed = Math.Min(_profile.NitroMaxSpeed, Speed + 0.15f * _profile.DriftBonus);
            DriftTimer = 0;
        }

        bool canBoost = boost && Nitro > 0;
        if (canBoost)
        {
            IsBoosting = true;
            Nitro -= _profile.NitroConsumption * elapsedTime;
            if (Nitro < 0) Nitro = 0;
            ActiveBanner = "NITRO BOOST!";
            NitroIntensity = Math.Min(1f, NitroIntensity + elapsedTime * 4f);
            NitroPhase += elapsedTime * 32f;
            if (Speed < 0.12f) Speed = 0.18f;
        }
        else
        {
            IsBoosting = false;
            NitroIntensity = Math.Max(0f, NitroIntensity - elapsedTime * 2.5f);
            if (ActiveBanner == "NITRO BOOST!") ActiveBanner = "";
            if (!IsDrifting && Nitro < 100) { Nitro += _profile.NitroRegen * elapsedTime; if (Nitro > 100) Nitro = 100; }
        }
        if (IsDrifting && !IsBoosting) ActiveBanner = "DRIFT!";

        float maxSpd = IsBoosting ? _profile.NitroMaxSpeed : _profile.MaxSpeed;
        if (SlowTimer > 0) maxSpd *= 0.6f;
        float accel = IsBoosting ? _profile.Acceleration * 2.2f : _profile.Acceleration;

        if (up || IsBoosting) Speed += accel * elapsedTime * gripMult * (IsDrifting ? 0.72f : 1f);
        else if (!IsDrifting) Speed -= 1.0f * elapsedTime;

        // 統一橫向物理：轉向力 + 離心力 + 彈簧回正 + 阻尼（不依路段切換靈敏度）
        float lane = PlayerCurvature - trackCenterCurvature;
        float input = right ? 1f : left ? -1f : 0f;
        float steerForce = input * 0.7f * (1f - Speed / 2f) * gripMult * 0.58f;
        if (IsDrifting) steerForce *= 1.8f;

        float centrifugal = sectionCurvature * Speed * Speed * 0.16f * gripMult;
        float spring = -lane * (2.2f + Speed * 1.8f);
        float damping = -fLaneVelocity * (IsDrifting ? 1.4f : 3.2f);

        fLaneVelocity += (steerForce + centrifugal + spring + damping) * elapsedTime;

        if (IsDrifting)
        {
            fLaneVelocity += DriftMomentum * Speed * 0.28f * elapsedTime;
            if (input != 0) DriftMomentum = Math.Clamp(DriftMomentum + input * 2.4f * elapsedTime, -1f, 1f);
            if (Math.Abs(sectionCurvature) > 0.25f)
                Speed = Math.Min(maxSpd, Speed + 0.22f * elapsedTime * gripMult);
        }
        else
            DriftMomentum *= Math.Max(0, 1f - elapsedTime * 5f);

        float maxLatVel = 0.42f + Speed * 0.38f;
        fLaneVelocity = Math.Clamp(fLaneVelocity, -maxLatVel, maxLatVel);

        lane += fLaneVelocity * elapsedTime;
        PlayerCurvature = trackCenterCurvature + lane;

        ApplyCurbCollision(ref lane, bounds, elapsedTime, IsDrifting);
        PlayerCurvature = trackCenterCurvature + lane;
        fLaneVelocity *= Math.Clamp(1f - elapsedTime * 0.5f, 0.7f, 1f);

        Speed = Math.Max(0, Math.Min(maxSpd, Speed));
        Distance += 70f * Speed * elapsedTime;
        if (Distance >= totalTrackDistance)
        {
            Distance -= totalTrackDistance;
            LapCount++;
        }

        float leanBase = IsDrifting ? 0.12f : 0.06f;
        float targetLean = CurrentSteerDir * leanBase + DriftMomentum * (IsDrifting ? 0.09f : 0.02f);
        fLeanAngle += (targetLean - fLeanAngle) * (IsDrifting ? 18f : 12f) * elapsedTime;
        fSmokeTimer += elapsedTime * (IsDrifting ? 40f : 25f);
        fFlameTimer += elapsedTime * 30f;

        UpdateRank(elapsedTime);
        if (RankFlashTimer > 0) RankFlashTimer -= elapsedTime;
        RankRingPhase += elapsedTime * (CurrentRank == "S" ? 9f : 5f);
        if (RankAnimScale > 1f) RankAnimScale -= elapsedTime * 1.6f;
        if (RankAnimScale < 1f) RankAnimScale = 1f;

        if (IsBoosting && !wasBoosting) nitroStartPending = true;
        wasBoosting = IsBoosting;
        WavSoundEngine.PlayEngine(Speed);
    }

    public bool TryConsumeNitroStart()
    {
        if (!nitroStartPending) return false;
        nitroStartPending = false;
        return true;
    }

    private void ApplyCurbCollision(ref float lane, Track.LaneBounds bounds, float dt, bool drifting)
    {
        if (ShieldActive) return;
        float bounce = drifting ? 8f : 12f;
        float drag = drifting ? 0.6f : 1.2f;

        if (lane > bounds.MaxLane)
        {
            float excess = lane - bounds.MaxLane;
            Speed = Math.Max(0, Speed - (drag + excess * 2.5f) * dt);
            lane -= excess * bounce * dt;
            fLaneVelocity = Math.Min(fLaneVelocity, 0) - excess * 2.5f;
            WallHitThisLap = true;
            if (excess > 0.008f) SoundManager.Play(Sfx.WallHit);
            if (lane > bounds.RumbleMaxLane) lane = bounds.RumbleMaxLane;
            if (drifting) DriftMomentum *= 0.5f;
        }
        else if (lane < bounds.MinLane)
        {
            float excess = bounds.MinLane - lane;
            Speed = Math.Max(0, Speed - (drag + excess * 2.5f) * dt);
            lane += excess * bounce * dt;
            fLaneVelocity = Math.Max(fLaneVelocity, 0) + excess * 2.5f;
            WallHitThisLap = true;
            if (excess > 0.008f) SoundManager.Play(Sfx.WallHit);
            if (lane < bounds.RumbleMinLane) lane = bounds.RumbleMinLane;
            if (drifting) DriftMomentum *= 0.5f;
        }
    }

    private void UpdateRank(float dt)
    {
        string newRank = "C";
        float lane = Math.Abs(PlayerCurvature);
        if (IsDrifting && DriftTimer > 0.3f) newRank = "B";
        if (IsDrifting && DriftTimer > 0.8f) newRank = "A";
        if (IsDrifting && DriftTimer > 1.5f && lane > 0.2f) newRank = "S";

        if (newRank != pendingRank)
        {
            int old = RankValue(pendingRank), nw = RankValue(newRank);
            if (nw > old)
            {
                RankAnimScale = newRank == "S" ? 2.8f : newRank == "A" ? 2.4f : 2.0f;
                RankFlashTimer = newRank == "S" ? 1.0f : 0.65f;
                rankBurstPending = true;
                SoundManager.Play(Sfx.RankUp);
                Combo++;
                if (Combo > ComboMax) ComboMax = Combo;
            }
            pendingRank = newRank;
            CurrentRank = newRank;
            rankHoldTimer = 1.8f;
        }
        else if (!IsDrifting) { rankHoldTimer -= dt; if (rankHoldTimer <= 0) { Combo = 0; pendingRank = "C"; CurrentRank = "C"; } }
    }

    private static int RankValue(string r) => r switch { "S" => 4, "A" => 3, "B" => 2, _ => 1 };

    public bool TryConsumeRankBurst()
    {
        if (!rankBurstPending) return false;
        rankBurstPending = false;
        return true;
    }

    public float GetLaneOffset() => PlayerCurvature - _trackCenter;
    public float GetLaneOffset(Track track) => GetLaneOffset();

    public void Render(Graphics g, int width, int height, Track track, float sectionCurvature, string name)
    {
        float lane = GetLaneOffset(track);
        RenderCar(g, width, height, sectionCurvature, lane, new SolidBrush(_carColor), fLeanAngle, CurrentSteerDir,
            name, 1.0f, height - Track.CarScreenOffsetY, IsBoosting, true);
    }

    public void RenderRemote(Graphics g, int width, int height, Track track, float sectionCurvature,
        float playerTrackCurvature, float remoteCurve, int remoteSteerDir, string name, float relDistance,
        bool remoteBoosting, Color color)
    {
        if (relDistance < -50f || relDistance > 200f) return;
        float perspective = Math.Clamp(1.0f - relDistance / 200f, 0.1f, 2.0f);
        int horizonY = height / 2;
        int carY = horizonY + (int)((height - horizonY - Track.CarScreenOffsetY) * perspective);
        float lane = remoteCurve - playerTrackCurvature;
        float remoteLean = remoteSteerDir * 0.06f;
        using var brush = new SolidBrush(color);
        RenderCar(g, width, height, sectionCurvature, lane, brush, remoteLean, remoteSteerDir,
            name, perspective, carY, remoteBoosting, false);
    }

    public void RenderGhost(Graphics g, int w, int h, Track track, float sectionCurvature, float playerTrackCurvature,
        GhostFrame frame)
    {
        float lane = Math.Abs(frame.LateralOffset) > 0.001f
            ? frame.LateralOffset
            : frame.Curvature - playerTrackCurvature;
        lane = Math.Clamp(lane, -0.5f, 0.5f);
        int carY = h - Track.CarScreenOffsetY;
        using var ghost = new SolidBrush(Color.FromArgb(90, 180, 220, 255));
        RenderCar(g, w, h, sectionCurvature, lane, ghost, 0, 0, "GHOST", 1f, carY, false, false);
    }

    private void RenderCar(Graphics g, int width, int height, float sectionCurvature, float laneOffset,
        Brush carBrush, float leanAngle, int steerDir, string name, float scale, int carY, bool isBoosting, bool isLocal)
    {
        var bounds = Track.ComputeLaneBounds(width, height, sectionCurvature, scale);
        float carPosRelative = Math.Clamp(laneOffset, bounds.RumbleMinLane, bounds.RumbleMaxLane);

        int carWidth = (int)(110 * scale);
        int carHeight = (int)(55 * scale);

        int halfH = height / 2;
        float perspective = Math.Clamp((carY + carHeight * 0.65f - halfH) / (float)halfH, 0f, 0.999f);
        float roadMidShift = sectionCurvature * MathF.Pow(1f - perspective, 3f);
        float normCenter = 0.5f + roadMidShift + carPosRelative * scale / 2f;
        int carX = (int)(normCenter * width) - carWidth / 2;

        if (Speed > 0.05f && ((int)fSmokeTimer % 2 == 0)) carY += 1;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        float fontSize = Math.Max(7f, 9f * scale);
        using (var nameFont = new Font("微軟正黑體", fontSize, FontStyle.Bold))
        {
            var nameSize = TextRenderer.MeasureText(name, nameFont);
            int labelX = carX + carWidth / 2 - nameSize.Width / 2;
            int labelY = carY - (int)(30 * scale);
            using var bgBrush = new SolidBrush(Color.FromArgb(120, Color.Black));
            {
                g.FillRectangle(bgBrush, labelX - 4, labelY - 2, nameSize.Width + 8, nameSize.Height + 4);
            }
            g.DrawRectangle(Pens.White, labelX - 4, labelY - 2, nameSize.Width + 8, nameSize.Height + 4);
            g.DrawString(name, nameFont, Brushes.White, labelX, labelY);
        }

        if (ShieldActive && isLocal)
            g.DrawEllipse(new Pen(Color.Cyan, 3 * scale), carX - 5, carY - 5, carWidth + 10, carHeight + 10);

        var originalMatrix = g.Transform;
        var leanMatrix = g.Transform;
        leanMatrix.RotateAt(leanAngle * (180f / (float)Math.PI), new PointF(carX + carWidth / 2f, carY + carHeight));
        g.Transform = leanMatrix;

        if (isLocal && _livery != LiveryType.Solid)
            CarLivery.Render(g, carX, carY, carWidth, carHeight, _livery, _carColor, scale);
        else
            g.FillRectangle(carBrush, carX, carY, carWidth, carHeight - (int)(15 * scale));

        g.FillRectangle(carBrush, carX + (int)(5 * scale), carY - (int)(15 * scale), carWidth - (int)(10 * scale), (int)(15 * scale));

        Point[] rearWindow =
        {
            new(carX + (int)(25 * scale), carY),
            new(carX + (int)(85 * scale), carY),
            new(carX + (int)(75 * scale), carY - (int)(12 * scale)),
            new(carX + (int)(35 * scale), carY - (int)(12 * scale))
        };
        g.FillPolygon(Brushes.Black, rearWindow);

        g.FillRectangle(Brushes.DarkRed, carX + (int)(10 * scale), carY + (int)(8 * scale), carWidth - (int)(20 * scale), (int)(14 * scale));
        g.FillEllipse(Brushes.Orange, carX + (int)(18 * scale), carY + (int)(12 * scale), (int)(6 * scale), (int)(6 * scale));
        g.FillEllipse(Brushes.Red, carX + (int)(26 * scale), carY + (int)(12 * scale), (int)(6 * scale), (int)(6 * scale));
        g.FillEllipse(Brushes.Red, carX + carWidth - (int)(32 * scale), carY + (int)(12 * scale), (int)(6 * scale), (int)(6 * scale));
        g.FillEllipse(Brushes.Orange, carX + carWidth - (int)(24 * scale), carY + (int)(12 * scale), (int)(6 * scale), (int)(6 * scale));

        g.FillRectangle(Brushes.DimGray, carX + (int)(8 * scale), carY + carHeight - (int)(18 * scale), (int)(16 * scale), (int)(18 * scale));
        g.FillRectangle(Brushes.DimGray, carX + carWidth - (int)(24 * scale), carY + carHeight - (int)(18 * scale), (int)(16 * scale), (int)(18 * scale));

        if (isBoosting) DrawNitroFlames(g, carX, carY, carWidth, carHeight, scale, isLocal);

        g.Transform = originalMatrix;

        if (IsDrifting)
            DrawDriftEffects(g, carX, carY, carWidth, carHeight, scale, steerDir, isLocal);

        if (isLocal && CurrentRank != "C" && RankFlashTimer > 0)
            DrawRankAura(g, carX, carY, carWidth, carHeight, scale);

        if (carBrush is SolidBrush sb) sb.Dispose();
    }

    private void DrawRankAura(Graphics g, int carX, int carY, int carW, int carH, float scale)
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(RankRingPhase * 2f);
        Color glow = CurrentRank switch
        {
            "S" => Color.FromArgb((int)(90 + 80 * pulse), 0, 255, 255),
            "A" => Color.FromArgb((int)(80 + 70 * pulse), 255, 215, 0),
            "B" => Color.FromArgb((int)(70 + 60 * pulse), 200, 220, 255),
            _ => Color.FromArgb(60, 180, 180, 180)
        };
        int pad = (int)((8 + pulse * 10) * scale);
        using var pen = new Pen(glow, 2.5f * scale);
        g.DrawEllipse(pen, carX - pad, carY - pad, carW + pad * 2, carH + pad * 2);
        if (CurrentRank == "S" && (int)(RankRingPhase * 3) % 2 == 0)
        {
            using var spark = new SolidBrush(Color.FromArgb(160, 255, 255, 255));
            g.FillEllipse(spark, carX + carW / 2 - 3, carY - 10 * scale, 6 * scale, 6 * scale);
        }
    }

    private void DrawDriftEffects(Graphics g, int carX, int carY, int carW, int carH, float scale, int steerDir, bool isLocal)
    {
        if (!IsDrifting && steerDir == 0) return;

        int smokeFrame = (int)fSmokeTimer % 4;
        int rightWheelX = carX + carW - (int)(24 * scale);
        int leftWheelX = carX + (int)(8 * scale);
        int smokeY = carY + carH - (int)(5 * scale);
        float intensity = Math.Clamp(Speed / _profile.MaxSpeed, 0.3f, 1.5f);

        for (int i = 0; i < (IsDrifting ? 4 : 1); i++)
        {
            int frame = (smokeFrame + i) % 4;
            int spread = frame * 4 + i * 6;
            using var smokeBrush = new SolidBrush(Color.FromArgb(IsDrifting ? 140 : 100, 200, 200, 200));
            using var hotBrush = new SolidBrush(Color.FromArgb(120, 255, 220, 100));

            if (steerDir == -1 || (IsDrifting && DriftMomentum < -0.1f))
            {
                int sx = rightWheelX + (int)(18 * scale) + spread;
                g.FillEllipse(smokeBrush, sx, smokeY - frame * 3, (8 + frame * 3) * scale, (6 + frame * 2) * scale);
                if (IsDrifting && frame % 2 == 0)
                    g.FillEllipse(hotBrush, sx + 2, smokeY - 2, 4 * scale, 3 * scale);
            }
            if (steerDir == 1 || (IsDrifting && DriftMomentum > 0.1f))
            {
                int sx = leftWheelX - (int)(12 * scale) - spread;
                g.FillEllipse(smokeBrush, sx, smokeY - frame * 3, (8 + frame * 3) * scale, (6 + frame * 2) * scale);
                if (IsDrifting && frame % 2 == 0)
                    g.FillEllipse(hotBrush, sx + 2, smokeY - 2, 4 * scale, 3 * scale);
            }
        }

        if (isLocal && IsDrifting && DriftTimer > 1.2f && (int)fDriftSparkTimer % 3 == 0)
        {
            using var spark = new SolidBrush(Color.FromArgb(200, 255, 255, 180));
            int sparkX = steerDir < 0 ? rightWheelX + 20 : leftWheelX - 8;
            g.FillEllipse(spark, sparkX, smokeY - 8, 5 * scale * intensity, 4 * scale);
        }
    }

    private void DrawNitroFlames(Graphics g, int carX, int carY, int carW, int carH, float scale, bool isLocal)
    {
        int frame = (int)NitroPhase % 4;
        float power = NitroIntensity;
        int outerLength = (int)((35 + frame * 14) * scale * (0.7f + power * 0.5f));
        int midLength = (int)((22 + frame * 9) * scale * power);
        int innerLength = (int)((12 + frame * 6) * scale);
        int flameWidth = (int)((16 + frame * 2) * scale);
        int exhaustY = carY + carH - (int)(12 * scale);

        foreach (int ex in new[] { carX + (int)(28 * scale), carX + carW - (int)(28 * scale) })
        {
            using var glow = new SolidBrush(Color.FromArgb((int)(60 * power), 0, 180, 255));
            g.FillEllipse(glow, ex - flameWidth, exhaustY - 4, flameWidth * 2, outerLength + 12);

            Brush outer = isLocal ? Brushes.DeepSkyBlue : Brushes.OrangeRed;
            Brush mid = isLocal ? Brushes.Cyan : Brushes.Orange;
            Brush inner = Brushes.White;

            g.FillPolygon(outer, new[]
            {
                new Point(ex - flameWidth / 2, exhaustY),
                new Point(ex + flameWidth / 2, exhaustY),
                new Point(ex + frame - 2, exhaustY + outerLength)
            });
            g.FillPolygon(mid, new[]
            {
                new Point(ex - flameWidth / 3, exhaustY),
                new Point(ex + flameWidth / 3, exhaustY),
                new Point(ex, exhaustY + midLength)
            });
            g.FillPolygon(inner, new[]
            {
                new Point(ex - flameWidth / 5, exhaustY),
                new Point(ex + flameWidth / 5, exhaustY),
                new Point(ex, exhaustY + innerLength)
            });
        }

        if (isLocal && power > 0.3f)
        {
            using var streak = new Pen(Color.FromArgb((int)(100 * power), 180, 240, 255), 2f * scale);
            for (int i = 0; i < 3; i++)
            {
                int sy = carY + carH / 2 + i * 8;
                g.DrawLine(streak, carX - 20 - frame * 4, sy, carX + carW + 20 + frame * 4, sy);
            }
        }
    }
}
