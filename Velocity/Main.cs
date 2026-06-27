using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Pseudo3DRacer;

public partial class MainForm : Form
{
    private readonly Track track = new();
    private Vehicle vehicle = new(CarProfile.Get(CarType.SpeedDemon));
    private readonly TrackCurvatureController curvature = new();
    private readonly MultiplayerSession net = new();
    private System.Windows.Forms.Timer gameTimer = null!;
    private DateTime lastTime;
    private Bitmap? frameBuffer;

    private bool keyUp, keyDown, keyLeft, keyRight, keySpace;
    private float currentLapTime, totalRaceTime, countdownTimer = 3f;
    private int targetLaps = 3;
    private readonly List<float> lapTimes = new();
    private float bestLapTime = float.MaxValue;

    private GameState currentState = GameState.MainMenu;
    private WeatherMode weather = WeatherMode.Sunny;
    private CarProfile carProfile = CarProfile.Get(CarType.SpeedDemon);
    private LiveryType livery = LiveryType.Solid;
    private int aiCount;
    private bool enableGhost, enableSound = true;
    private readonly List<AiBot> aiBots = new();

    private string localPlayerId = "Player";
    private GameSaveData saveData = GameSaveData.Load();
    private SectorTimer sectorTimer = null!;
    private GhostRecorder ghostRecorder = new();
    private ReplayRecorder replayRecorder = new();
    private ParticleSystem particles = new();
    private DailyChallenge dailyChallenge = DailyChallenge.GetToday();

    private PictureBox picCanvas = null!;
    private Panel menuPanel = null!;
    private Panel menuScroll = null!;
    private ComboBox cmbMap = null!, cmbCar = null!, cmbWeather = null!, cmbLivery = null!, cmbLaps = null!, cmbAi = null!;
    private CheckBox chkNet = null!, chkHost = null!, chkGhost = null!, chkSound = null!;
    private TextBox txtId = null!, txtIp = null!;
    private Button btnStart = null!;
    private Label lblDaily = null!, lblAchieve = null!, lblRoom = null!, lblFooter = null!;
    private string toastMsg = "";
    private float toastTimer;
    private int finishPosition;
    private readonly List<string> newAchievements = new();
    private bool replayMode;
    private int replayIndex;
    private float replayTimer;

    public MainForm()
    {
        Text = "Turbo Velocity - Ultimate Edition";
        ClientSize = new Size(800, 500);
        KeyPreview = true;
        DoubleBuffered = true;

        sectorTimer = new SectorTimer(track.TotalDistance);
        saveData.AutoUnlock();
        RefreshLiveryCombo();
        vehicle.SetLivery(LiveryType.ElectricBlue, Color.Red);

        net.RaceSyncReceived += OnRaceSync;
        net.RemotePlayerJoined += () => net.BroadcastRaceSync(track.MapIndex, (int)weather, targetLaps, countdownTimer);

        picCanvas = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, TabStop = true };
        Controls.Add(picCanvas);
        picCanvas.KeyDown += (s, e) => { HandleKey(e.KeyCode, true); if (e.KeyCode == Keys.Space) e.SuppressKeyPress = true; };
        picCanvas.KeyUp += (s, e) => HandleKey(e.KeyCode, false);
        BuildMenu();
        picCanvas.SendToBack();

        KeyDown += (s, e) => { HandleKey(e.KeyCode, true); if (e.KeyCode == Keys.Space) e.SuppressKeyPress = true; };
        KeyUp += (s, e) => HandleKey(e.KeyCode, false);

