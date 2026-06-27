using System.Collections.Generic;

namespace Pseudo3DRacer;

public struct ReplayFrame
{
    public float Distance;
    public float Curvature;
    public float LateralOffset;
    public bool Up, Left, Right, Boost;
}

public class ReplayRecorder
{
    private readonly List<ReplayFrame> _frames = new();
    private bool _recording;

    public IReadOnlyList<ReplayFrame> Frames => _frames;
    public bool HasReplay => _frames.Count > 0;

    public void StartLap() { _frames.Clear(); _recording = true; }

    public void Record(float dist, float curve, float lateral, bool up, bool left, bool right, bool boost)
    {
        if (!_recording) return;
        if (_frames.Count == 0 || _frames.Count % 2 == 0)
            _frames.Add(new ReplayFrame { Distance = dist, Curvature = curve, LateralOffset = lateral, Up = up, Left = left, Right = right, Boost = boost });
    }

    public void Stop() => _recording = false;
}
