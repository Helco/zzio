using System;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;

namespace zzre.game.systems
{
    public class PlayerControls : AComponentSystem<float, components.PlayerControls>
    {
        private const Key ForwardKey = Key.Y;
        private const Key BackwardKey = Key.H;
        private const Key LeftKey = Key.G;
        private const Key RightKey = Key.J;
        private const Key JumpKey = Key.Space;
        private readonly IZanzarahContainer zzContainer;
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

        protected override void Update(float state, ref components.PlayerControls component)
        {
            component.GoesForward = nextControls.GoesForward;
            component.GoesBackward = nextControls.GoesBackward;
            component.GoesRight = nextControls.GoesRight;
            component.GoesLeft = nextControls.GoesLeft;
            component.Jumps = nextControls.Jumps;
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
                case JumpKey: nextControls.Jumps = isDown; break;
            }
        }
    }
}
