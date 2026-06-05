using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
        private List<float> lapTimes = new List<float>() { 0, 0, 0, 0, 0 };
        private PictureBox picCanvas;

        private GameState currentState = GameState.MainMenu;

        private UdpClient? udpClient = null;
        private IPEndPoint? remoteEP = null;
        private Thread? receiveThread = null;
        private bool isMultiplayer = false;

        private float remoteDistance = 0.0f;
        private float remotePlayerCurvature = 0.0f;
        private int remoteSteerDir = 0;

        // 使用 🌟 null-forgiving 運算子（!）通知編譯器這些控制項會在 InitializeMenuUI 中被完整實例化
        private Panel menuPanel = null!;
        private ComboBox cmbMapSelect = null!;
        private CheckBox chkEnableNet = null!;
        private TextBox txtRemoteIp = null!;
        private TextBox txtLocalPort = null!;
        private TextBox txtRemotePort = null!;
        private Button btnStartGame = null!;

        public MainForm()
        {
            this.Width = 640;
            this.Height = 500;
            this.Text = "Turbo Velocity - Custom Art Edition";
            this.KeyPreview = true;

            track = new Track();
            vehicle = new Vehicle();

            InitializeMenuUI();

            picCanvas = new PictureBox();
            picCanvas.Dock = DockStyle.Fill;
            this.Controls.Add(picCanvas);

            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;

            gameTimer = new System.Windows.Forms.Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;

            lastTime = DateTime.Now;
            gameTimer.Start();
        }

        private void InitializeMenuUI()
        {
            menuPanel = new Panel()
            {
                Size = new Size(360, 320),
                Location = new Point((640 - 360) / 2, (480 - 320) / 2 - 20),
                BackColor = Color.FromArgb(230, Color.DarkSlateGray),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTitle = new Label() { Text = "TURBO VELOCITY", Font = new Font("Impact", 22, FontStyle.Italic), ForeColor = Color.Yellow, Left = 20, Top = 15, Width = 320, TextAlign = ContentAlignment.MiddleCenter };

            Label lblMap = new Label() { Text = "選擇賽道:", ForeColor = Color.White, Font = new Font("微軟正黑體", 10, FontStyle.Bold), Left = 30, Top = 70, Width = 80 };
            cmbMapSelect = new ComboBox() { Left = 120, Top = 68, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMapSelect.Items.AddRange(new string[] { "1. 經典新手村", "2. 極速沙漠高架", "3. 秋名山地獄" });
            cmbMapSelect.SelectedIndex = 0;
            cmbMapSelect.SelectedIndexChanged += (s, e) => { track.SetMap(cmbMapSelect.SelectedIndex); };

            chkEnableNet = new CheckBox() { Text = "啟用雙人 UDP 連線對戰", ForeColor = Color.Lime, Font = new Font("微軟正黑體", 10, FontStyle.Bold), Left = 30, Top = 110, Width = 250, Checked = false };
            chkEnableNet.CheckedChanged += ChkEnableNet_CheckedChanged;

            Label lblIp = new Label() { Text = "好友 IP:", ForeColor = Color.Gainsboro, Left = 40, Top = 150, Width = 70 };
            txtRemoteIp = new TextBox() { Text = "127.0.0.1", Left = 120, Top = 147, Width = 110, Enabled = false };

            Label lblLocal = new Label() { Text = "我 Port:", ForeColor = Color.Gainsboro, Left = 40, Top = 185, Width = 70 };
            txtLocalPort = new TextBox() { Text = "9001", Left = 120, Top = 182, Width = 50, Enabled = false };

            Label lblRemote = new Label() { Text = "他 Port:", ForeColor = Color.Gainsboro, Left = 185, Top = 185, Width = 50 };
            txtRemotePort = new TextBox() { Text = "9002", Left = 240, Top = 182, Width = 50, Enabled = false };

            btnStartGame = new Button() { Text = "進入賽道 (START)", Font = new Font("Impact", 14), BackColor = Color.OrangeRed, ForeColor = Color.White, Left = 30, Top = 240, Width = 300, Height = 45 };
            btnStartGame.Click += BtnStartGame_Click;

            menuPanel.Controls.AddRange(new Control[] { lblTitle, lblMap, cmbMapSelect, chkEnableNet, lblIp, txtRemoteIp, lblLocal, txtLocalPort, lblRemote, txtRemotePort, btnStartGame });
            this.Controls.Add(menuPanel);
        }

        private void ChkEnableNet_CheckedChanged(object? sender, EventArgs e)
        {
            bool isNet = chkEnableNet.Checked;
            txtRemoteIp.Enabled = isNet;
            txtLocalPort.Enabled = isNet;
            txtRemotePort.Enabled = isNet;
        }

        private void BtnStartGame_Click(object? sender, EventArgs e)
        {
            if (chkEnableNet.Checked)
            {
                try
                {
                    int localPort = int.Parse(txtLocalPort.Text);
                    int remotePort = int.Parse(txtRemotePort.Text);
                    string remoteIp = txtRemoteIp.Text;

                    udpClient = new UdpClient(localPort);
                    remoteEP = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);

                    receiveThread = new Thread(ReceiveDataLoop);
                    receiveThread.IsBackground = true;
                    receiveThread.Start();

                    isMultiplayer = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("UDP 初始化失敗，請檢查 Port 是否被佔用: " + ex.Message);
                    return;
                }
            }

            menuPanel.Visible = false;
            currentState = GameState.Playing;
            lastTime = DateTime.Now;
            this.Focus();
        }

        private void ReceiveDataLoop()
        {
            while (isMultiplayer && udpClient != null)
            {
                try
                {
                    IPEndPoint anyEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = udpClient.Receive(ref anyEP);
                    string rawPacket = Encoding.UTF8.GetString(buffer);

                    string[] tokens = rawPacket.Split(',');
                    if (tokens.Length == 3)
                    {
                        remoteDistance = float.Parse(tokens[0]);
                        remotePlayerCurvature = float.Parse(tokens[1]);
                        remoteSteerDir = int.Parse(tokens[2]);
                    }
                }
                catch { break; }
            }
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

            if (isMultiplayer && udpClient != null && remoteEP != null)
            {
                try
                {
                    string packetString = $"{vehicle.Distance},{vehicle.PlayerCurvature},{vehicle.CurrentSteerDir}";
                    byte[] sendData = Encoding.UTF8.GetBytes(packetString);
                    udpClient.Send(sendData, sendData.Length, remoteEP);
                }
                catch { }
            }

            Bitmap bmp = new Bitmap(picCanvas.Width, picCanvas.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);

                track.Render(g, bmp.Width, bmp.Height, vehicle.Distance, fTrackCurvature, fCurvature, sectionIndex);

                if (isMultiplayer)
                {
                    if (remoteDistance < vehicle.Distance)
                    {
                        vehicle.RenderRemote(g, bmp.Width, bmp.Height, fTrackCurvature, remotePlayerCurvature, remoteSteerDir);
                        vehicle.Render(g, bmp.Width, bmp.Height, fTrackCurvature);
                    }
                    else
                    {
                        vehicle.Render(g, bmp.Width, bmp.Height, fTrackCurvature);
                        vehicle.RenderRemote(g, bmp.Width, bmp.Height, fTrackCurvature, remotePlayerCurvature, remoteSteerDir);
                    }
                }
                else
                {
                    vehicle.Render(g, bmp.Width, bmp.Height, fTrackCurvature);
                }

                track.RenderMiniMap(g, bmp.Width, vehicle.Distance, isMultiplayer, remoteDistance);

                using (Font hudFont = new Font("Arial", 11, FontStyle.Bold))
                {
                    g.DrawString($"SPEED : {(vehicle.Speed * 320):0} km/h", hudFont, Brushes.Yellow, 10, 10);
                    g.DrawString($"LAP TIME : {currentLapTime:F2}s", hudFont, Brushes.White, 10, 30);
                    g.DrawString($"DISTANCE : {vehicle.Distance:F1} m", hudFont, Brushes.White, 10, 50);
                    g.DrawString($"MAP: {track.MapName}", hudFont, Brushes.Orange, 10, 80);

                    if (isMultiplayer) g.DrawString("MULTIPLAYER ONLINE", hudFont, Brushes.Lime, 10, 110);
                }
            }

            picCanvas.Image?.Dispose();
            picCanvas.Image = bmp;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            isMultiplayer = false;
            if (udpClient != null) udpClient.Close();
            base.OnFormClosing(e);
        }
    }
}