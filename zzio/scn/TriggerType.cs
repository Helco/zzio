namespace zzio.scn;

public enum TriggerType
{
    Doorway = 0,
    SingleplayerStartpoint,
    MultiplayerStartpoint,
    NpcStartpoint,
    CameraPosition,
    Waypoint,
    StartDuel, // unused
    LeaveDuel, // unused
    NpcAttackPosition,
    FlyArea, // unused
    KillPlayer,
    SetCameraView, // unused
    SavePoint, // unused
    SwampMarker,
    RiverMarker,
    PlayVideo, // unused
    Elevator, // in the executable it is actually called teleporter
    GettingACard, // unused
    Sign,
    GettingPixie, // unused
    UsingPipe, // unused
    DancePlatform, // unused
    LeaveDancePlatform, // unused
    RemoveStoneBlocker, // unused
    RemovePlantBlocker, // unused
    EventCamera,
    Platform,
    CreatePlatforms,
    ShadowLight,
    CreateItems,
    Item,
    Shrink, // unused
    WizformMarker, // unused
                   // RemoveLock, // this name is in the executable, but it messes up with the rest of the types
    IndoorCamera, // unused
    LensFlare, // unused
    FogModifier,
    OpenMagicWaypoints, // unused
    RuneTarget, // no name present in the executable
    Unused38, // no name present in the executable
    Animal,
    AnimalWaypoint,
    SceneOpening, // unused
    CollectionWizform,
    ElementalLock,
    ItemGenerator,
    Escape,
    Jumper,
    RefreshMana, // unused
    StartSubgame, // unused
    TemporaryNpc,
    EffectBeam,
    MultiplayerObserverPosition,
    MultiplayerHealingPool,
    MultiplayerManaPool,
    Ceiling,
    HealAllWizforms,

    Unknown = -1
}