using System;
using DefaultEcs.System;
using Veldrid;

namespace zzre.game.systems
{
    public class PlayerControls : AComponentSystem<float, components.PlayerControls>
    {
        private const float JumpLockDuration = 0.8f;
        private const Key ForwardKey = Key.W;
        private const Key BackwardKey = Key.S;
        private const Key LeftKey = Key.A;
        private const Key RightKey = Key.D;
        private const Key JumpKey = Key.Space;
        private readonly IZanzarahContainer zzContainer;

        private float jumpLockTimer;
        private bool jumpChanged;
        private components.PlayerControls nextControls;

        public PlayerControls(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>())
        {
            World.SetMaxCapacity<components.PlayerControls>(1);
            zzContainer = diContainer.GetTag<IZanzarahContainer>();
            zzContainer.OnKeyDown += HandleKeyDown;
            zzContainer.OnKeyUp += HandleKeyUp;
        }

        public override void Dispose()
        {
            base.Dispose();
            zzContainer.OnKeyDown -= HandleKeyDown;
            zzContainer.OnKeyUp -= HandleKeyUp;
        }

        protected override void Update(float elapsedTime, ref components.PlayerControls component)
        {
            jumpLockTimer = Math.Max(0f, jumpLockTimer - elapsedTime);
            component.GoesForward = nextControls.GoesForward;
            component.GoesBackward = nextControls.GoesBackward;
            component.GoesRight = nextControls.GoesRight;
            component.GoesLeft = nextControls.GoesLeft;

            // Jump is weird, it is set by the physics system (both true and false) 
            // so we should override it with care
            if (jumpChanged)
            {
                component.Jumps = nextControls.Jumps;
                jumpChanged = false;
            }
        }

        private void HandleKeyDown(Key obj) => HandleKey(obj, true);
        private void HandleKeyUp(Key obj) => HandleKey(obj, false);
        private void HandleKey(Key key, bool isDown)
        {
            switch(key)
            {
                case ForwardKey: nextControls.GoesForward = isDown; break;
                case BackwardKey: nextControls.GoesBackward = isDown; break;
                case LeftKey: nextControls.GoesLeft = isDown; break;
                case RightKey: nextControls.GoesRight = isDown; break;

                case JumpKey when isDown == nextControls.Jumps:
                    return;
                case JumpKey when !isDown:
                    nextControls.Jumps = false;
                    jumpChanged = true;
                    break;
                case JumpKey when isDown && jumpLockTimer <= 0f:
                    jumpLockTimer = JumpLockDuration;
                    nextControls.Jumps = true;
                    jumpChanged = true;
                    break;
            }
        }
    }
}
