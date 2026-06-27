namespace Pseudo3DRacer;

/// <summary>Integrates track section curvature into world-space road center offset.</summary>
public sealed class TrackCurvatureController
{
    public float Section { get; private set; }
    public float Track { get; private set; }
    public float Render { get; private set; }

    public void Reset() => Section = Track = Render = 0;

    public void Update(Track track, float distance, float speed, float dt)
    {
        float target = track.GetInterpolatedCurvature(distance);
        Section += (target - Section) * dt * Math.Max(speed, 0.1f) * 2.2f;
        Render += (Section - Render) * dt * 2.8f;
        Track += Section * dt * speed;
    }
}
