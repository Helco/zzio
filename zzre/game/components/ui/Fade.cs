namespace zzre.game.components.ui;

public record struct Fade(
    float From, float To,
    float StartDelay,
    float InDuration,
    float SustainDelay,
    float OutDuration,
    float CurrentTime = 0f)
{
    public static Fade SingleIn(float duration) => new(
        From: 0f,
        To: 0.8f,
        StartDelay: 0f,
        InDuration: duration,
        SustainDelay: float.PositiveInfinity,
        OutDuration: 1f); // because -INF / +INF == NaN
    public static Fade SingleOut(float duration) => new(
        From: 0f,
        To: 0.8f,
        StartDelay: 0f,
        InDuration: 0f,
        SustainDelay: 0f,
        OutDuration: duration);

    public static readonly Fade StdIn = SingleIn(1.5f);
    public static readonly Fade StdOut = SingleOut(0.8f);

    public float Value => IsFadedIn
        ? MathEx.Lerp(To, From, CurrentTime, StartDelay + InDuration + SustainDelay, OutDuration)
        : MathEx.Lerp(From, To, CurrentTime, StartDelay, InDuration);

    public bool IsFadedIn => CurrentTime >= StartDelay + InDuration;
    public bool IsFinished => CurrentTime >= StartDelay + InDuration + SustainDelay + OutDuration;
}
