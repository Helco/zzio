namespace zzre.game.components;

public struct SoundFade(float fromVol, float toVol, float length)
{
    public readonly float FromVolume = fromVol;
    public readonly float ToVolume = toVol;
    public readonly float Length = length;
    public float Time = 0f;

    public static SoundFade In(float toVol, float length) => new(0f, toVol, length);
    public static SoundFade Out(float fromVol, float length) => new(fromVol, 0f, length);
}
