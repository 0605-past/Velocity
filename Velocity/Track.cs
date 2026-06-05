using System;
using System.Collections.Generic;
using System.Drawing;

namespace Pseudo3DRacer
{
    public class TrackSection
    {
        public float Curvature { get; set; }
        public float Length { get; set; }
        public TrackSection(float curvature, float length)
        {
            Curvature = curvature;
            Length = length;
        }
    }

    public class Track
    {
        public List<TrackSection> Sections { get; private set; } = new List<TrackSection>();
        public float TotalDistance { get; private set; }
        public string MapName { get; private set; } = "經典新手村";

        private Color skyColorTop = Color.DeepSkyBlue;
        private Color skyColorBottom = Color.RoyalBlue;
        private Brush grassColorBright = Brushes.LimeGreen;
        private Brush grassColorDark = Brushes.Green;

        private List<PointF> mapPoints = new List<PointF>();
        private float mapMinX, mapMaxX, mapMinY, mapMaxY;

        public Track()
        {
            SetMap(0);
        }

        public void SetMap(int mapIndex)
        {
            Sections.Clear();
            TotalDistance = 0;

            if (mapIndex == 0)
            {
                MapName = "經典新手村 (經典繞圈)";
                skyColorTop = Color.DeepSkyBlue;
                skyColorBottom = Color.RoyalBlue;
                grassColorBright = Brushes.LimeGreen;
                grassColorDark = Brushes.Green;

                Sections.Add(new TrackSection(0.0f, 210.0f));
                Sections.Add(new TrackSection(1.0f, 200.0f));
                Sections.Add(new TrackSection(0.0f, 400.0f));
                Sections.Add(new TrackSection(-1.0f, 150.0f));
                Sections.Add(new TrackSection(0.0f, 200.0f));
                Sections.Add(new TrackSection(0.4f, 400.0f));
            }
            else if (mapIndex == 1)
            {
                MapName = "極速沙漠高架 (高速直線)";
                skyColorTop = Color.OrangeRed;
                skyColorBottom = Color.Gold;
                grassColorBright = new SolidBrush(Color.FromArgb(210, 180, 140));
                grassColorDark = new SolidBrush(Color.FromArgb(139, 69, 19));

                Sections.Add(new TrackSection(0.0f, 600.0f));
                Sections.Add(new TrackSection(0.3f, 200.0f));
                Sections.Add(new TrackSection(0.0f, 500.0f));
                Sections.Add(new TrackSection(-0.3f, 200.0f));
                Sections.Add(new TrackSection(0.0f, 300.0f));
            }
            else
            {
                MapName = "秋名山地獄 (九彎十八拐)";
                skyColorTop = Color.Purple;
                skyColorBottom = Color.DarkSlateBlue;
                grassColorBright = Brushes.DarkGreen;
                grassColorDark = new SolidBrush(Color.FromArgb(0, 30, 0));

                Sections.Add(new TrackSection(0.0f, 100.0f));
                Sections.Add(new TrackSection(1.5f, 100.0f));
                Sections.Add(new TrackSection(-1.5f, 100.0f));
                Sections.Add(new TrackSection(0.0f, 150.0f));
                Sections.Add(new TrackSection(1.8f, 120.0f));
                Sections.Add(new TrackSection(-1.2f, 150.0f));
                Sections.Add(new TrackSection(0.5f, 300.0f));
            }

            foreach (var s in Sections) TotalDistance += s.Length;
            CalculateMapPoints();
        }

        private void CalculateMapPoints()
        {
            mapPoints.Clear();
            float x = 0, y = 0;
            float angle = -(float)Math.PI / 2.0f;
            float step = 2.0f;

            mapMinX = mapMaxX = x;
            mapMinY = mapMaxY = y;

            for (float d = 0; d < TotalDistance; d += step)
            {
                var details = GetCurrentDetails(d);
                angle += details.curvature * 0.005f * step;

                x += (float)Math.Cos(angle) * step;
                y += (float)Math.Sin(angle) * step;

                mapPoints.Add(new PointF(x, y));

                if (x < mapMinX) mapMinX = x; if (x > mapMaxX) mapMaxX = x;
                if (y < mapMinY) mapMinY = y; if (y > mapMaxY) mapMaxY = y;
            }
        }

        public (float curvature, int sectionIndex) GetCurrentDetails(float distance)
        {
            float offset = 0;
            int index = 0;
            while (index < Sections.Count && offset <= distance)
            {
                offset += Sections[index].Length;
                index++;
            }
            return (Sections[Math.Max(0, index - 1)].Curvature, Math.Max(0, index - 1));
        }

