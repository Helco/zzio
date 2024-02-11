using System;
using DefaultEcs.System;
using Silk.NET.SDL;

namespace zzre.game.systems;

public class PlayerControls : AComponentSystem<float, components.PlayerControls>
{
    private const float JumpLockDuration = 0.8f;
    private const KeyCode ForwardKey = KeyCode.KW;
    private const KeyCode BackwardKey = KeyCode.KS;
    private const KeyCode LeftKey = KeyCode.KA;
    private const KeyCode RightKey = KeyCode.KD;
    private const KeyCode JumpKey = KeyCode.KSpace;
    private const KeyCode MenuKey = KeyCode.KReturn;
    // private const KeyCode PauseKey = KeyCode.F1;
    private const KeyCode RuneMenuKey = KeyCode.KF2;
    private const KeyCode BookMenuKey = KeyCode.KF3;
    private const KeyCode MapMenuKey = KeyCode.KF4;
    private const KeyCode DeckMenuKey = KeyCode.KF5;
    // private const KeyCode EscapeKey = KeyCode.Escape;
    private readonly IZanzarahContainer zzContainer;
    private readonly UI ui;
    private readonly IDisposable lockMessageSubscription;

    private bool stuckMovingForward;
    private float lockTimer;
    private float jumpLockTimer;
    private bool jumpChanged;
    private components.PlayerControls nextControls;

    public bool IsLocked => lockTimer > 0;

    public PlayerControls(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>())
    {
        diContainer.AddTag(this);
        World.SetMaxCapacity<components.PlayerControls>(1);
        lockMessageSubscription = World.Subscribe<messages.LockPlayerControl>(HandleLockPlayerControl);
        ui = diContainer.GetTag<UI>();
        zzContainer = diContainer.GetTag<IZanzarahContainer>();
        zzContainer.OnKeyDown += HandleKeyDown;
        zzContainer.OnKeyUp += HandleKeyUp;
    }

    public override void Dispose()
    {
        base.Dispose();
        zzContainer.OnKeyDown -= HandleKeyDown;
        zzContainer.OnKeyUp -= HandleKeyUp;
        lockMessageSubscription.Dispose();
    }

    private void HandleLockPlayerControl(in messages.LockPlayerControl msg)
    {
        if (msg == messages.LockPlayerControl.Unlock)
            lockTimer = 0f;
        else if (msg.Duration > lockTimer)
        {
            lockTimer = msg.Duration;
            stuckMovingForward = msg.MovingForward;
        }
    }

    protected override void Update(float elapsedTime, ref components.PlayerControls component)
    {
        lockTimer = Math.Max(0f, lockTimer - elapsedTime);
        if (IsLocked)
        {
            component = new components.PlayerControls() with { GoesForward = stuckMovingForward };
            return;
        }

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

    private void HandleKeyDown(KeyCode obj) => HandleKey(obj, true);
    private void HandleKeyUp(KeyCode obj) => HandleKey(obj, false);
    private void HandleKey(KeyCode KeyCode, bool isDown)
    {
        switch (KeyCode)
        {
            case ForwardKey: nextControls.GoesForward = isDown; break;
            case BackwardKey: nextControls.GoesBackward = isDown; break;
            case LeftKey: nextControls.GoesLeft = isDown; break;
            case RightKey: nextControls.GoesRight = isDown; break;

            case JumpKey: HandleJump(isDown); break;
        }

        if (isDown && !IsLocked)
        {
            switch (KeyCode)
            {
                case MenuKey: ui.Publish<messages.ui.OpenDeck>(); break;
                case RuneMenuKey: ui.Publish<messages.ui.OpenRuneMenu>(); break;
                case BookMenuKey: ui.Publish<messages.ui.OpenBookMenu>(); break;
                case MapMenuKey: ui.Publish<messages.ui.OpenMapMenu>(); break;
                case DeckMenuKey: ui.Publish<messages.ui.OpenDeck>(); break;
            }
        }
    }

    private void HandleJump(bool isDown)
    {
        if (isDown == nextControls.Jumps)
            return;
        if (!isDown)
        {
            nextControls.Jumps = isDown;
            jumpChanged = true;
        }
        else if (jumpLockTimer <= 0f)
        {
            jumpLockTimer = JumpLockDuration;
            nextControls.Jumps = true;
            jumpChanged = true;
        }
    }
}
