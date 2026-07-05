using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs.System;
using Serilog;
using zzio;

namespace zzre.game.systems;

public sealed partial class AIMovement : AEntitySetSystem<float>
{
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
    [Configuration(Min = 0.0f, Description = "At which the path movement is reversed")]
    private float PlayerNearDistance = 0.5f;

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
        movement.DidReverse = false;
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
        float moveDistLeft = speed * elapsedTime;

        FindPathResult result;
        if (Vector3.DistanceSquared(playerPos, location.LocalPosition) < PlayerNearDistance * PlayerNearDistance)
            movement.IsPlayerNear = true;
        if (movement.IsPlayerNear)
        {
            result = UpdateOnPath(-1, moveDistLeft, entity, location, ref path, ref movement);
            if (result is FindPathResult.NotFound)
                movement.IsPlayerNear = false; // breaking out of reverse movement
        }
        else
            result = UpdateOnPath(1, moveDistLeft, entity, location, ref path, ref movement);
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
        var newAngle = targetDirAngle + MathF.CopySign(elapsedTime * speed, dirAngleDelta);
        var newAngleDelta = MathEx.NormalizeAngle(targetTargetDirAngle - newAngle);
        if ((newAngleDelta < 0) != (dirAngleDelta < 0))
            newAngle = targetTargetDirAngle;

        targetDir.TargetDirection = MathEx.HorizontalDirection(newAngle);
        location.LookIn(targetDir.TargetDirection);
    }

    private FindPathResult UpdateOnPath(
        int direction,
        float moveDistLeft,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.AIPath path,
        ref components.AIMovement movement)
    {
        if (direction is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(direction));

        if (movement.DidReverse && direction > 0)
            World.Publish(new messages.ResetAIMovement(entity));
        else if (!movement.DidReverse && direction < 0) // we need to reverse movement
        {
            movement.DidReverse = true;
            movement.DirToCurrentWp *= -1f;
            movement.DistMovedToCurWp = Vector3.Distance(path.Waypoints[path.TargetIndex], movement.CurrentPos);
            path.TargetIndex--;
            Debug.Assert(path.TargetIndex >= 0);
        }

        var result = AdvanceOnPath(direction, ref moveDistLeft, entity, location, ref path, ref movement);
        switch(result)
        {
            case FindPathResult.NotThereYet:
                movement.CurrentPos += moveDistLeft * movement.DirToCurrentWp;
                movement.DistMovedToCurWp += moveDistLeft;
                break;
            case FindPathResult.NotFound when direction > 0:
                // set scatter6 state
                movement.TryBailout = true;
                return AdvanceOnPath(direction, ref moveDistLeft, entity, location, ref path, ref movement);
        }
        return result;
    }

    private FindPathResult AdvanceOnPath(
        int direction,
        ref float moveDistLeft,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.AIPath path,
        ref components.AIMovement movement)
    {
        while (moveDistLeft > 0)
        {
            if (!path.Waypoints.IsEmpty && movement.DistLeftToCurWp > moveDistLeft)
                return FindPathResult.NotThereYet;

            // We are at the end of the path
            if (path.Waypoints.IsEmpty || !path.IsInBounds(path.TargetIndex + direction))
            {
                if (direction < 0) // eventually resetting direction
                    return FindPathResult.NotFound;

                movement.DidTimeoutFindingPath = false;
                var lastWaypointId = path.WaypointIds.Count > 0 ? path.WaypointIds[^1] : PathFinder.InvalidId;
                World.Publish(new messages.GenerateAIPath(entity, lastWaypointId));
                
                switch(path.LastResult)
                {
                    case FindPathResult.Success: break;
                    case FindPathResult.Timeout:
                        movement.DidTimeoutFindingPath = true;
                        logger.Warning("Path finder timeout");
                        return FindPathResult.Timeout;
                    default:
                        path.WaypointIds.Clear();
                        return path.LastResult;
                }

                // I skipped a lot of weird original cached/non-cached/smoothing waypoint handling here
            }

            // Switch to next path node
            Debug.Assert(!path.WaypointIds.IsEmpty &&
                path.IsInBounds(path.TargetIndex) &&
                path.IsInBounds(path.TargetIndex + direction));
            moveDistLeft -= movement.DistLeftToCurWp;
            movement.CurrentPos = path.Waypoints[path.TargetIndex];
            path.TargetIndex += direction;
            movement.CurrentEdgeKind = path.EdgeKinds[path.TargetIndex];
            movement.DirToCurrentWp = MathEx.SafeNormalize(path.Waypoints[path.TargetIndex] - movement.CurrentPos);
            movement.DistToCurWp = Vector3.Distance(path.Waypoints[path.TargetIndex], movement.CurrentPos);
            movement.DistMovedToCurWp = 0f;
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
