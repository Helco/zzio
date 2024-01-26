namespace zzre.game.components.ui;

public record struct FlashFade(
    float From, float To,
    float StartDelay,
    float OutDuration,
    float SustainDelay,
    float InDuration,
    float CurrentTime = 0f)
{
    public float Value => IsFadedOut
        ? MathEx.Lerp(To, From, CurrentTime, StartDelay + OutDuration + SustainDelay, InDuration)
        : MathEx.Lerp(From, To, CurrentTime, StartDelay, OutDuration);

    public bool IsFadedOut => CurrentTime >= StartDelay + OutDuration;
    public bool IsFinished => CurrentTime >= StartDelay + OutDuration + SustainDelay + InDuration;
}
