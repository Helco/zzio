namespace zzre.game.components;

// this component is on the ECS world
// currently different gameflow systems can update on the same frame depending on the system order
// this is not ideal but seems to work for now...

public enum GameFlow
{
    Normal,
    GotCard,
    Doorway,
    UnlockDoor,
    Teleporter
}
