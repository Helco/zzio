using System.Numerics;

namespace zzre.game.components;

// contrary to Location.Parent this does not rotate
public readonly record struct MoveToLocation(Location Parent, Vector3 RelativePosition);
