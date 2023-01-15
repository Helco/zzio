using System.Numerics;

namespace zzre.game.messages;

public record struct CreateItem(int ItemId, Vector3 Position, int Count = 1);
