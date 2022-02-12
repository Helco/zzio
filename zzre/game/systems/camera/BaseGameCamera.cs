using System;
using System.Numerics;

namespace zzre.game.systems
{
    public abstract class BaseGameCamera : BaseCamera
    {
        private const float DirectionLerpSpeedFactor = 0.01f;
        private const float MaxMouseMove = 20f;

        protected readonly WorldCollider worldCollider;
        protected Location playerLocation => playerLocationLazy.Value;

        private IDisposable lockPlayerSubscription;
        private readonly Lazy<Location> playerLocationLazy;
        private Vector2 nextMove;
        private float lockTimer;

        protected BaseGameCamera(ITagContainer diContainer) : base(diContainer)
        {
            lockPlayerSubscription = world.Subscribe<messages.LockPlayerControl>(HandleLockPlayerControl);
            worldCollider = diContainer.GetTag<WorldCollider>();
            var game = diContainer.GetTag<Game>();
            zzContainer.OnMouseMove += HandleMouseMove;
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        }

        public override void Dispose()
        {
            base.Dispose();
            zzContainer.OnMouseMove -= HandleMouseMove;
            lockPlayerSubscription.Dispose();
        }

        private void HandleLockPlayerControl(in messages.LockPlayerControl msg) => lockTimer = msg.Duration;

        public override void Update(float elapsedTime)
        {
            lockTimer = Math.Max(0f, lockTimer - elapsedTime);
            if (lockTimer > 0f)
                nextMove = Vector2.Zero;
            if (!IsEnabled)
                return;
            Update(elapsedTime, nextMove);
            nextMove = Vector2.Zero;
        }

        protected abstract void Update(float elapsedTime, Vector2 delta);

        private void HandleMouseMove(Vector2 delta) => nextMove = delta;

        protected float DeadZone(float value, float deadzone) => Math.Abs(value) < deadzone ? 0f : value;

        protected Vector2 ClampMouseMove(Vector2 input) => new Vector2(
            Math.Clamp(input.X, -MaxMouseMove, +MaxMouseMove),
            Math.Clamp(input.Y, -MaxMouseMove, +MaxMouseMove));

        protected void Lerp(float elapsedTime, float lerpSpeed, ref Vector3 newPos, ref Vector3 newDir)
        {
            var oldPos = camera.Location.LocalPosition;
            var oldDir = camera.Location.InnerForward;

            newPos = Vector3.Lerp(newPos, oldPos, MathF.Pow(lerpSpeed, elapsedTime));
            newDir = Vector3.Lerp(newDir, oldDir, MathF.Pow(lerpSpeed * DirectionLerpSpeedFactor, elapsedTime));
            newDir = Vector3.Normalize(newDir);
        }
    }
}
