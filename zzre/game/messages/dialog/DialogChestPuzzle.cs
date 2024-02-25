
namespace zzre.game.messages;

public record struct DialogChestPuzzle(
    DefaultEcs.Entity DialogEntity,
    int Size,
    int LabelExit
);
