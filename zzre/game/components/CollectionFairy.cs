namespace zzre.game.components;

public readonly record struct CollectionFairy(float VerticalFrequency)
{
    public const float MinVerticalFreq = 60f;
    public const float VerticalAmpl = 40f;

    public static CollectionFairy Random =>
        new(System.Random.Shared.NextFloat() * VerticalAmpl + MinVerticalFreq);
}
