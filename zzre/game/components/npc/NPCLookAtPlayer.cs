namespace zzre.game.components;

public struct NPCLookAtPlayer
{
    public enum Mode
    {
        Hard,
        Smooth,
        Billboard
    }

    public readonly Mode RotationMode;
    public float TimeLeft;

    public NPCLookAtPlayer(Mode rotationMode, float duration) => (RotationMode, TimeLeft) = (rotationMode, duration);
}
