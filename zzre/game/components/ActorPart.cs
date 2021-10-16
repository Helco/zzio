namespace zzre.game.components
{
    public readonly struct ActorPart
    {
        public readonly DefaultEcs.Entity ParentActor;

        public ActorPart(DefaultEcs.Entity parentActor) => ParentActor = parentActor;
    }
}
