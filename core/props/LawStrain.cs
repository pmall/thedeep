using System;

namespace TheDeep;

/// <summary>Which side of a law the forbidden way lies on; the values are the direction along X (east +1, west −1).</summary>
public enum LawSide
{
    /// <summary>The forbidden side lies to the west.</summary>
    West = -1,

    /// <summary>The forbidden side lies to the east.</summary>
    East = 1,
}

/// <summary>Where a law stands (<see cref="LawStrain"/>).</summary>
public enum LawState
{
    /// <summary>Holding.</summary>
    Intact,

    /// <summary>Just broken: the veil is scattering and the words going out.</summary>
    Breaking,

    /// <summary>Broken for good (also a law broken on an earlier visit).</summary>
    Broken,
}

/// <summary>The numbers of a <see cref="LawStrain"/> (lengths in meters, times in seconds, rates per second).</summary>
public readonly record struct LawStrainTuning(
    float Near, float Lip, float BehindLip, float Height, float PushStrain, float PushTime, float ShowRate, float ReleaseRate, float BreakTime);

/// <summary>
/// The rules of a law the pilgrim breaks by an act, pure (docs/gameplay.md#law-act): where the pilgrim stands decides whether the
/// law's words show, whether pressing on strains it (up to <see cref="LawStrainTuning.PushStrain"/>, never breaking it), when the
/// pilgrim refuses aloud (once) and so offers the act at the lip; the act held full breaks it, and the breaking runs
/// <see cref="LawStrainTuning.BreakTime"/> seconds.
/// </summary>
public sealed class LawStrain
{
    private readonly LawStrainTuning _tuning;
    private float _push;
    private float _brokeFor;

    /// <summary>An intact law.</summary>
    public LawStrain(LawStrainTuning tuning) => _tuning = tuning;

    /// <summary>Holding, breaking or broken.</summary>
    public LawState State { get; private set; } = LawState.Intact;

    /// <summary>The veil is gone and the way open (breaking or broken).</summary>
    public bool Broken => State != LawState.Intact;

    /// <summary>The pilgrim has said why it may not, this visit: the act is offered at the lip.</summary>
    public bool Refused { get; private set; }

    /// <summary>The pilgrim stands at the lip, upright and in control: pressing on strains, the act is offered (if refused).</summary>
    public bool AtLip { get; private set; }

    /// <summary>0 to 1 while pressing against the law (never above the push strain) or holding the act.</summary>
    public float Strain { get; private set; }

    /// <summary>0 to 1: how far the words have risen as the pilgrim nears.</summary>
    public float Shown { get; private set; }

    /// <summary>0 to 1 while the law breaks: how far the veil and words are gone.</summary>
    public float Gone => State switch
    {
        LawState.Intact => 0f,
        LawState.Breaking => Math.Clamp(_brokeFor / _tuning.BreakTime, 0f, 1f),
        _ => 1f,
    };

    /// <summary>The act offered at the lip should show now.</summary>
    public bool Offering => Refused && AtLip;

    /// <summary>
    /// Advances an intact law by <paramref name="dt"/> seconds. <paramref name="away"/> is how far the pilgrim stands from the lip,
    /// meters, positive on the pilgrim's side (infinity when there is none); <paramref name="standing"/> is it upright, in control,
    /// on the floor and level with the lip; <paramref name="pushing"/> is it pressing toward the forbidden side;
    /// <paramref name="hold"/> is how full the act's hold is. Returns true the one time the pilgrim refuses aloud.
    /// </summary>
    public bool Step(float dt, float away, bool standing, bool pushing, float hold)
    {
        AtLip = standing && away > -_tuning.BehindLip && away < _tuning.Lip;
        bool pressing = AtLip && pushing;
        Shown = Motion.Approach(Shown, away < _tuning.Near && away > -1f ? 1f : 0f, dt * _tuning.ShowRate);
        float rate = _tuning.PushStrain / _tuning.PushTime;
        _push = Math.Clamp(_push + (pressing ? dt : -dt * _tuning.ReleaseRate) * rate, 0f, _tuning.PushStrain);
        bool refusedNow = !Refused && _push >= _tuning.PushStrain;
        Refused |= refusedNow;
        Strain = Math.Max(_push, hold);
        return refusedNow;
    }

    /// <summary>Advances a broken law by <paramref name="dt"/> seconds.</summary>
    public void Advance(float dt)
    {
        if (State == LawState.Breaking)
        {
            _brokeFor += dt;
            if (_brokeFor >= _tuning.BreakTime)
            {
                State = LawState.Broken;
            }
        }
    }

    /// <summary>The act held full: the law breaks (once).</summary>
    public void Break()
    {
        if (Broken)
        {
            return;
        }

        State = LawState.Breaking;
        _brokeFor = 0f;
        Strain = 1f;
    }

    /// <summary>Broken on an earlier visit: nothing shows.</summary>
    public void Settle()
    {
        State = LawState.Broken;
        Strain = 0f;
    }
}
