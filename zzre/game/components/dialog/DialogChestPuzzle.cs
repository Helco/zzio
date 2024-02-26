
namespace zzre.game.components;

public struct DialogChestPuzzle
{
    public DefaultEcs.Entity DialogEntity;
    public int Size;
    public int LabelExit;
    public int Attempts;
    public int MinTries;
    public bool[] BoardState;
    public DefaultEcs.Entity Board;
}
