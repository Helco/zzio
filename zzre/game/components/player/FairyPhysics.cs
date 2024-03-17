namespace zzre.game.components;

public struct FairyPhysics
{
    public bool IsRunning;
    public bool HitFloor;
    public bool HitCeiling;

    public static readonly FairyPhysics Default = new FairyPhysics()
    {
    };
}
