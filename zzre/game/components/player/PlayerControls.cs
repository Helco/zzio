namespace zzre.game.components;

public struct PlayerControls
{
    public bool GoesForward;
    public bool GoesBackward;
    public bool GoesLeft;
    public bool GoesRight;
    public bool Jumps;
    public bool WhirlJumps; // not actually set by input...
    public bool SwitchesSpells;
    public int FrameCountShooting;

    public readonly bool Shoots => FrameCountShooting > 0;
    public readonly bool GoesAnywhere => GoesForward || GoesBackward || GoesRight || GoesLeft;
}
