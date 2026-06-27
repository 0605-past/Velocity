using System;

namespace Pseudo3DRacer;

public class SectorTimer
{
    private readonly float _sectorLength;
    private int _currentSector;
    private float _sectorStartTime;
    private float[] _sectorTimes = new float[3];
    private bool[] _sectorDone = new bool[3];

    public float[] SectorTimes => _sectorTimes;
    public string LastToast { get; private set; } = "";
    public float LastToastTimer { get; private set; }
    public int CurrentSectorIndex => Math.Min(2, _currentSector);
    public int LastSectorDeltaMs { get; private set; }

    public SectorTimer(float totalDistance) => _sectorLength = totalDistance / 3f;

    public void Reset()
    {
        _currentSector = 0;
        _sectorStartTime = 0;
        _sectorTimes = new float[3];
        _sectorDone = new bool[3];
        LastToast = "";
        LastToastTimer = 0;
    }

    public void Update(float lapTime, float distance, float totalDistance, GameSaveData save, int mapIndex)
    {
        if (LastToastTimer > 0) LastToastTimer -= 0.016f;

        float loopDist = distance % totalDistance;
        if (loopDist < 0) loopDist += totalDistance;

        int sector = Math.Min(2, (int)(loopDist / _sectorLength));
        if (sector > _currentSector && !_sectorDone[sector])
        {
            float sectorTime = lapTime - _sectorStartTime;
            _sectorTimes[sector] = sectorTime;
            _sectorDone[sector] = true;
            _currentSector = sector;
            _sectorStartTime = lapTime;

            float best = save.BestSectorByMap[mapIndex][sector];
            float diff = sectorTime - best;
            if (diff < 0) { save.BestSectorByMap[mapIndex][sector] = sectorTime; diff = 0; }
            LastSectorDeltaMs = (int)Math.Round(diff * 1000);
            LastToast = $"SECTOR {sector + 1}: {sectorTime:F2}s [{LastSectorDeltaMs}]";
            LastToastTimer = 2.5f;
        }
    }

    public void OnNewLap()
    {
        _currentSector = 0;
        _sectorStartTime = 0;
        _sectorDone = new bool[3];
    }
}
