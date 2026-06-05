using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Pseudo3DRacer
{
    public enum GameState { MainMenu, Playing }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public partial class MainForm : Form
    {
        private Track track;
        private Vehicle vehicle;
        private System.Windows.Forms.Timer gameTimer;
        private DateTime lastTime;

        private bool keyUp = false;
        private bool keyLeft = false;
        private bool keyRight = false;

        private float fCurvature = 0.0f;
        private float fTrackCurvature = 0.0f;
        private float currentLapTime = 0.0f;
        private List<float> lapTimes = new List<float> { 0, 0, 0, 0, 0 };
        private PictureBox picCanvas;

        private GameState currentState = GameState.MainMenu;

        private NetworkManager netManager = new NetworkManager();
        private bool isHostMode;

        private Panel menuPanel = null!;
        private ComboBox cmbMapSelect = null!;
        private Button btnSinglePlayer = null!;
        private Button btnCreateRoom = null!;
        private Button btnJoinRoom = null!;

        private TextBox txtPlayerName = null!;

        private Panel lobbyPanel = null!;
        private Label[] lblPlayerSlots = new Label[4];
        private Label lblLobbyStatus = null!;
        private Label lblLocalIP = null!;
        private Label lblMyName = null!;
        private Button btnLobbyStart = null!;
        private Button btnLobbyLeave = null!;
        private TextBox txtLobbyPort = null!;
        private TextBox txtHostIp = null!;
        private Button btnLobbyConnect = null!;
        private Button btnSearchRooms = null!;
        private ListBox lstRooms = null!;

        private int lastAskedCount = 1;

        private static readonly string[] DefaultNames = { "拓海", "涼介", "啓介", "中里", "京一", "小柏", "岩城" };
        private static readonly Random rng = new Random();

        public MainForm()
        {
            this.Width = 640;
            this.Height = 500;
            this.Text = "Turbo Velocity - Custom Art Edition";
            this.KeyPreview = true;

            track = new Track();
            vehicle = new Vehicle();

            string defaultName = DefaultNames[rng.Next(DefaultNames.Length)];

            BuildMenuUI(defaultName);
            BuildLobbyUI();

            picCanvas = new PictureBox();
            picCanvas.Dock = DockStyle.Fill;
            this.Controls.Add(picCanvas);

            picCanvas.BringToFront();
            menuPanel.BringToFront();

            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;

            gameTimer = new System.Windows.Forms.Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;

            lastTime = DateTime.Now;
            gameTimer.Start();
        }

        private void BuildMenuUI(string defaultName)
        {
            menuPanel = new Panel()
            {
                Size = new Size(360, 320),
                Location = new Point((640 - 360) / 2, (480 - 320) / 2 - 20),
                BackColor = Color.FromArgb(230, Color.DarkSlateGray),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTitle = new Label()
            { Text = "TURBO VELOCITY", Font = new Font("Impact", 22, FontStyle.Italic),
              ForeColor = Color.Yellow, Left = 20, Top = 12, Width = 320,
              TextAlign = ContentAlignment.MiddleCenter };

            Label lblMap = new Label()
            { Text = "賽道:", ForeColor = Color.White,
              Font = new Font("微軟正黑體", 10, FontStyle.Bold), Left = 30, Top = 60, Width = 50 };
            cmbMapSelect = new ComboBox()
            { Left = 90, Top = 58, Width = 230,
              DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMapSelect.Items.AddRange(new string[] { "1. 經典新手村", "2. 極速沙漠高架", "3. 秋名山地獄" });
            cmbMapSelect.SelectedIndex = 0;
            cmbMapSelect.SelectedIndexChanged += (s, e) => track.SetMap(cmbMapSelect.SelectedIndex);

            Label lblName = new Label()
            { Text = "暱稱:", ForeColor = Color.White,
              Font = new Font("微軟正黑體", 10, FontStyle.Bold), Left = 30, Top = 95, Width = 50 };
            txtPlayerName = new TextBox()
            { Text = defaultName, Left = 90, Top = 93, Width = 230,
              Font = new Font("微軟正黑體", 10) };

            btnSinglePlayer = new Button()
            { Text = "🚗 單人遊戲", Font = new Font("微軟正黑體", 12, FontStyle.Bold),
              BackColor = Color.SteelBlue, ForeColor = Color.White,
              Left = 30, Top = 135, Width = 300, Height = 36 };
            btnSinglePlayer.Click += BtnSinglePlayer_Click;

            btnCreateRoom = new Button()
            { Text = "🎮 建立房間", Font = new Font("微軟正黑體", 12, FontStyle.Bold),
              BackColor = Color.ForestGreen, ForeColor = Color.White,
              Left = 30, Top = 182, Width = 145, Height = 38 };
            btnCreateRoom.Click += BtnCreateRoom_Click;

            btnJoinRoom = new Button()
            { Text = "🔗 加入房間", Font = new Font("微軟正黑體", 12, FontStyle.Bold),
              BackColor = Color.DarkOrange, ForeColor = Color.White,
              Left = 185, Top = 182, Width = 145, Height = 38 };
            btnJoinRoom.Click += BtnJoinRoom_Click;

            Label lblTip = new Label()
            { Text = "多人模式：房主建立房間，其他人輸入房主 IP 加入", ForeColor = Color.Gray,
              Font = new Font("微軟正黑體", 8), Left = 30, Top = 240, Width = 300,
              TextAlign = ContentAlignment.MiddleCenter };

            menuPanel.Controls.AddRange(new Control[] { lblTitle, lblMap, cmbMapSelect,
                lblName, txtPlayerName,
                btnSinglePlayer, btnCreateRoom, btnJoinRoom, lblTip });
            this.Controls.Add(menuPanel);
        }

        private void BuildLobbyUI()
        {
            lobbyPanel = new Panel()
            {
                Size = new Size(360, 440),
                Location = new Point((640 - 360) / 2, Math.Max(0, (480 - 440) / 2)),
                BackColor = Color.FromArgb(230, Color.DarkSlateGray),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            Label lblTitle = new Label()
            { Text = "🏎 Turbo Velocity 大廳", Font = new Font("Impact", 16, FontStyle.Italic),
              ForeColor = Color.Yellow, Left = 20, Top = 6, Width = 320,
              TextAlign = ContentAlignment.MiddleCenter };

            lblLocalIP = new Label()
            { Text = "", ForeColor = Color.Cyan,
              Font = new Font("Consolas", 9), Left = 15, Top = 32,
              Width = 330, Height = 18, TextAlign = ContentAlignment.MiddleCenter };

            btnSearchRooms = new Button()
            { Text = "🔍 搜尋區域網路房間", BackColor = Color.MediumSlateBlue, ForeColor = Color.White,
              Left = 30, Top = 54, Width = 300, Height = 28 };
            btnSearchRooms.Click += BtnSearchRooms_Click;

            lstRooms = new ListBox()
            { Left = 30, Top = 86, Width = 300, Height = 64,
              BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.LightGray,
              Font = new Font("微軟正黑體", 9) };
            lstRooms.SelectedIndexChanged += LstRooms_SelectedIndexChanged;

            Label lblOr = new Label()
            { Text = "─ 或手動輸入 ─", ForeColor = Color.Gray,
              Font = new Font("微軟正黑體", 8), Left = 20, Top = 156,
              Width = 320, Height = 14, TextAlign = ContentAlignment.MiddleCenter };

            txtHostIp = new TextBox()
            { Text = "192.168.", Left = 130, Top = 176, Width = 110, Visible = false };
            Label lblIpLabel = new Label()
            { Text = "房主 IP:", ForeColor = Color.Gainsboro,
              Font = new Font("微軟正黑體", 9), Left = 50, Top = 178, Width = 60 };

            Label lblPortLabel = new Label()
            { Text = "Port:", ForeColor = Color.Gainsboro,
              Font = new Font("微軟正黑體", 9), Left = 50, Top = 206, Width = 40 };
            txtLobbyPort = new TextBox() { Text = "9001", Left = 90, Top = 204, Width = 55 };

            btnLobbyConnect = new Button()
            { Text = "🔗 連線", BackColor = Color.DarkOrange, ForeColor = Color.White,
              Left = 200, Top = 202, Width = 110, Height = 26, Visible = false };
            btnLobbyConnect.Click += BtnLobbyConnect_Click;

            lblMyName = new Label()
            { Text = "", ForeColor = Color.White,
              Font = new Font("微軟正黑體", 9), Left = 20, Top = 234,
              Width = 320, Height = 18 };

            lblLobbyStatus = new Label()
            { Text = "", ForeColor = Color.Lime,
              Font = new Font("微軟正黑體", 9), Left = 15, Top = 254,
              Width = 330, Height = 18, TextAlign = ContentAlignment.MiddleCenter };

            Label lblHeader = new Label()
            { Text = "─ 已連線玩家 ─", ForeColor = Color.Gray,
              Font = new Font("微軟正黑體", 8), Left = 20, Top = 274,
              Width = 320, Height = 14, TextAlign = ContentAlignment.MiddleCenter };

            int slotY = 290;
            for (int i = 0; i < 4; i++)
            {
                lblPlayerSlots[i] = new Label()
                { Text = "", ForeColor = Color.Gray,
                  Left = 25, Top = slotY + i * 28,
                  Width = 310, Height = 24, Font = new Font("微軟正黑體", 10, FontStyle.Bold) };
                lobbyPanel.Controls.Add(lblPlayerSlots[i]);
            }

            btnLobbyStart = new Button()
            { Text = "開始遊戲 (2+)", Font = new Font("微軟正黑體", 11, FontStyle.Bold),
              BackColor = Color.OrangeRed, ForeColor = Color.White,
              Left = 25, Top = slotY + 4 * 28 + 6, Width = 145, Height = 32, Enabled = false };
            btnLobbyStart.Click += BtnLobbyStart_Click;

            btnLobbyLeave = new Button()
            { Text = "離開房間", Font = new Font("微軟正黑體", 11, FontStyle.Bold),
              BackColor = Color.Gray, ForeColor = Color.White,
              Left = 190, Top = slotY + 4 * 28 + 6, Width = 145, Height = 32 };
            btnLobbyLeave.Click += BtnLobbyLeave_Click;

            lobbyPanel.Controls.AddRange(new Control[] { lblTitle, lblLocalIP,
                btnSearchRooms, lstRooms, lblOr,
                lblIpLabel, txtHostIp, lblPortLabel, txtLobbyPort, btnLobbyConnect,
                lblMyName, lblLobbyStatus, lblHeader,
                btnLobbyStart, btnLobbyLeave });
            this.Controls.Add(lobbyPanel);
        }

        private void ShowLobby(bool asHost)
        {
            isHostMode = asHost;
            lastAskedCount = 1;
            string myName = txtPlayerName.Text.Trim();
            if (string.IsNullOrEmpty(myName)) myName = DefaultNames[rng.Next(DefaultNames.Length)];

            menuPanel.Visible = false;
            lobbyPanel.Visible = true;

            foreach (var lbl in lblPlayerSlots)
                lbl.Text = "";

            lblLobbyStatus.Text = "";
            lblMyName.Text = $"👤 暱稱: {myName}";

            btnLobbyStart.Visible = asHost;
            btnLobbyStart.Enabled = false;
            txtLobbyPort.Enabled = true;

            bool isClient = !asHost;
            btnSearchRooms.Visible = isClient;
            lstRooms.Visible = isClient;
            txtHostIp.Visible = isClient;
            btnLobbyConnect.Visible = isClient;
            lstRooms.Items.Clear();

            if (asHost)
            {
                string localIP = NetworkManager.GetLocalIP();
                lblLocalIP.Text = $"🖥 您的 IP: {localIP}";
                lblLobbyStatus.Text = "正在建立房間...";

                int port = int.TryParse(txtLobbyPort.Text, out int p) ? p : NetworkManager.DefaultPort;
                if (netManager.StartHost(port, myName))
                {
                    lblLobbyStatus.Text = $"🟢 房間已建立 (Port {port})";
                    UpdateLobbySlots();
                    btnLobbyStart.Enabled = netManager.GetConnectedCount() >= 2;

                    netManager.OnPlayerJoined += Net_OnPlayerJoined;
                    netManager.OnPlayerLeft += Net_OnPlayerLeft;
                    netManager.OnGameStarted += Net_OnGameStarted;
                    netManager.OnLobbyRefresh += Net_OnLobbyRefresh;
                    netManager.OnJoinRequest += Net_OnJoinRequest;
                }
                else
                {
                    lblLobbyStatus.Text = $"❌ 建立失敗: {netManager.LastError}";
                }
            }
            else
            {
                lblLocalIP.Text = "🖥 點擊搜尋或手動輸入房主 IP";
                lblLobbyStatus.Text = "";
            }
        }

        private void ShowMainMenu()
        {
            netManager.Stop();
            netManager = new NetworkManager();
            lobbyPanel.Visible = false;
            menuPanel.Visible = true;
            currentState = GameState.MainMenu;
        }

        private void UpdateLobbySlots()
        {
            for (int i = 0; i < 4; i++)
            {
                var slot = lblPlayerSlots[i];
                if (netManager.Players[i].Connected)
                {
                    string name = netManager.Players[i].Name;
                    string tag;
                    Color color;
                    string icon;

                    if (i == 0)
                    {
                        tag = "房主";
                        color = Color.Lime;
                        icon = "👑";
                    }
                    else if (i == netManager.LocalPlayerId)
                    {
                        tag = "你";
                        color = Color.Yellow;
                        icon = "⭐";
                    }
                    else
                    {
                        tag = "";
                        color = Color.Cyan;
                        icon = "🚗";
                    }

                    slot.Text = $"  {icon} {name}  {(tag != "" ? $"({tag})" : "")}";
                    slot.ForeColor = color;
                }
                else
                {
                    slot.Text = $"  ⚫ 等待中...";
                    slot.ForeColor = Color.Gray;
                }
            }
            btnLobbyStart.Enabled = isHostMode && netManager.GetConnectedCount() >= 2;
        }

        private void Net_OnJoinRequest(string playerName)
        {
            this.BeginInvoke(new Action(() =>
            {
                var result = MessageBox.Show(
                    $"🚗 {playerName} 想要加入房間",
                    "連線請求",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    netManager.ApproveJoin();
                else
                    netManager.RejectJoin();
            }));
        }

        private void Net_OnJoinRejected()
        {
            this.BeginInvoke(new Action(() =>
            {
                lblLobbyStatus.Text = "❌ 房主拒絕了你的加入請求";
                btnSearchRooms.Visible = true;
                lstRooms.Visible = true;
                txtHostIp.Visible = true;
                btnLobbyConnect.Visible = true;
                txtHostIp.Enabled = true;
                txtLobbyPort.Enabled = true;
            }));
        }

        private void Net_OnPlayerJoined(int id)
        {
            this.BeginInvoke(new Action(() =>
            {
                UpdateLobbySlots();
                string name = netManager.Players[id].Name;
                int count = netManager.GetConnectedCount();
                lblLobbyStatus.Text = $"🔵 {name} 已加入 ({count}/4)";

                if (isHostMode && count >= 2 && count > lastAskedCount)
                {
                    lastAskedCount = count;
                    var result = MessageBox.Show(
                        $"已有 {count}/4 人加入！\n是否要開始比賽？",
                        "🏎 準備好了嗎？",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                        BtnLobbyStart_Click(null, EventArgs.Empty);
                }

                if (isHostMode)
                    btnLobbyStart.Enabled = count >= 2;
            }));
        }

        private void Net_OnPlayerLeft(int id)
        {
            this.BeginInvoke(new Action(() =>
            {
                UpdateLobbySlots();
                lblLobbyStatus.Text = $"❌ {netManager.Players[id].Name} 已離開";
                if (isHostMode)
                    btnLobbyStart.Enabled = netManager.GetConnectedCount() >= 2;
            }));
        }

        private void Net_OnLobbyRefresh()
        {
            this.BeginInvoke(new Action(() =>
            {
                UpdateLobbySlots();
                if (!isHostMode && netManager.LocalPlayerId >= 0 && netManager.Role == NetworkRole.Client)
                    lblLobbyStatus.Text = "🟢 已連線，等待房主開始...";
            }));
        }

        private void Net_OnGameStarted()
        {
            this.BeginInvoke(new Action(() =>
            {
                currentState = GameState.Playing;
                lobbyPanel.Visible = false;
                lastTime = DateTime.Now;
                this.Focus();
            }));
        }

        private void BtnSinglePlayer_Click(object? sender, EventArgs e)
        {
            menuPanel.Visible = false;
            currentState = GameState.Playing;
            lastTime = DateTime.Now;
            this.Focus();
        }

        private void BtnCreateRoom_Click(object? sender, EventArgs e)
        {
            ShowLobby(true);
        }

        private void BtnJoinRoom_Click(object? sender, EventArgs e)
        {
            ShowLobby(false);
        }

        private void BtnSearchRooms_Click(object? sender, EventArgs e)
        {
            btnSearchRooms.Enabled = false;
            btnSearchRooms.Text = "🔍 搜尋中...";
            lstRooms.Items.Clear();
            lblLobbyStatus.Text = "正在搜尋區域網路房間...";

            new Thread(() =>
            {
                var rooms = NetworkManager.DiscoverRooms();
                this.BeginInvoke(new Action(() =>
                {
                    lstRooms.Items.Clear();
                    foreach (var r in rooms)
                        lstRooms.Items.Add(r);
                    lblLobbyStatus.Text = rooms.Count > 0
                        ? $"🟢 找到 {rooms.Count} 個房間，點選即可連線"
                        : "❌ 沒有找到房間，請確認房主已開房";
                    btnSearchRooms.Enabled = true;
                    btnSearchRooms.Text = "🔍 搜尋區域網路房間";
                }));
            }) { IsBackground = true }.Start();
        }

        private void LstRooms_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (lstRooms.SelectedItem is RoomInfo room)
            {
                txtHostIp.Text = room.IP;
                txtLobbyPort.Text = room.Port.ToString();
                BtnLobbyConnect_Click(sender, e);
            }
        }

        private void BtnLobbyConnect_Click(object? sender, EventArgs e)
        {
            int localPort = int.TryParse(txtLobbyPort.Text, out int lp) ? lp : NetworkManager.DefaultPort;
            string hostIp = txtHostIp.Text.Trim();
            if (string.IsNullOrEmpty(hostIp)) { lblLobbyStatus.Text = "請輸入房主 IP"; return; }

            string myName = txtPlayerName.Text.Trim();
            if (string.IsNullOrEmpty(myName)) myName = DefaultNames[rng.Next(DefaultNames.Length)];

            lblLobbyStatus.Text = "正在連線...";
            if (netManager.StartClient(hostIp, NetworkManager.DefaultPort, localPort, myName))
            {
                lblLobbyStatus.Text = "⏳ 等待房主同意...";

                netManager.OnJoinRejected += Net_OnJoinRejected;
                netManager.OnPlayerJoined += Net_OnPlayerJoined;
                netManager.OnPlayerLeft += Net_OnPlayerLeft;
                netManager.OnGameStarted += Net_OnGameStarted;
                netManager.OnLobbyRefresh += Net_OnLobbyRefresh;

                btnSearchRooms.Visible = false;
                lstRooms.Visible = false;
                txtHostIp.Visible = false;
                btnLobbyConnect.Visible = false;
                txtHostIp.Enabled = false;
                txtLobbyPort.Enabled = false;
            }
            else
            {
                lblLobbyStatus.Text = $"❌ 連線失敗: {netManager.LastError}";
            }
        }

        private void BtnLobbyStart_Click(object? sender, EventArgs e)
        {
            if (netManager.GetConnectedCount() < 2) return;

            track.SetMap(cmbMapSelect.SelectedIndex);
            vehicle = new Vehicle();
            fCurvature = 0;
            fTrackCurvature = 0;
            currentLapTime = 0;
            lapTimes = new List<float> { 0, 0, 0, 0, 0 };

            netManager.SendStartGame();
            currentState = GameState.Playing;
            lobbyPanel.Visible = false;
            lastTime = DateTime.Now;
            this.Focus();
        }

        private void BtnLobbyLeave_Click(object? sender, EventArgs e)
        {
            ShowMainMenu();
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (currentState != GameState.Playing) return;
            if (e.KeyCode == Keys.Up) keyUp = true;
            if (e.KeyCode == Keys.Left) keyLeft = true;
            if (e.KeyCode == Keys.Right) keyRight = true;
        }

        private void MainForm_KeyUp(object? sender, KeyEventArgs e)
        {
            if (currentState != GameState.Playing) return;
            if (e.KeyCode == Keys.Up) keyUp = false;
            if (e.KeyCode == Keys.Left) keyLeft = false;
            if (e.KeyCode == Keys.Right) keyRight = false;
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            if (currentState == GameState.MainMenu)
            {
                Bitmap menuBmp = new Bitmap(picCanvas.Width, picCanvas.Height);
                using (Graphics g = Graphics.FromImage(menuBmp))
                {
                    g.Clear(Color.Black);
                    track.Render(g, menuBmp.Width, menuBmp.Height, 0, 0, 0, 0);
                    vehicle.Render(g, menuBmp.Width, menuBmp.Height, 0);

                    using (Font tipsFont = new Font("微軟正黑體", 10, FontStyle.Bold))
                    {
                        g.DrawString($"當前預覽: {track.MapName}", tipsFont, Brushes.White, 10, 10);
                    }
                }
                picCanvas.Image?.Dispose();
                picCanvas.Image = menuBmp;
                return;
            }

            DateTime currentTime = DateTime.Now;
            float elapsedTime = (float)(currentTime - lastTime).TotalSeconds;
            lastTime = currentTime;

            if (elapsedTime > 0.1f) elapsedTime = 0.1f;

            var trackDetails = track.GetCurrentDetails(vehicle.Distance);
            float targetCurvature = trackDetails.curvature;
            int sectionIndex = trackDetails.sectionIndex;

            float trackCurveDiff = (targetCurvature - fCurvature) * elapsedTime * vehicle.Speed;
            fCurvature += trackCurveDiff;
            fTrackCurvature += fCurvature * elapsedTime * vehicle.Speed;

            float oldDistance = vehicle.Distance;
            vehicle.Update(keyUp, keyLeft, keyRight, fTrackCurvature, elapsedTime, track.TotalDistance);

            currentLapTime += elapsedTime;
            if (vehicle.Distance < oldDistance)
            {
                lapTimes.Insert(0, currentLapTime);
                if (lapTimes.Count > 5) lapTimes.RemoveAt(5);
                currentLapTime = 0.0f;
            }

            if (netManager.Role != NetworkRole.None)
                netManager.SendPlayerState(vehicle.Distance, vehicle.PlayerCurvature, vehicle.CurrentSteerDir);

            if (netManager.Role == NetworkRole.Host)
                netManager.CheckTimeouts();

            Bitmap bmp = new Bitmap(picCanvas.Width, picCanvas.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);

                track.Render(g, bmp.Width, bmp.Height, vehicle.Distance, fTrackCurvature, fCurvature, sectionIndex);

                var renderOrder = new List<(int id, float dist)>();
                renderOrder.Add((netManager.LocalPlayerId, vehicle.Distance));
                for (int i = 0; i < 4; i++)
                {
                    if (i != netManager.LocalPlayerId && netManager.Players[i].Connected)
                        renderOrder.Add((i, netManager.Players[i].Distance));
                }
                renderOrder.Sort((a, b) => b.dist.CompareTo(a.dist));

                foreach (var (id, _) in renderOrder)
                {
                    if (id == netManager.LocalPlayerId)
                        vehicle.Render(g, bmp.Width, bmp.Height, fTrackCurvature);
                    else
                        vehicle.RenderRemote(g, bmp.Width, bmp.Height, fTrackCurvature, netManager.Players[id]);
                }

                var remotePlayerData = new List<(float, Color)>();
                Color[] dotColors = { Color.Gold, Color.Cyan, Color.Magenta };
                int ci = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (i != netManager.LocalPlayerId && netManager.Players[i].Connected && ci < dotColors.Length)
                        remotePlayerData.Add((netManager.Players[i].Distance, dotColors[ci++]));
                }
                track.RenderMiniMap(g, bmp.Width, vehicle.Distance,
                    remotePlayerData.Count > 0 ? remotePlayerData.ToArray() : null);

                using (Font hudFont = new Font("Arial", 11, FontStyle.Bold))
                {
                    g.DrawString($"SPEED : {(vehicle.Speed * 320):0} km/h", hudFont, Brushes.Yellow, 10, 10);
                    g.DrawString($"LAP TIME : {currentLapTime:F2}s", hudFont, Brushes.White, 10, 30);
                    g.DrawString($"DISTANCE : {vehicle.Distance:F1} m", hudFont, Brushes.White, 10, 50);
                    g.DrawString($"MAP: {track.MapName}", hudFont, Brushes.Orange, 10, 80);

                    if (netManager.Role != NetworkRole.None)
                    {
                        int online = netManager.GetConnectedCount();
                        g.DrawString($"ONLINE: {online}/4", hudFont, Brushes.Lime, 10, 110);

                        int ny = 132;
                        Color[] nameColors = { Color.Lime, Color.Gold, Color.Cyan, Color.Magenta };
                        for (int i = 0; i < 4; i++)
                        {
                            if (netManager.Players[i].Connected)
                            {
                                string mark = i == netManager.LocalPlayerId ? "★" : "•";
                                g.DrawString($"{mark} {netManager.Players[i].Name}", hudFont, new SolidBrush(nameColors[i]), 10, ny);
                                ny += 18;
                            }
                        }
                    }
                }
            }

            picCanvas.Image?.Dispose();
            picCanvas.Image = bmp;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            netManager.Stop();
            track.Dispose();
            base.OnFormClosing(e);
        }

    }
}
