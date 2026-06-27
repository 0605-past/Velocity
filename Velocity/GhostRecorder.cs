using System.Collections.Generic;

namespace Pseudo3DRacer;

public struct GhostFrame
{
    public float Distance;
    public float Curvature;
    public float LateralOffset;
}

public class GhostRecorder
{
    private readonly List<GhostFrame> _frames = new();
    private readonly List<GhostFrame> _bestLap = new();
    private float _bestLapTime = float.MaxValue;
    private bool _recording;

    public IReadOnlyList<GhostFrame> BestLap => _bestLap;
    public bool HasGhost => _bestLap.Count > 0;

    public void StartLap()
    {
        _frames.Clear();
        _recording = true;
    }

    public void Record(float distance, float curvature, float lateralOffset)
    {
        if (!_recording) return;
        _frames.Add(new GhostFrame { Distance = distance, Curvature = curvature, LateralOffset = lateralOffset });
    }

    public void FinishLap(float lapTime)
    {
        _recording = false;
        if (lapTime > 0 && lapTime < _bestLapTime)
        {
            _bestLapTime = lapTime;
            _bestLap.Clear();
            _bestLap.AddRange(_frames);
        }
    }

    public GhostFrame? GetFrameAt(float distance, float totalDistance)
    {
        if (_bestLap.Count == 0) return null;
        float loop = distance % totalDistance;
        if (loop < 0) loop += totalDistance;
        GhostFrame? best = null;
        float bestDiff = float.MaxValue;
        foreach (var f in _bestLap)
        {
            float fd = f.Distance % totalDistance;
            if (fd < 0) fd += totalDistance;
            float diff = System.Math.Abs(fd - loop);
            if (diff < bestDiff) { bestDiff = diff; best = f; }
        }
        return best;
    }
}