        gameTimer = new System.Windows.Forms.Timer { Interval = 16 };
        gameTimer.Tick += GameLoop;
        lastTime = DateTime.Now;
        gameTimer.Start();
    }

    private void BuildMenu()
    {
        menuPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 268,
            BackColor = Color.FromArgb(215, 55, 58, 68),
            AutoScroll = true
        };
        menuScroll = new Panel { Size = new Size(252, 680), Location = new Point(6, 6), BackColor = Color.Transparent };
        int y = 4;

        void AddRow(string t, Control c, ref int top)
        {
            menuScroll.Controls.Add(new Label { Text = t, ForeColor = Color.Gainsboro, Font = new Font("微軟正黑體", 9), Left = 2, Top = top + 4, Width = 48 });
            c.Left = 54; c.Top = top; c.Width = 188; c.Font = new Font("微軟正黑體", 9);
            menuScroll.Controls.Add(c); top += 27;
        }

        menuScroll.Controls.Add(new Label
        {
            Text = "TURBO VELOCITY",
            ForeColor = Color.Gold,
            Font = new Font("Impact", 16, FontStyle.Italic | FontStyle.Bold),
            Left = 6, Top = y, Width = 240, Height = 24
        });
        y += 26;
        menuScroll.Controls.Add(new Label
        {
            Text = "Ultimate Edition",
            ForeColor = Color.Orange,
            Font = new Font("Impact", 10, FontStyle.Italic),
            Left = 10, Top = y, Width = 200, Height = 18
        });
        y += 24;

        txtId = new TextBox { Text = "Player" };
        AddRow("名字:", txtId, ref y);
        cmbMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbMap.Items.AddRange(new object[] { "1. 經典新手村", "2. 極速沙漠高架", "3. 秋名山地獄" });
        cmbMap.SelectedIndex = 2;
        track.SetMap(2);
        cmbMap.SelectedIndexChanged += (_, _) => { track.SetMap(cmbMap.SelectedIndex); sectorTimer = new SectorTimer(track.TotalDistance); };
        AddRow("賽道:", cmbMap, ref y);
        cmbCar = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbCar.Items.AddRange(new object[] { "極速蜂 (速度)", "甩尾王 (甩尾)", "氮氣狂 (氮氣)" });
        cmbCar.SelectedIndex = 0;
        AddRow("車輛:", cmbCar, ref y);
        cmbLaps = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbLaps.Items.AddRange(new object[] { "3 圈", "5 圈" });
        cmbLaps.SelectedIndex = 0;
        AddRow("圈數:", cmbLaps, ref y);
        cmbWeather = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbWeather.Items.AddRange(new object[] { "晴天", "雨天", "夜晚" });
        cmbWeather.SelectedIndex = 0;
        AddRow("天氣:", cmbWeather, ref y);
        cmbLivery = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        AddRow("塗裝:", cmbLivery, ref y);
        cmbLivery.SelectedIndexChanged += (_, _) => ApplyPreviewLivery();
        cmbAi = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbAi.Items.AddRange(new object[] { "0", "1", "2", "3", "4" });
        cmbAi.SelectedIndex = 2;
        AddRow("AI 數:", cmbAi, ref y);

        chkGhost = new CheckBox { Text = "倒車", ForeColor = Color.White, Width = 70, Checked = true };
        chkSound = new CheckBox { Text = "音效", ForeColor = Color.White, Checked = true, Width = 70 };
        chkGhost.Left = 58; chkGhost.Top = y;
        chkSound.Left = 140; chkSound.Top = y;
        menuScroll.Controls.Add(chkGhost);
        menuScroll.Controls.Add(chkSound);
        y += 28;

        chkHost = new CheckBox { Text = "擔任連線主機", ForeColor = Color.Lime, Width = 120 };
        chkNet = new CheckBox { Text = "多人連線", ForeColor = Color.Lime, Width = 90 };
        chkHost.Left = 58; chkHost.Top = y;
        chkNet.Left = 165; chkNet.Top = y;
        chkHost.CheckedChanged += (_, _) => { UpdateRoomCode(); UpdateNetUi(); };
        chkNet.CheckedChanged += (_, _) => UpdateNetUi();
        menuScroll.Controls.Add(chkHost);
        menuScroll.Controls.Add(chkNet);
        y += 26;
        txtIp = new TextBox { Text = "127.0.0.1", Enabled = false, Left = 58, Top = y, Width = 195 };
        menuScroll.Controls.Add(txtIp);
        y += 26;
        lblRoom = new Label { ForeColor = Color.Yellow, Left = 58, Top = y, Width = 195, Text = "房間碼: ----" };
        menuScroll.Controls.Add(lblRoom);
        y += 24;

        lblDaily = new Label { ForeColor = Color.Gold, Font = new Font("微軟正黑體", 8), Left = 6, Top = y, Width = 255, Height = 44 };
        menuScroll.Controls.Add(lblDaily); y += 48;

        btnStart = new Button
        {
            Text = "START RACE!", BackColor = Color.OrangeRed, ForeColor = Color.White,
            Font = new Font("Impact", 14), Left = 30, Top = y, Width = 210, Height = 40
        };
        btnStart.Click += (_, _) => StartRace();
        menuScroll.Controls.Add(btnStart);
        y += 48;

        lblAchieve = new Label { ForeColor = Color.LightGreen, Font = new Font("微軟正黑體", 8), Left = 6, Top = y, Width = 255, Height = 36 };
        menuScroll.Controls.Add(lblAchieve); y += 36;
        lblFooter = new Label { ForeColor = Color.Gray, Font = new Font("微軟正黑體", 7), Left = 6, Top = y, Width = 255, Height = 30 };
        menuScroll.Controls.Add(lblFooter);

        menuPanel.Controls.Add(menuScroll);
        Controls.Add(menuPanel);
        UpdateDailyLabel();
        UpdateAchieveLabel();
        UpdateRoomCode();
    }

    private void ApplyPreviewLivery()
    {
        livery = LiveryCatalog.Parse(cmbLivery.SelectedItem?.ToString());
        vehicle.SetLivery(livery, Color.Red);
    }

    private void RefreshLiveryCombo()
    {
        if (cmbLivery == null) return;
        var sel = cmbLivery.SelectedItem?.ToString();
        cmbLivery.Items.Clear();
        foreach (var d in LiveryCatalog.UnlockedDisplays(saveData.UnlockedLiveries))
            cmbLivery.Items.Add(d);
        if (cmbLivery.Items.Count > 0)
        {
            int idx = 0;
            if (sel != null) for (int i = 0; i < cmbLivery.Items.Count; i++)
                    if (cmbLivery.Items[i]?.ToString() == sel) { idx = i; break; }
            cmbLivery.SelectedIndex = idx;
        }
        for (int i = 0; i < cmbLivery.Items.Count; i++)
            if (cmbLivery.Items[i]?.ToString()?.Contains("電光藍") == true) { cmbLivery.SelectedIndex = i; break; }
        ApplyPreviewLivery();
    }

    private void UpdateNetUi()
    {
        if (chkNet == null || txtIp == null) return;
        txtIp.Enabled = chkNet.Checked;
        if (chkNet.Checked && chkHost.Checked)
            txtIp.Text = "127.0.0.1";
        UpdateRoomCode();
    }

    private void UpdateRoomCode()
    {
        if (chkHost == null || lblRoom == null) return;
        if (!chkNet.Checked)
            lblRoom.Text = "房間碼: ----";
        else if (chkHost.Checked)
            lblRoom.Text = $"主機 {NetworkUtil.GetLanIPv4()}:{NetProtocol.DefaultPort}";
        else
            lblRoom.Text = "連線至主機 IP ↑";
    }

    private void UpdateDailyLabel()
    {
        string status = saveData.LastChallengeDate == DateTime.Today.ToString("yyyy-MM-dd") && saveData.LastChallengeCompleted ? "已完成" : "進行中";
        string reward = dailyChallenge.RewardLivery switch
        {
            "RacingStripe" => "賽道條紋",
            "Flames" => "烈焰",
            "Carbon" => "碳纖維",
            "Neon" => "霓虹",
            "Checker" => "棋盤格",
            _ => dailyChallenge.RewardLivery
        };
        lblDaily.Text = $"【每日】{dailyChallenge.Title}：{dailyChallenge.Description} -> 獎勵：{reward} ({status})";
    }

    private void UpdateAchieveLabel()
    {
        string bestNote = saveData.BestLapTime < float.MaxValue ? "最佳圈速已記錄" : "尚無紀錄";
        lblAchieve.Text = $"成就 {saveData.UnlockedAchievements.Count}/12 | 勝場 {saveData.TotalWins} | {bestNote}";
        lblFooter.Text = chkNet.Checked
            ? "多人連線: 一台擔任主機，其他玩家輸入主機 IP"
            : "Sector 1/3 | 每日任務 | WAV 音效";
    }

    private void StartRace()
    {
        localPlayerId = string.IsNullOrWhiteSpace(txtId.Text) ? "Player" : txtId.Text.Trim();
        targetLaps = cmbLaps.SelectedIndex == 0 ? 3 : 5;
        var carType = (CarType)cmbCar.SelectedIndex;
        carProfile = CarProfile.Get(carType);
        weather = (WeatherMode)cmbWeather.SelectedIndex;
        enableGhost = chkGhost.Checked;
        enableSound = chkSound.Checked;
        WavSoundEngine.Enabled = enableSound;
        aiCount = chkNet.Checked ? 0 : cmbAi.SelectedIndex;

        livery = LiveryCatalog.Parse(cmbLivery.SelectedItem?.ToString());
        track.SetMap(cmbMap.SelectedIndex);
        sectorTimer = new SectorTimer(track.TotalDistance);
        curvature.Reset();
        vehicle = new Vehicle(carProfile);
        vehicle.PlayerCurvature = track.GetAccumulatedCurvature(0);
        vehicle.SetLivery(livery, Color.Red);
        aiBots.Clear();
        if (!chkNet.Checked && aiCount > 0)
        {
            for (int i = 0; i < aiCount && i < AiRoster.Bots.Length; i++)
                aiBots.Add(new AiBot(AiRoster.Bots[i], vehicle.Distance + 20f + i * 14f, vehicle.Distance, track));
        }

        net.Dispose();
        if (chkNet.Checked && chkHost.Checked)
            net.StartHostRelay();

        if (chkNet.Checked)
        {
            try
            {
                net.LocalPlayerId = localPlayerId;
                net.Connect(chkHost.Checked, txtIp.Text, track.MapIndex, (int)weather, targetLaps, cmbCar.SelectedIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "多人連線", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        ghostRecorder.StartLap();
        replayRecorder.StartLap();
        lapTimes.Clear();
        currentLapTime = totalRaceTime = 0;
        vehicle.LapCount = 0;
        vehicle.ResetLapFlags();
        particles.Clear();
        menuPanel.Visible = false;
        picCanvas.Focus();
        ActiveControl = picCanvas;
        countdownTimer = 3f;
        currentState = GameState.Countdown;
        lastTime = DateTime.Now;
        frameBuffer?.Dispose();
        frameBuffer = new Bitmap(picCanvas.Width > 0 ? picCanvas.Width : 800, picCanvas.Height > 0 ? picCanvas.Height : 600);
    }

    private void OnRaceSync(int mapIndex, int weatherIdx, int laps, float hostCountdown)
    {
        if (net.RaceSynced && Math.Abs(countdownTimer - hostCountdown) < 0.3f) return;
        track.SetMap(mapIndex);
        sectorTimer = new SectorTimer(track.TotalDistance);
        weather = (WeatherMode)Math.Clamp(weatherIdx, 0, 2);
        targetLaps = laps is 3 or 5 ? laps : 3;
        if (currentState is GameState.Countdown or GameState.Playing)
            countdownTimer = Math.Max(countdownTimer, hostCountdown);
        net.RaceSynced = true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (currentState == GameState.Playing && (keyData & Keys.KeyCode) == Keys.Space)
        {
            HandleKey(Keys.Space, true);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void HandleKey(Keys k, bool down)
    {
        if (k == Keys.Escape)
        {
            if (currentState == GameState.Playing) currentState = GameState.Paused;
            else if (currentState == GameState.Paused) currentState = GameState.Playing;
            return;
        }
        if (currentState != GameState.Playing) return;
        if (k == Keys.Up) keyUp = down;
        if (k == Keys.Down) keyDown = down;
        if (k == Keys.Left) keyLeft = down;
        if (k == Keys.Right) keyRight = down;
        if (k == Keys.Space) keySpace = down;
    }

    private void GameLoop(object? sender, EventArgs e)
    {
        float dt = (float)(DateTime.Now - lastTime).TotalSeconds;
        lastTime = DateTime.Now;
        if (dt > 0.1f) dt = 0.1f;
        if (toastTimer > 0) toastTimer -= dt;

        if (net.Enabled && currentState is GameState.Countdown or GameState.Playing)
            net.Tick(dt, vehicle, currentState, countdownTimer, track.MapIndex, (int)weather, targetLaps);

        if (picCanvas.Width < 100 || picCanvas.Height < 100) return;
        if (frameBuffer == null || frameBuffer.Width != picCanvas.Width || frameBuffer.Height != picCanvas.Height)
        { frameBuffer?.Dispose(); frameBuffer = new Bitmap(picCanvas.Width, picCanvas.Height); }

        switch (currentState)
        {
            case GameState.MainMenu: RenderMenuPreview(); break;
            case GameState.Countdown: UpdateCountdown(dt); break;
            case GameState.Playing: UpdatePlaying(dt); break;
            case GameState.Paused: RenderPaused(); break;
            case GameState.Finished: RenderFinished(); break;
        }
    }

    private void RenderMenuPreview()
    {
        int w = frameBuffer!.Width, h = frameBuffer.Height;
        weather = (WeatherMode)cmbWeather.SelectedIndex;
        track.SetMap(cmbMap.SelectedIndex);
        ApplyPreviewLivery();
        track.RenderFrame(frameBuffer, 0, 0, 0, 0, weather);
        using var g = Graphics.FromImage(frameBuffer);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        string previewName = string.IsNullOrWhiteSpace(txtId.Text) ? "Player1" : txtId.Text.Trim();
        vehicle.Render(g, w, h, track, 0, previewName);

        using var ready = new Font("Impact", 20, FontStyle.Bold | FontStyle.Italic);
        var rs = g.MeasureString("READY TO RACE", ready);
        g.DrawString("READY TO RACE", ready, Brushes.Gold, w / 2f - rs.Width / 2, h - 52);
        PresentFrame();
    }

    private void UpdateCountdown(float dt)
    {
        if (!net.IsHost && net.Enabled && !net.RaceSynced)
        {
            using var gWait = Graphics.FromImage(frameBuffer!);
            CountdownRenderer.DrawWaitingHost(gWait, net.Status);
            PresentFrame();
            return;
        }

        countdownTimer -= dt;
        int w = frameBuffer!.Width, h = frameBuffer.Height;
        track.RenderFrame(frameBuffer, vehicle.Distance, curvature.Track, curvature.Render, 0, weather);
        using var g = Graphics.FromImage(frameBuffer);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        CountdownRenderer.DrawStartGrid(g, w, h, curvature.Render);
        track.RenderGoSigns(g, w, h, curvature.Render);
        CountdownRenderer.DrawLineup(g, w, h, track, vehicle, localPlayerId, curvature.Render, curvature.Track,
            aiBots, net.RemotePlayers.Values, d => RaceSceneRenderer.WrapDistance(d, track.TotalDistance));
        CountdownRenderer.DrawNumber(g, w, h, countdownTimer);

        if (countdownTimer <= 0)
        {
            currentState = GameState.Playing;
            if (countdownTimer > -0.1f) SoundManager.Play(Sfx.Go);
        }
        PresentFrame();
    }

    private void UpdatePlaying(float dt)
    {
        if (replayMode) { UpdateReplay(dt); return; }

        net.PruneStalePlayers();
        curvature.Update(track, vehicle.Distance, vehicle.Speed, dt);

        float grip = WeatherSystem.GripMultiplier(weather);
        int lapsBefore = vehicle.LapCount;
        vehicle.Update(keyUp, keyDown, keyLeft, keyRight, keySpace, keyDown, curvature.Track, curvature.Render,
            frameBuffer!.Width, frameBuffer.Height, dt, track.TotalDistance, grip);

        if (vehicle.LapCount > lapsBefore)
        {
            lapTimes.Add(currentLapTime);
            ghostRecorder.FinishLap(currentLapTime);
            replayRecorder.Stop();
            sectorTimer.OnNewLap();
            if (currentLapTime < bestLapTime) bestLapTime = currentLapTime;
            if (currentLapTime < saveData.BestLapTime) saveData.BestLapTime = currentLapTime;
            if (track.MapIndex == 2 && currentLapTime < saveData.BestLapMap2) saveData.BestLapMap2 = currentLapTime;
            currentLapTime = 0;
            vehicle.ResetLapFlags();
            ghostRecorder.StartLap();
            replayRecorder.StartLap();

            if (vehicle.LapCount >= targetLaps) { FinishRace(); return; }
        }

        currentLapTime += dt;
        totalRaceTime += dt;
        sectorTimer.Update(currentLapTime, vehicle.Distance, track.TotalDistance, saveData, track.MapIndex);
        ghostRecorder.Record(vehicle.Distance, vehicle.PlayerCurvature, vehicle.GetLaneOffset());
        replayRecorder.Record(vehicle.Distance, vehicle.PlayerCurvature, vehicle.LateralOffset, keyUp, keyLeft, keyRight, keySpace);

        var pickup = track.CheckPickup(vehicle.Distance, vehicle.PlayerCurvature - curvature.Track);
        if (pickup != null) { vehicle.ApplyPickup(pickup.Type); SoundManager.Play(Sfx.PickUp); }

        foreach (var bot in aiBots)
            bot.Update(track, curvature.Track, curvature.Render, frameBuffer!.Width, frameBuffer.Height, dt, track.TotalDistance, weather);
        EmitParticles(dt);
        RenderRaceScene();
    }

    private void EmitParticles(float dt)
    {
        particles.Update(dt);
        int pw = frameBuffer!.Width, ph = frameBuffer.Height;
        float lane = vehicle.GetLaneOffset();
        int carCenterX = pw / 2 + (int)(pw * lane / 2f);
        int carRearY = ph - 72;
        if (vehicle.IsDrifting && vehicle.Speed > 0.1f)
        {
            int steer = vehicle.CurrentSteerDir != 0 ? vehicle.CurrentSteerDir : vehicle.DriftMomentum < 0 ? -1 : 1;
            float wx = steer < 0 ? carCenterX + 38 : carCenterX - 38;
            particles.EmitDrift(wx, carRearY, steer, vehicle.Speed, 1.1f);
        }
        if (vehicle.IsBoosting)
            particles.EmitNitroTrail(carCenterX, ph - 48, vehicle.NitroIntensity);
        if (vehicle.TryConsumeNitroStart())
            particles.EmitNitroBurst(carCenterX, ph - 60);
        if (vehicle.TryConsumeRankBurst())
            particles.EmitRankUp(pw / 2f, ph * 0.38f, vehicle.CurrentRank, vehicle.Combo);
    }

    private void FinishRace()
    {
        currentState = GameState.Finished;
        saveData.TotalRaces++;
        var all = LeaderboardBuilder.Build(localPlayerId, vehicle, net.RemotePlayers.Values, aiBots);
        finishPosition = all.FindIndex(p => p.IsLocal) + 1;
        if (finishPosition == 1) { saveData.TotalWins++; saveData.WinStreak++; } else saveData.WinStreak = 0;
        if (vehicle.ComboMax >= 5 || vehicle.CurrentRank == "S") saveData.SRankCount++;

        bool beatGhost = enableGhost && ghostRecorder.HasGhost;
        var result = new RaceResult
        {
            Position = finishPosition, TotalRacers = all.Count,
            LapTime = lapTimes.Count > 0 ? lapTimes.Min() : currentLapTime,
            MapIndex = track.MapIndex, HighestRank = vehicle.CurrentRank,
            ComboMax = vehicle.ComboMax, PickupsCollected = vehicle.PickupsCollected,
            NoWallHit = !vehicle.WallHitThisLap, BeatGhost = beatGhost
        };
        newAchievements.Clear();
        newAchievements.AddRange(AchievementTracker.Evaluate(result, saveData));

        string today = DateTime.Today.ToString("yyyy-MM-dd");
        if (saveData.LastChallengeDate != today || !saveData.LastChallengeCompleted)
        {
            if (dailyChallenge.IsCompleted(result))
            {
                saveData.LastChallengeDate = today;
                saveData.LastChallengeCompleted = true;
                if (!saveData.UnlockedLiveries.Contains(dailyChallenge.RewardLivery))
                    saveData.UnlockedLiveries.Add(dailyChallenge.RewardLivery);
                newAchievements.Add("每日挑戰完成!");
                SoundManager.Play(Sfx.DailyComplete);
            }
        }
        saveData.AutoUnlock();
        saveData.Save();
        SoundManager.Play(Sfx.Finish);
        foreach (var _ in newAchievements) SoundManager.Play(Sfx.Achievement);
    }

    private RaceSceneContext BuildSceneContext() => new()
    {
        FrameBuffer = frameBuffer!,
        Track = track,
        Player = vehicle,
        PlayerName = localPlayerId,
        Curvature = curvature,
        Weather = weather,
        Particles = particles,
        Ghost = ghostRecorder,
        SectorTimer = sectorTimer,
        SaveData = saveData,
        RemotePlayers = net.RemotePlayers.Values,
        AiBots = aiBots,
        EnableGhost = enableGhost,
        TargetLaps = targetLaps,
        CurrentLapTime = currentLapTime,
        BestLapTime = bestLapTime,
        TotalRaceTime = totalRaceTime,
        CarTag = carProfile.HudTag,
        NetEnabled = net.Enabled,
        NetStatus = net.Status,
        BottomBanner = vehicle.ActiveBanner,
        ToastMessage = toastMsg,
        ToastTimer = toastTimer
    };

    private void RenderRaceScene()
    {
        RaceSceneRenderer.Draw(BuildSceneContext());
        PresentFrame();
    }

    private void PresentFrame()
    {
        picCanvas.Image?.Dispose();
        picCanvas.Image = (Bitmap)frameBuffer!.Clone();
    }

    private void RenderPaused()
    {
        RenderRaceScene();
        using var g = Graphics.FromImage(frameBuffer!);
        using var veil = new SolidBrush(Color.FromArgb(150, Color.Black));
        g.FillRectangle(veil, 0, 0, frameBuffer!.Width, frameBuffer.Height);
        using var f = new Font("微軟正黑體", 24, FontStyle.Bold);
        g.DrawString("PAUSED", f, Brushes.White, frameBuffer.Width / 2 - 80, frameBuffer.Height / 2 - 60);
        g.DrawString("ESC: 繼續", f, Brushes.Lime, frameBuffer.Width / 2 - 80, frameBuffer.Height / 2);
        PresentFrame();
    }

    private void RenderFinished()
    {
        RenderRaceScene();
        using var g = Graphics.FromImage(frameBuffer!);
        int w = frameBuffer!.Width, h = frameBuffer.Height;
        float bestLap = lapTimes.Count > 0 ? lapTimes.Min() : bestLapTime;
        FinishScreenRenderer.Draw(g, new FinishScreenViewModel
        {
            FinishPosition = finishPosition,
            TotalRaceTime = totalRaceTime,
            BestLap = bestLap,
            ComboMax = vehicle.ComboMax,
            PickupsCollected = vehicle.PickupsCollected,
            NewAchievements = newAchievements,
            HasReplay = replayRecorder.HasReplay
        }, w, h, out var btnReplay, out var btnAgain, out var btnMenu);
        PresentFrame();

        void OnClick(object? s, MouseEventArgs e)
        {
            if (btnAgain.Contains(e.Location)) { replayMode = false; StartRace(); }
            else if (btnMenu.Contains(e.Location)) ReturnToMenu();
            else if (replayRecorder.HasReplay && btnReplay.Contains(e.Location))
            { replayMode = true; replayIndex = 0; replayTimer = 0; currentState = GameState.Playing; menuPanel.Visible = false; }
        }
        picCanvas.MouseClick -= OnClick;
        picCanvas.MouseClick += OnClick;
    }

    private void UpdateReplay(float dt)
    {
        replayTimer += dt;
        if (replayTimer > 0.032f && replayIndex < replayRecorder.Frames.Count)
        {
            var f = replayRecorder.Frames[replayIndex++];
            vehicle.Distance = f.Distance;
            vehicle.PlayerCurvature = f.Curvature;
            vehicle.LateralOffset = f.LateralOffset;
            replayTimer = 0;
        }
        if (replayIndex >= replayRecorder.Frames.Count)
        { replayMode = false; currentState = GameState.Finished; RenderFinished(); return; }
        RenderRaceScene();
    }

    private void ReturnToMenu()
    {
        net.Dispose();
        replayMode = false;
        currentState = GameState.MainMenu;
        menuPanel.Visible = true;
        RefreshLiveryCombo();
        UpdateDailyLabel();
        UpdateAchieveLabel();
        UpdateNetUi();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        net.Dispose();
        frameBuffer?.Dispose();
        base.OnFormClosing(e);
    }
}
