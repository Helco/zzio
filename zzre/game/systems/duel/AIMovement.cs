using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs.System;
using Serilog;
using zzio;
using zzre.game.components.behaviour;

namespace zzre.game.systems;

public sealed partial class AIMovement : AEntitySetSystem<float>
{
    private const int MinPathLength = 4; // TODO: Is this necessary or configurable?

    [Configuration(Key = "/zanzarah.ai.AI_WIZ_FORM_SPEED")]
    private float WizFormSpeed = 2.0f;
    [Configuration(Key = "/zanzarah.ai.AI_GRAVITY")]
    private float Gravity = -4.0f;
    [Configuration(Key = "/zanzarah.ai.AI_JUMP_POWER")]
    private float JumpPower = 3.0f;
    [Configuration]
    private int ManaPerJump = -500;
    [Configuration]
    private float FloorOffset = -0.2f;

    private readonly ILogger logger;
    private readonly IDisposable configDisposable;
    private readonly IDisposable resetMessageDisposable;

    public AIMovement(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        logger = diContainer.GetLoggerFor<AIMovement>();
        configDisposable = diContainer.GetConfigFor(this);
        resetMessageDisposable = World.Subscribe<messages.ResetAIMovement>(HandleResetMovement);
    }

    public override void Dispose()
    {
        base.Dispose();
        configDisposable.Dispose();
        resetMessageDisposable.Dispose();
    }

    private void HandleResetMovement(in messages.ResetAIMovement msg)
    {
        ref var movement = ref msg.ForEntity.Get<components.AIMovement>();
        movement.DistMovedToCurWp = movement.DistToCurWp = -1f;
        movement.ShouldAdvanceNode = true;
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.AIPath path,
        ref components.AIMovement movement,
        ref components.PuppetActorMovement targetDir,
        ref components.Velocity velocity,
        InventoryFairy invFairy,
        in Sphere colliderSphere)
    {
        if (!FindPlayerFairy(out var playerPos))
            return;

        movement.DidMove = false;
        var speed = WizFormSpeed * invFairy.moveSpeed;
        // TODO: AI Movement: Disable movement in some condition
        // TODO: AI Movement: Slow down sharp turns
        // TODO: AI Movement: Reverse AI movement if player is near
        float moveDistLeft = speed * elapsedTime;
        var result = UpdateForward(moveDistLeft, entity, location, ref path, ref movement);
        if (result is not (FindPathResult.Success or FindPathResult.NotThereYet))
            return;

        var isHinderedByGravity = Move(elapsedTime, speed, location, ref movement, ref velocity, invFairy, in colliderSphere);
        UpdateTargetTargetDir(ref movement, in velocity, isHinderedByGravity, playerPos);
        Rotate(elapsedTime, speed, location, ref movement, ref targetDir);
    }

    private bool Move(
        float elapsedTime,
        float speed,
        Location location,
        ref components.AIMovement movement,
        ref components.Velocity velocity,
        InventoryFairy invFairy,
        in Sphere colliderSphere)
    {
        movement.DidMove = true;
        velocity.Value = speed * movement.DirToCurrentWp;

        if (movement.ShouldJump)
        {
            movement.ShouldJump = false;
            movement.YVelocity = JumpPower;
            Inventory.AddJumpMana(invFairy, ManaPerJump);
        }
        else
            movement.YVelocity += Gravity * elapsedTime;

        var nextPosition = movement.CurrentPos;
        var nextYByGravity = location.LocalPosition.Y + elapsedTime * movement.YVelocity;
        var nextYByMovement = movement.CurrentPos.Y + colliderSphere.Radius + FloorOffset;
        var isHinderedByGravity = nextYByGravity >= nextYByMovement;
        if (isHinderedByGravity)
        {
            nextPosition.Y = nextYByGravity;
            velocity.Value = velocity.Value with { Y = elapsedTime * movement.YVelocity };
        }
        else
        {
            nextPosition.Y = nextYByMovement;
            velocity.Value = velocity.Value with { Y = 0f };
            movement.YVelocity = 0f;
            movement.ShouldJump |= movement.CurrentEdgeKind == WaypointEdgeKind.Jumpable;
            Inventory.AddJumpMana(invFairy, (int)(elapsedTime * 1000f * invFairy.jumpPower));
        }
        location.LocalPosition = nextPosition;
        return isHinderedByGravity;
    }

    private static void UpdateTargetTargetDir(
        ref components.AIMovement movement,
        in components.Velocity velocity,
        bool isHinderedByGravity,
        Vector3 playerPos)
    {
        movement.TargetTargetDir = 0 switch
        {
            // _ when isSpinning => Vector3.Zero,
            _ when isHinderedByGravity => Vector3.Normalize(playerPos - velocity.Value),
            _ => movement.DirToCurrentWp
        };
    }

