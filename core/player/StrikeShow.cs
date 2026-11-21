using System;
using static TheDeep.Motion;

namespace TheDeep;

/// <summary>
/// How a sword strike shows on the body, Godot-free: how far into its clip the body is (<see cref="Progress"/>, the strike's
/// own progress, so the animation fits the gameplay timing of docs/gameplay.md#fight), how much it covers the rest of the
/// body (<see cref="Weight"/>: whole through the strike and the hold of its end pose, then letting go), and the blade:
/// out of the forearm (<see cref="Blade"/>) and leaving its slash (<see cref="Edge"/>).
/// </summary>
public sealed class StrikeShow
{
    /// <summary>How long the end pose holds after a strike, seconds; the finisher, the heaviest, holds longest.</summary>
    private const float Hold = 0.1f;
    private const float FinisherHold = 0.22f;

    /// <summary>How long letting go of the end pose takes, seconds.</summary>
    private const float LetGo = 0.16f;

    /// <summary>How fast the blade springs out and folds away, 1/s.</summary>
    private const float BladeOut = 45f;
    private const float BladeIn = 14f;

    private float _windUpEnd = 0.15f;
    private float _activeEnd = 0.5f;
    private bool _active;
    private float _released = float.MaxValue;

    /// <summary>The strike shown (or the last one, while it lets go).</summary>
    public StrikeKind Kind { get; private set; }

    /// <summary>How far through the strike, 0 to 1 (1 through the hold).</summary>
    public float Progress { get; private set; } = 1f;

    /// <summary>How much the strike covers the body, 0 to 1.</summary>
    public float Weight { get; private set; }

    /// <summary>How far the blade is out of the forearm, 0 (folded away) to 1.</summary>
    public float Blade { get; private set; }

    /// <summary>How strongly the blade's sweep leaves a trail now, 0 to 1: through the cut and its follow-through.</summary>
    public float Edge { get; private set; }

    /// <summary>
    /// A strike under way this tick: <paramref name="progress"/> through it, its wind-up ending at <paramref name="windUpEnd"/>
    /// and its hit window at <paramref name="activeEnd"/> (shares of the whole).
    /// </summary>
    public void Strike(float progress, float windUpEnd, float activeEnd, StrikeKind kind)
    {
        _active = true;
        Kind = kind;
        Progress = Math.Clamp(progress, 0f, 1f);
        _windUpEnd = Math.Clamp(windUpEnd, 0.01f, 0.9f);
        _activeEnd = Math.Clamp(activeEnd, _windUpEnd + 0.01f, 0.99f);
    }

    /// <summary>The strike is over: the body holds its end pose a moment, then lets go.</summary>
    public void End()
    {
        if (_active)
        {
            _active = false;
            _released = 0f;
            Progress = 1f;
        }
    }

    /// <summary>No strike shown at all, at once: the blade folded away and no trail.</summary>
    public void Reset()
    {
        _active = false;
        _released = float.MaxValue;
        Progress = 1f;
        Weight = 0f;
        Blade = 0f;
        Edge = 0f;
    }

    /// <summary>Advances by <paramref name="delta"/> seconds.</summary>
    public void Update(float delta)
    {
        float dt = MathF.Max(delta, 0f);
        float hold = Kind == StrikeKind.Finisher ? FinisherHold : Hold;
        if (_active)
        {
            Weight = 1f;
            // Laid from the coil to the end of the follow-through, not after: settling into the hold turns the blade back, and a
            // trail laid over itself piles into a bright spot.
            Edge = SmoothStep(_windUpEnd - 0.01f, _windUpEnd + 0.04f, Progress) * (1f - SmoothStep(_activeEnd - 0.1f, _activeEnd, Progress));
        }
        else if (_released < float.MaxValue)
        {
            _released += dt;
            Weight = 1f - SmoothStep(hold, hold + LetGo, _released);
            Edge = 0f;
            if (Weight <= 0f)
            {
                _released = float.MaxValue;
            }
        }
        else
        {
            Weight = 0f;
            Edge = 0f;
        }

        bool drawn = _active || _released < hold + LetGo * 0.4f;
        Blade = Damp(Blade, drawn ? 1f : 0f, drawn ? BladeOut : BladeIn, dt);
        if (_active)
        {
            Blade = MathF.Max(Blade, SmoothStep(0f, _windUpEnd * 0.8f, Progress)); // all out by the coil, however short the wind-up
        }

        if (!drawn && Blade < 0.002f)
        {
            Blade = 0f;
        }
    }
}
