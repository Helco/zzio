
namespace zzre.game.components;

public struct DialogChestPuzzle
{
    public DefaultEcs.Entity DialogEntity;
    public int Size;
    public int LabelExit;
    public uint NumAttempts;
    public bool LockBoard;
    public Rect BgRect;
    public DefaultEcs.Entity[] Cells;
    public DefaultEcs.Entity Attempts;
    public DefaultEcs.Entity Action;
}
