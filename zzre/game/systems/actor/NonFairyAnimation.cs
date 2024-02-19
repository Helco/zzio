namespace zzre.game.systems;
using System;
using System.Collections.Generic;
using DefaultEcs.System;

public partial class NonFairyAnimation : AEntitySetSystem<float>
{
    private const float SmithCycleDuration = 1.2f;
    private const float AltIdleCycleDuration = 6f;

    private readonly IDisposable switchDisposable;

    public NonFairyAnimation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        switchDisposable = World.Subscribe<messages.SwitchAnimation>(HandleSwitchAnimation);
    }

    public override void Dispose()
    {
        base.Dispose();
        switchDisposable?.Dispose();
    }

    [Update]
    private void Update(
        float elapsedTime,
        in components.ActorParts actorParts,
        ref components.NonFairyAnimation animation)
    {
        var bodySkeleton = actorParts.Body.Get<Skeleton>();
        var pool = actorParts.Body.Get<components.AnimationPool>();

        animation.Timer += elapsedTime;
        if (animation.Next == animation.Current)
            Maintain(actorParts.Body, bodySkeleton, pool, ref animation);
        else
            Switch(bodySkeleton, pool, ref animation);
    }

    private void Maintain(
        in DefaultEcs.Entity body,
        Skeleton bodySkeleton,
        in components.AnimationPool pool,
        ref components.NonFairyAnimation animation)
    {
        switch (animation.Current)
        {
            case zzio.AnimationType.Idle0 when
                animation.CanUseAlternativeIdles &&
                animation.Timer > AltIdleCycleDuration:
                var attemptAni = Random.Shared.NextSign() <= 0
                    ? zzio.AnimationType.Idle2
                    : zzio.AnimationType.Idle1;
                if (pool.Contains(attemptAni))
                    animation.Next = attemptAni;
                break;

            case zzio.AnimationType.Smith when animation.Timer > SmithCycleDuration:
                animation.Timer = 0f;
                World.Publish(new messages.SpawnSample(
                    "resources/audio/sfx/specials/_s036.wav",
                    Position: body.Get<Location>().LocalPosition));
                bodySkeleton.BlendToAnimation(pool[zzio.AnimationType.Smith], 0f, loop: false);
                break;

            case zzio.AnimationType.ThudGround when bodySkeleton.Animation == null:
                animation.Next = zzio.AnimationType.Idle0;
                break;

            case zzio.AnimationType.Talk0 when bodySkeleton.Animation == null:
                bodySkeleton.BlendToAnimation(pool[zzio.AnimationType.Idle0], 0.2f, loop: true);
                break;

            case zzio.AnimationType.Idle1 when bodySkeleton.Animation == null:
            case zzio.AnimationType.Idle2 when bodySkeleton.Animation == null:
                animation.Timer = components.NonFairyAnimation.RandomStartTimer(Random.Shared);
                bodySkeleton.BlendToAnimation(pool[zzio.AnimationType.Idle0], 0.2f, loop: true);
                break;
        }
    }

    private void HandleSwitchAnimation(in messages.SwitchAnimation message)
    {
        var bodyEntity = message.Entity.Get<components.ActorParts>().Body;
        var bodySkeleton = bodyEntity.Get<Skeleton>();
        var bodyPool = bodyEntity.Get<components.AnimationPool>();
        ref var animation = ref message.Entity.Get<components.NonFairyAnimation>();

        animation.Next = message.Animation;
        Switch(bodySkeleton, bodyPool, ref animation);
    }

    private static void Switch(
        Skeleton bodySkeleton,
        in components.AnimationPool pool,
        ref components.NonFairyAnimation animation)
    {
        var next = animation.Next;
        float blendDuration;
        bool loops = true;

        switch (animation.Next)
        {
            case zzio.AnimationType _ when SimpleSwitches.TryGetValue(animation.Next, out var simple):
                blendDuration = simple.BlendDuration;
                loops = simple.Loops;
                if (simple.ResetsTimer)
                    animation.Timer = 0f;
                break;

            case zzio.AnimationType.Idle0:
                blendDuration = animation.Current == zzio.AnimationType.FlyRight ? 0.5f : 0.2f;
                animation.Timer = components.NonFairyAnimation.RandomStartTimer(Random.Shared);
                break;

            case zzio.AnimationType.Run:
            case zzio.AnimationType.RunForwardLeft:
            case zzio.AnimationType.RunForwardRight:
            case zzio.AnimationType.Back:
            case zzio.AnimationType.Right:
            case zzio.AnimationType.Left:
                next = zzio.AnimationType.Run;
                blendDuration = animation.Current switch
                {
                    zzio.AnimationType.Walk0 => 0.1f,
                    zzio.AnimationType.Walk1 => 0.4f,
                    zzio.AnimationType.Idle0 => 0.2f,
                    zzio.AnimationType.Fall => 0.1f,
                    _ => 0f
                };
                break;

            default: throw new NotSupportedException($"Unsupported animation {animation.Next}");
        }

        if (animation.Current == (zzio.AnimationType)(-1))
            blendDuration = 0f;
        bodySkeleton.BlendToAnimation(pool[next], blendDuration, loops);
        animation.Current = animation.Next;
    }

    private readonly struct SimpleSwitch
    {
        public readonly float BlendDuration;
        public readonly bool Loops;
        public readonly bool ResetsTimer;

        public SimpleSwitch(float blendDuration, bool loops, bool resetsTimer) =>
            (BlendDuration, Loops, ResetsTimer) = (blendDuration, loops, resetsTimer);
    }
    private static readonly IReadOnlyDictionary<zzio.AnimationType, SimpleSwitch> SimpleSwitches = new Dictionary<zzio.AnimationType, SimpleSwitch>()
    {
        { zzio.AnimationType.Jump,          new SimpleSwitch(0.2f,  loops: true,  resetsTimer: false) },
        { zzio.AnimationType.Dance,         new SimpleSwitch(0.2f,  loops: false,  resetsTimer: false) },
        { zzio.AnimationType.Fall,          new SimpleSwitch(0.2f,  loops: true,  resetsTimer: false) },
        { zzio.AnimationType.Idle1,         new SimpleSwitch(0.2f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Idle2,         new SimpleSwitch(0.2f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Talk0,         new SimpleSwitch(0.2f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Talk1,         new SimpleSwitch(0.2f,  loops: true,  resetsTimer: false) },
        { zzio.AnimationType.Talk2,         new SimpleSwitch(0.5f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Talk3,         new SimpleSwitch(0.5f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Walk0,         new SimpleSwitch(0.16f, loops: true,  resetsTimer: true)  },
        { zzio.AnimationType.Walk1,         new SimpleSwitch(0.2f,  loops: true,  resetsTimer: true)  },
        { zzio.AnimationType.Walk2,         new SimpleSwitch(1.0f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.SpecialIdle0,  new SimpleSwitch(0.2f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.FlyForward,    new SimpleSwitch(0.0f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.FlyBack,       new SimpleSwitch(0.0f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.FlyRight,      new SimpleSwitch(0.5f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Joy,           new SimpleSwitch(0.2f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.ThudGround,    new SimpleSwitch(0.07f, loops: false, resetsTimer: false) },
        { zzio.AnimationType.ThudGround2,   new SimpleSwitch(0.03f, loops: false, resetsTimer: false) },
        { zzio.AnimationType.UseFairyPipe,  new SimpleSwitch(0.5f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.UseSeaShell,   new SimpleSwitch(0.5f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Smith,         new SimpleSwitch(0.2f,  loops: false, resetsTimer: true)  },
        { zzio.AnimationType.Astonished,    new SimpleSwitch(0.3f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Surprise0,     new SimpleSwitch(0.3f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Surprise1,     new SimpleSwitch(0.3f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.Stop,          new SimpleSwitch(0.1f,  loops: false, resetsTimer: false) },
        { zzio.AnimationType.PixieFlounder, new SimpleSwitch(0.3f,  loops: true,  resetsTimer: false) }
    };
}
