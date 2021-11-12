using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class PlantWiggle : AEntitySetSystem<float>
    {
        private const float MaxCameraDistance = 6f;
        private const float MinColliderSize = 0.5f;
        private const float PlayerDuration = 3f;
        private const float NormalSpeed = 30f;
        private const float PlayerSpeed = 10f;
        private const float PlayerAmplitudeFactor = 0.5f;
        private static readonly Vector3 PlayerDistanceShift = new Vector3(0f, 0.7f, 0f);

        private readonly IDisposable addComponentSubscription;
        private Location playerLocation => playerLocationLazy.Value;
        private readonly Lazy<Location> playerLocationLazy;
        private readonly Location cameraLocation;
        private readonly GameTime gameTime;

        public PlantWiggle(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            var game = diContainer.GetTag<Game>();
            gameTime = diContainer.GetTag<GameTime>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
            cameraLocation = diContainer.GetTag<rendering.Camera>().Location;
            addComponentSubscription = World.SubscribeComponentAdded<components.PlantWiggle>(HandleComponentAdded);
        }

        public override void Dispose()
        {
            base.Dispose();
            addComponentSubscription.Dispose();
        }

        private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.PlantWiggle value)
        {
            var location = entity.Get<Location>();
            entity.Set(value with { StartRotation = location.LocalRotation });
        }

        [Update]
        private void Update(
            float elapsedTime,
            in Location plantLocation,
            in Sphere collider,
            ref components.PlantWiggle wiggle)
        {
            if (wiggle.RemainingTimer > 0f)
                wiggle.RemainingTimer -= elapsedTime;

            var camDist = Vector3.Distance(plantLocation.LocalPosition, cameraLocation.LocalPosition);
            var playerDist = Vector3.DistanceSquared(
                playerLocation.LocalPosition,
                plantLocation.LocalPosition + PlayerDistanceShift);
            if (camDist < MaxCameraDistance && collider.Radius > MinColliderSize)
            {
                if (playerDist > collider.Radius * collider.Radius)
                    wiggle.RemainingTimer = Math.Max(wiggle.RemainingTimer, 0f);
                else if (MathEx.CmpZero(wiggle.RemainingTimer))
                {
                    // TODO: Play plant wiggle sound
                    wiggle.RemainingTimer = PlayerDuration;
                }
            }

            var (speed, amplitude) = wiggle.RemainingTimer > 0
                ? (PlayerSpeed, Vector2.One * wiggle.RemainingTimer * PlayerAmplitudeFactor)
                : (NormalSpeed, wiggle.Amplitude);
            var currentTime = speed * (gameTime.TotalElapsed + wiggle.Delay);
            wiggle.Angles +=
                new Vector2(MathF.Sin(currentTime), MathF.Cos(currentTime)) *
                amplitude *
                MathEx.DegToRad;

            plantLocation.LocalRotation = wiggle.StartRotation *
                Quaternion.CreateFromYawPitchRoll(wiggle.Angles.Y, wiggle.Angles.X, roll: 0f);
        }
    }
}
