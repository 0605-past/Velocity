using System;
using System.Collections.Generic;
using System.Drawing;

namespace Pseudo3DRacer;

public class Particle
{
    public float X, Y;
    public float Vx, Vy;
    public float Life;
    public Color Color;
    public float Size;
}

public class ParticleSystem
{
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();

    public void Emit(float x, float y, Color color, int count, float speed = 40f)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float spd = speed * (0.5f + (float)_rng.NextDouble());
            _particles.Add(new Particle
            {
                X = x, Y = y,
                Vx = (float)Math.Cos(angle) * spd,
                Vy = (float)Math.Sin(angle) * spd,
                Life = 0.4f + (float)_rng.NextDouble() * 0.3f,
                Color = color,
                Size = 3f + (float)_rng.NextDouble() * 4f
            });
        }
    }

    public void EmitDrift(float x, float y, int steerDir, float speed, float intensity = 1f)
    {
        int count = (int)(4 + speed * 6 * intensity);
        float driftAngle = steerDir < 0 ? 0.35f : steerDir > 0 ? -0.35f : 0f;
        for (int i = 0; i < count; i++)
        {
            float spread = (float)(_rng.NextDouble() - 0.5) * 0.8f;
            float angle = (float)Math.PI / 2 + driftAngle + spread;
            float spd = 25f + (float)_rng.NextDouble() * 55f * speed;
            bool hot = _rng.NextDouble() < 0.25;
            _particles.Add(new Particle
            {
                X = x + (float)(_rng.NextDouble() - 0.5) * 10f,
                Y = y + (float)(_rng.NextDouble() - 0.5) * 4f,
                Vx = (float)Math.Cos(angle) * spd,
                Vy = (float)Math.Sin(angle) * spd * 0.6f,
                Life = 0.25f + (float)_rng.NextDouble() * 0.45f,
                Color = hot ? Color.FromArgb(220, 255, 200, 80) : Color.FromArgb(200, 210, 210, 210),
                Size = 2f + (float)_rng.NextDouble() * 5f * intensity
            });
        }
    }

    public void EmitNitroTrail(float x, float y, float intensity = 1f)
    {
        int count = (int)(3 + intensity * 6);
        for (int i = 0; i < count; i++)
        {
            _particles.Add(new Particle
            {
                X = x + (float)(_rng.NextDouble() - 0.5) * 22f,
                Y = y + (float)_rng.NextDouble() * 10f,
                Vx = (float)(_rng.NextDouble() - 0.5) * 35f,
                Vy = 40f + (float)_rng.NextDouble() * 70f * intensity,
                Life = 0.15f + (float)_rng.NextDouble() * 0.25f,
                Color = i % 2 == 0 ? Color.FromArgb(200, 0, 220, 255) : Color.FromArgb(160, 200, 255, 255),
                Size = 2f + (float)_rng.NextDouble() * 4f * intensity
            });
        }
    }

    public void EmitNitroBurst(float x, float y)
    {
        for (int i = 0; i < 24; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float spd = 60f + (float)_rng.NextDouble() * 100f;
            _particles.Add(new Particle
            {
                X = x, Y = y,
                Vx = (float)Math.Cos(angle) * spd,
                Vy = (float)Math.Sin(angle) * spd,
                Life = 0.35f + (float)_rng.NextDouble() * 0.3f,
                Color = Color.FromArgb(220, 100, 230, 255),
                Size = 3f + (float)_rng.NextDouble() * 5f
            });
        }
    }

    public void EmitRankUp(float cx, float cy, string rank, int combo)
    {
        Color core = rank switch
        {
            "S" => Color.FromArgb(255, 80, 255, 255),
            "A" => Color.FromArgb(255, 255, 220, 60),
            "B" => Color.FromArgb(255, 200, 220, 255),
            _ => Color.FromArgb(255, 220, 220, 220)
        };
        int count = rank == "S" ? 48 : rank == "A" ? 32 : 20;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float spd = rank == "S" ? 80f + (float)_rng.NextDouble() * 160f : 50f + (float)_rng.NextDouble() * 100f;
            _particles.Add(new Particle
            {
                X = cx + (float)(_rng.NextDouble() - 0.5) * 40f,
                Y = cy + (float)(_rng.NextDouble() - 0.5) * 20f,
                Vx = (float)Math.Cos(angle) * spd,
                Vy = (float)Math.Sin(angle) * spd,
                Life = 0.5f + (float)_rng.NextDouble() * 0.6f,
                Color = i % 3 == 0 ? core : Color.FromArgb(220, 255, 255, 255),
                Size = rank == "S" ? 3f + (float)_rng.NextDouble() * 6f : 2f + (float)_rng.NextDouble() * 4f
            });
        }
        if (combo > 1)
        {
            for (int i = 0; i < combo * 3; i++)
            {
                _particles.Add(new Particle
                {
                    X = cx, Y = cy - 30,
                    Vx = (float)(_rng.NextDouble() - 0.5) * 120f,
                    Vy = -40f - (float)_rng.NextDouble() * 80f,
                    Life = 0.7f + (float)_rng.NextDouble() * 0.4f,
                    Color = Color.FromArgb(200, 255, 180, 0),
                    Size = 2f + (float)_rng.NextDouble() * 3f
                });
            }
        }
    }

    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Vy += 40f * dt;
            p.Life -= dt;
            if (p.Life <= 0) _particles.RemoveAt(i);
        }
    }

    public void Render(Graphics g)
    {
        foreach (var p in _particles)
        {
            int alpha = (int)(255 * Math.Clamp(p.Life / 0.5f, 0, 1));
            using var brush = new SolidBrush(Color.FromArgb(alpha, p.Color));
            g.FillEllipse(brush, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
        }
    }

    public void Clear() => _particles.Clear();
}
