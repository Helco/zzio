﻿using System;
using System.Numerics;

namespace zzre;

public class Location
{
    public Location? Parent { get; set; }

    public Vector3 LocalPosition { get; set; } = Vector3.Zero;
    public Vector3 LocalScale { get; set; } = Vector3.One;

    private Quaternion _localRotation = Quaternion.Identity;
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set => _localRotation = Quaternion.Normalize(value);
    }

    public Matrix4x4 ParentToLocal
    {
        get =>
            Matrix4x4.CreateScale(LocalScale) *
            Matrix4x4.CreateFromQuaternion(LocalRotation) *
            Matrix4x4.CreateTranslation(LocalPosition);
        set
        {
            if (!Matrix4x4.Decompose(value, out _, out var newRotation, out var newTranslation))
            {
                newRotation = Quaternion.Normalize(
                    Quaternion.CreateFromRotationMatrix(value));
            }
            LocalPosition = newTranslation;
            LocalRotation = newRotation;
        }
    }

    public Matrix4x4 LocalToWorld
    {
        get => ParentToLocal * (Parent?.LocalToWorld ?? Matrix4x4.Identity);
        set => ParentToLocal = Parent == null ? value : value * Parent.WorldToLocal;
    }

    public Matrix4x4 WorldToLocal
    {
        get
        {
            if (!Matrix4x4.Invert(LocalToWorld, out var worldToLocal))
                throw new InvalidOperationException();
            return worldToLocal;
        }
        set
        {
            var localToParent = (Parent?.LocalToWorld ?? Matrix4x4.Identity) * value;
            if (!Matrix4x4.Invert(localToParent, out var parentToLocal))
                throw new InvalidOperationException();
            ParentToLocal = parentToLocal;
        }
    }

    // TODO: We might want setters for these three as well 

    public Vector3 GlobalPosition => LocalToWorld.Translation;
    public Quaternion GlobalRotation => (Parent?.GlobalRotation ?? Quaternion.Identity) * LocalRotation;

    public Vector3 GlobalForward => Vector3.Transform(Vector3.UnitZ, GlobalRotation);
    public Vector3 GlobalUp => Vector3.Transform(Vector3.UnitY, GlobalRotation);
    public Vector3 GlobalRight => Vector3.Transform(Vector3.UnitX, GlobalRotation);
    public Vector3 InnerForward => Vector3.Transform(Vector3.UnitZ, LocalRotation);
    public Vector3 InnerUp => Vector3.Transform(Vector3.UnitY, LocalRotation);
    public Vector3 InnerRight => Vector3.Transform(Vector3.UnitX, LocalRotation);

    public void LookAt(Vector3 dest, bool isLocalSpace = false) =>
        LookIn(dest - (isLocalSpace ? LocalPosition : GlobalPosition), isLocalSpace);

    public void LookNotAt(Vector3 dest, bool isLocalSpace = false) => // useful for camera which looks backwards
        LookIn((isLocalSpace ? LocalPosition : GlobalPosition) - dest, isLocalSpace);

    public void LookIn(Vector3 dir, bool isLocalSpace = false)
    {
        if (MathEx.CmpZero(dir.LengthSquared()))
            return;
        var up = UpFor(isLocalSpace);
        if (MathEx.CmpZero(Vector3.Cross(up, dir).LengthSquared()))
            up = ForwardFor(isLocalSpace);
        LocalRotation = NumericsExtensions.LookIn(dir, up);
    }

    public void LookNotIn(Vector3 dir, bool isLocalSpace = false) => LookIn(-dir, isLocalSpace);

    private Vector3 ForwardFor(bool isLocalSpace) => isLocalSpace || Parent == null ? Vector3.UnitZ : Parent.GlobalForward;
    private Vector3 UpFor(bool isLocalSpace) => isLocalSpace || Parent == null ? Vector3.UnitY : Parent.GlobalUp;

    public void HorizontalSlerpIn(Vector3 dir, float curvature, float time)
    {
        var newForward = MathEx.HorizontalSlerp(InnerForward, MathEx.SafeNormalize(dir), curvature, time);
        LookIn(newForward);
    }

    public float Distance(Vector3 point) => Vector3.Distance(GlobalPosition, point);
    public float DistanceSquared(Vector3 point) => Vector3.DistanceSquared(GlobalPosition, point);
    public float Distance(Location other) => Vector3.Distance(GlobalPosition, other.GlobalPosition);
    public float DistanceSquared(Location other) => Vector3.DistanceSquared(GlobalPosition, other.GlobalPosition);
}