        public void RenderMiniMap(Graphics g, int screenWidth, float carDistance, bool showRemote, float remoteDistance)
        {
            int mapSize = 100;
            int padding = 20;
            int mapLeft = screenWidth - mapSize - padding;
            int mapTop = padding;

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
            {
                g.FillRectangle(bgBrush, mapLeft - 5, mapTop - 5, mapSize + 10, mapSize + 10);
                g.DrawRectangle(Pens.White, mapLeft - 5, mapTop - 5, mapSize + 10, mapSize + 10);
            }

            if (mapPoints.Count < 2) return;

            float trackWidth = mapMaxX - mapMinX;
            float trackHeight = mapMaxY - mapMinY;
            float scale = Math.Min(mapSize / trackWidth, mapSize / trackHeight) * 0.85f;

            float offsetX = mapLeft + (mapSize - trackWidth * scale) / 2.0f - mapMinX * scale;
            float offsetY = mapTop + (mapSize - trackHeight * scale) / 2.0f - mapMinY * scale;

            List<PointF> screenPoints = new List<PointF>();
            foreach (var pt in mapPoints)
            {
                screenPoints.Add(new PointF(pt.X * scale + offsetX, pt.Y * scale + offsetY));
            }

            using (Pen trackPen = new Pen(Color.LightGray, 2))
            {
                g.DrawLines(trackPen, screenPoints.ToArray());
                g.DrawLine(trackPen, screenPoints[screenPoints.Count - 1], screenPoints[0]);
            }

            var oldMode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (showRemote)
            {
                int remoteIndex = (int)((remoteDistance / TotalDistance) * screenPoints.Count);
                remoteIndex = Math.Max(0, Math.Min(remoteIndex, screenPoints.Count - 1));
                PointF rPos = screenPoints[remoteIndex];
                g.FillEllipse(Brushes.Gold, rPos.X - 3, rPos.Y - 3, 6, 6);
            }

            int playerPointIndex = (int)((carDistance / TotalDistance) * screenPoints.Count);
            playerPointIndex = Math.Max(0, Math.Min(playerPointIndex, screenPoints.Count - 1));
            PointF playerPos = screenPoints[playerPointIndex];

            g.FillEllipse(Brushes.Lime, playerPos.X - 4, playerPos.Y - 4, 8, 8);
            g.DrawEllipse(Pens.White, playerPos.X - 4, playerPos.Y - 4, 8, 8);

            g.SmoothingMode = oldMode;
        }

        public void Render(Graphics g, int width, int height, float carDistance, float currentTrackCurvature, float currentSectionCurvature, int sectionIndex)
        {
            int halfHeight = height / 2;

            g.FillRectangle(new SolidBrush(skyColorTop), 0, 0, width, halfHeight / 2);
            g.FillRectangle(new SolidBrush(skyColorBottom), 0, halfHeight / 2, width, halfHeight / 2);

            for (int x = 0; x < width; x++)
            {
                int hillHeight = (int)(Math.Abs(Math.Sin(x * 0.015f + currentTrackCurvature) * 20.0f));
                g.DrawLine(Pens.SaddleBrown, x, halfHeight - hillHeight, x, halfHeight);
            }

            for (int y = 0; y < halfHeight; y++)
            {
                float perspective = (float)y / halfHeight;
                float roadWidth = 0.1f + perspective * 0.8f;
                float clipWidth = roadWidth * 0.15f;
                roadWidth *= 0.5f;

                float middlePoint = 0.5f + currentSectionCurvature * (float)Math.Pow(1.0f - perspective, 3);

                int leftGrass = (int)((middlePoint - roadWidth - clipWidth) * width);
                int leftClip = (int)((middlePoint - roadWidth) * width);
                int rightClip = (int)((middlePoint + roadWidth) * width);
                int rightGrass = (int)((middlePoint + roadWidth + clipWidth) * width);

                int rowY = halfHeight + y;

                bool grassToggle = Math.Sin(20.0f * Math.Pow(1.0f - perspective, 3) + carDistance * 0.1f) > 0;
                bool clipToggle = Math.Sin(80.0f * Math.Pow(1.0f - perspective, 2) + carDistance) > 0;

                Brush grassBrush = grassToggle ? grassColorBright : grassColorDark;
                Brush clipBrush = clipToggle ? Brushes.Red : Brushes.White;
                Brush roadBrush = (sectionIndex == 0) ? Brushes.White : Brushes.Black;

                g.FillRectangle(grassBrush, 0, rowY, leftGrass, 1);
                g.FillRectangle(clipBrush, leftGrass, rowY, leftClip - leftGrass, 1);
                g.FillRectangle(roadBrush, leftClip, rowY, rightClip - leftClip, 1);
                g.FillRectangle(clipBrush, rightClip, rowY, rightGrass - rightClip, 1);
                g.FillRectangle(grassBrush, rightGrass, rowY, width - rightGrass, 1);
            }
        }
    }
}