    private static void Rotate(
        float elapsedTime,
        float speed,
        Location location,
        ref components.AIMovement movement,
        ref components.PuppetActorMovement targetDir)
    {
        var targetDirAngle = MathF.Atan2(targetDir.TargetDirection.X, targetDir.TargetDirection.Z);
        var targetTargetDirAngle = MathF.Atan2(movement.TargetTargetDir.X, movement.TargetTargetDir.Z);
        var dirAngleDelta = MathEx.NormalizeAngle(targetTargetDirAngle - targetDirAngle);
        var newAngle = targetDirAngle + MathF.CopySign(dirAngleDelta, elapsedTime * speed);
        var newAngleDelta = MathEx.NormalizeAngle(targetTargetDirAngle - newAngle);
        if ((newAngleDelta < 0) != (dirAngleDelta < 0))
            newAngle = targetTargetDirAngle;

        targetDir.TargetDirection = MathEx.HorizontalDirection(newAngle);
        location.LookIn(targetDir.TargetDirection);
    }

    private FindPathResult UpdateForward(
        float moveDistLeft,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.AIPath path,
        ref components.AIMovement movement)
    {
        // TODO: Check path reversal
        var result = AdvancePath(ref moveDistLeft, entity, location, ref path, ref movement);
        movement.ShouldAdvanceNode = result is not FindPathResult.NotFound;

        if (result is FindPathResult.NotFound)
        {
            // set scatter6 state
            movement.TryBailout = true;
            result = AdvancePath(ref moveDistLeft, entity, location, ref path, ref movement);
            movement.ShouldAdvanceNode = result is FindPathResult.Success;
        }
        else if (result is FindPathResult.Success or FindPathResult.NotThereYet)
        {
            movement.CurrentPos += moveDistLeft * movement.DirToCurrentWp;
            movement.DistMovedToCurWp += moveDistLeft;
        }
        return result;
    }

    private FindPathResult AdvancePath(
        ref float moveDistLeft,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.AIPath path,
        ref components.AIMovement movement)
    {
        while (moveDistLeft > 0f)
        {
            if (path.HasPath && movement.DistToCurWp - (movement.DistMovedToCurWp + moveDistLeft) > 0f)
                break;
            //if (movement.ShouldAdvanceNode && path.WaypointIds.Count > 2) // TODO: Why 2?
            //    path.CurrentIndex++;

            var needsNewPath = /*path.WaypointIds.Count < MinPathLength || */ !path.HasPath;
            // TODO: Add bailout behavior

            if (needsNewPath)
            {
                movement.DidTimeoutFindingPath = false;
                var lastWaypointId = path.WaypointIds.Count > 0 ? path.WaypointIds[^1] : PathFinder.InvalidId;
                World.Publish(new messages.GenerateAIPath(entity, lastWaypointId));

                if (path.LastResult is FindPathResult.Timeout)
                {
                    movement.DidTimeoutFindingPath = true;
                    logger.Warning("Path find timeout");
                    return FindPathResult.Timeout;
                }

                if (path.LastResult is not FindPathResult.Success)
                {
                    path.WaypointIds.Clear();
                    return path.LastResult;
                }

                // I skipped a lot of weird original cached/non-cached/smoothing waypoint handling here
            }

            Debug.Assert(path.WaypointIds.Count > 0 && path.CurrentIndex + 1 < path.WaypointIds.Count);
            moveDistLeft -= Vector3.Distance(path.Waypoints[path.CurrentIndex], movement.CurrentPos); // rest distance
            movement.CurrentPos = path.Waypoints[path.CurrentIndex];
            path.CurrentIndex++;
            movement.CurrentEdgeKind = path.EdgeKinds[path.CurrentIndex];
            movement.DirToCurrentWp = MathEx.SafeNormalize(path.Waypoints[path.CurrentIndex] - movement.CurrentPos);
            movement.DistToCurWp = Vector3.Distance(path.Waypoints[path.CurrentIndex], movement.CurrentPos);
            movement.DistMovedToCurWp = 0f;

            // TODO: Investigate wheter DistToCurWp can be zero upon switch
        }
        return FindPathResult.NotThereYet;
    }


    private bool FindPlayerFairy(out Vector3 playerPosition)
    {
        var playerEntity = World.Get<components.PlayerEntity>().Entity;
        var playerFairy = playerEntity.Get<components.DuelParticipant>().ActiveFairy;
        playerPosition = playerFairy.IsAlive ? playerFairy.Get<Location>().GlobalPosition : default;
        return playerFairy.IsAlive;
    }
}
