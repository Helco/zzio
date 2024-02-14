namespace zzre.game.components;
using System;

public struct AnimalWaypointAI
{
    public enum State
    {
        Idle,
        SearchTarget,
        Moving
    }

    public readonly Configuration Config;
    public State CurrentState;
    public float CurrentSpeed;
    public float DistanceToWP;
    public float MovedDistance;
    public float CurIdleTime;
    public zzio.scn.Trigger? LastWaypoint, CurrentWaypoint;
    public zzio.AnimationType WalkAnimation;

    public AnimalWaypointAI(Configuration config)
    {
        Config = config;
        CurrentState = State.SearchTarget;
        CurrentSpeed = Config.NormalSpeed;
        DistanceToWP = MovedDistance = 0f;
        CurIdleTime = 0f;
        LastWaypoint = CurrentWaypoint = null;
        WalkAnimation = zzio.AnimationType.Walk0;
    }

    public class Configuration
    {
        public bool Flees { get; init; }
        public bool Crawls { get; init; }
        public bool OrientsToGround { get; init; }
        public bool FullAnimationCycles { get; init; }
        public float MaxIdleTime { get; init; }
        public float NormalSpeed { get; init; } = 10f;

        public static readonly Configuration Chicken = new()
        {
            Flees = true,
            Crawls = true,
            MaxIdleTime = 6f,
            NormalSpeed = 2.1f
        };

        public static readonly Configuration Rabbit = new()
        {
            Crawls = true,
            FullAnimationCycles = true,
            MaxIdleTime = 5f,
            NormalSpeed = 2.1f
        };

        public static readonly Configuration Bug = new()
        {
            Crawls = true,
            OrientsToGround = true,
            NormalSpeed = 0.2f
        };

        public static readonly Configuration Firefly = new()
        {
            MaxIdleTime = Random.Shared.NextFloat(),
            NormalSpeed = Random.Shared.NextFloat() * 3f
        };

        public static readonly Configuration Frog = new()
        {
            Crawls = true,
            OrientsToGround = true,
            FullAnimationCycles = true,
            MaxIdleTime = 5f,
            NormalSpeed = 1.4f
        };

        public static readonly Configuration Dragonfly = new()
        {
            MaxIdleTime = 4f,
            NormalSpeed = 5f
        };

        public static readonly Configuration BlackPixie = new()
        {
            Crawls = true,
            FullAnimationCycles = true,
            MaxIdleTime = 5f,
            NormalSpeed = 2.1f
        };
    }
}
