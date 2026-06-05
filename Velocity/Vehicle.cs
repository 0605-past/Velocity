using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Pseudo3DRacer
{
    public class Vehicle
    {
        public float Distance { get; set; }
        public float PlayerCurvature { get; set; }
        public float Speed { get; private set; }
        public int CurrentSteerDir { get; private set; } = 0;

        private float fLeanAngle = 0.0f;
        private float fSmokeTimer = 0.0f;

        public Vehicle()
        {
            Distance = 0.0f;
            PlayerCurvature = 0.0f;
            Speed = 0.0f;
        }

        public void Update(bool up, bool left, bool right, float trackCurvature, float elapsedTime, float totalTrackDistance)
        {
            CurrentSteerDir = 0;
            if (left) CurrentSteerDir = -1;
            if (right) CurrentSteerDir = 1;

            if (up) Speed += 2.0f * elapsedTime;
            else Speed -= 1.0f * elapsedTime;

            float steeringSensitivity = 0.7f * elapsedTime * (1.0f - Speed / 2.0f);
            if (left) PlayerCurvature -= steeringSensitivity;
            if (right) PlayerCurvature += steeringSensitivity;

            if (Math.Abs(PlayerCurvature - trackCurvature) >= 0.8f) Speed -= 5.0f * elapsedTime;
            Speed = Math.Max(0.0f, Math.Min(1.0f, Speed));
            Distance += (70.0f * Speed) * elapsedTime;
            if (Distance >= totalTrackDistance) Distance -= totalTrackDistance;

            // 平滑計算車身傾斜
            float targetLean = CurrentSteerDir * 0.06f;
            fLeanAngle += (targetLean - fLeanAngle) * 12.0f * elapsedTime;
            fSmokeTimer += elapsedTime * 25.0f;
        }

        public void Render(Graphics g, int width, int height, float trackCurvature)
        {
            RenderCar(g, width, height, trackCurvature, this.PlayerCurvature, Brushes.Red, fLeanAngle, CurrentSteerDir);
        }

        private static readonly Color[] RemoteColors = { Color.Gold, Color.Cyan, Color.Magenta };

        public void RenderRemote(Graphics g, int width, int height, float trackCurvature, NetworkPlayer player)
        {
            if (!player.Connected) return;
            float remoteLean = player.SteerDir * 0.06f;
            int colorIndex = (player.Id - 1) % RemoteColors.Length;
            using (Brush brush = new SolidBrush(RemoteColors[colorIndex]))
            {
                RenderCar(g, width, height, trackCurvature, player.PlayerCurvature, brush, remoteLean, player.SteerDir);
            }
        }

        private void RenderCar(Graphics g, int width, int height, float trackCurvature, float carCurvature, Brush carBrush, float leanAngle, int steerDir)
        {
            float carPosRelative = carCurvature - trackCurvature;
            int carWidth = 110;
            int carHeight = 55;
            int carX = (width / 2) + (int)((width * carPosRelative) / 2.0f) - (carWidth / 2);
            int carY = height - 100;

            if (Speed > 0.05f && ((int)fSmokeTimer % 2 == 0)) carY += 1;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Matrix originalMatrix = g.Transform;
            PointF rotationCenter = new PointF(carX + (carWidth / 2.0f), carY + carHeight);
            Matrix leanMatrix = g.Transform;
            leanMatrix.RotateAt(leanAngle * (180.0f / (float)Math.PI), rotationCenter);
            g.Transform = leanMatrix;

            // 後尾翼
            g.FillRectangle(carBrush, carX + 5, carY - 15, carWidth - 10, 15);
            g.FillRectangle(Brushes.Black, carX + 15, carY - 8, carWidth - 30, 8);

            // 主車身
            g.FillRectangle(carBrush, carX, carY, carWidth, carHeight - 15);

            Point[] rearSlant = {
                new Point(carX, carY + carHeight - 25),
                new Point(carX + 10, carY + carHeight - 15),
                new Point(carX, carY + carHeight - 15)
            };
            g.FillPolygon(Brushes.Black, rearSlant);

            // 後車窗
            Point[] rearWindow = {
                new Point(carX + 25, carY),
                new Point(carX + 85, carY),
                new Point(carX + 75, carY - 12),
                new Point(carX + 35, carY - 12)
            };
            g.FillPolygon(Brushes.Black, rearWindow);

            using (Pen louverPen = new Pen(Color.FromArgb(50, 50, 50), 2))
            {
                g.DrawLine(louverPen, carX + 40, carY - 4, carX + 70, carY - 4);
                g.DrawLine(louverPen, carX + 43, carY - 8, carX + 67, carY - 8);
            }

            // 四圓尾燈
            g.FillRectangle(Brushes.DarkRed, carX + 10, carY + 8, carWidth - 20, 14);
            g.FillRectangle(Brushes.Black, carX + 12, carY + 10, carWidth - 24, 10);
            g.FillEllipse(Brushes.Orange, carX + 18, carY + 12, 6, 6);
            g.FillEllipse(Brushes.Red, carX + 26, carY + 12, 6, 6);
            g.FillEllipse(Brushes.Red, carX + carWidth - 32, carY + 12, 6, 6);
            g.FillEllipse(Brushes.Orange, carX + carWidth - 24, carY + 12, 6, 6);

            // 排氣管
            g.FillRectangle(Brushes.Gray, carX + (carWidth / 2) - 8, carY + 28, 16, 4);
            g.FillEllipse(Brushes.Black, carX + (carWidth / 2) - 6, carY + 29, 3, 3);
            g.FillEllipse(Brushes.Black, carX + (carWidth / 2) - 1, carY + 29, 3, 3);
            g.FillEllipse(Brushes.Black, carX + (carWidth / 2) + 4, carY + 29, 3, 3);

            // 輪胎
            g.FillRectangle(Brushes.DimGray, carX + 8, carY + carHeight - 18, 16, 18);
            g.FillRectangle(Brushes.Black, carX + 8, carY + carHeight - 18, 4, 18);
            g.FillRectangle(Brushes.DimGray, carX + carWidth - 24, carY + carHeight - 18, 16, 18);
            g.FillRectangle(Brushes.Black, carX + carWidth - 12, carY + carHeight - 18, 4, 18);
            g.FillRectangle(Brushes.Silver, carX + 14, carY + carHeight - 10, 4, 4);
            g.FillRectangle(Brushes.Silver, carX + carWidth - 18, carY + carHeight - 10, 4, 4);

            // 底部陰影
            g.FillRectangle(Brushes.Black, carX + 24, carY + carHeight - 4, carWidth - 48, 4);

            g.Transform = originalMatrix;

            // 噴煙特效
            if (steerDir != 0)
            {
                int smokeFrame = (int)fSmokeTimer % 3;
                int leftWheelX = carX + 8;
                int rightWheelX = carX + carWidth - 24;
                int smokeY = carY + carHeight - 5;

                using (Brush smokeBrush = new SolidBrush(Color.FromArgb(160, Color.LightGray)))
                {
                    if (steerDir == -1)
                    {
                        g.FillEllipse(smokeBrush, rightWheelX + 18 + (smokeFrame * 3), smokeY - (smokeFrame * 2), 6 + smokeFrame * 3, 6 + smokeFrame * 2);
                    }
                    else if (steerDir == 1)
                    {
                        g.FillEllipse(smokeBrush, leftWheelX - 12 - (smokeFrame * 3), smokeY - (smokeFrame * 2), 6 + smokeFrame * 3, 6 + smokeFrame * 2);
                    }
                }
            }
        }
    }
}