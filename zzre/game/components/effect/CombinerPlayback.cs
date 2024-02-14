namespace zzre.game.components.effect;

public struct CombinerPlayback(
    float duration,
    bool depthTest = true)
{
    public float
        CurTime = 0f,
        CurProgress = 100f,
        Length = 1f;
    public readonly float Duration = duration; // set to infinite to loop
    public readonly bool DepthTest = depthTest;
    public bool IsFinished => CurTime >= Duration;
    public bool IsRunning => !IsFinished && !MathEx.CmpZero(CurProgress);
    public bool IsLooping => Duration == float.PositiveInfinity;
}
