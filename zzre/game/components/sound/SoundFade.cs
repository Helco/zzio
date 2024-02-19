namespace zzre.game.components;

public struct SoundFade(float fromVol, float toVol, float length, float delay = 0f)
{
    public readonly float FromVolume = fromVol;
    public readonly float ToVolume = toVol;
    public readonly float Length = length;
    public float Delay = delay;
    public float Time = 0f;

    public static SoundFade In(float toVol, float length, float delay = 0f) => new(0f, toVol, length, delay);
    public static SoundFade Out(float fromVol, float length, float delay = 0f) => new(fromVol, 0f, length, delay);
}
