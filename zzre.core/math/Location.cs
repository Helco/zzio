using System;
using System.Numerics;

namespace zzre
{
    public class Location 
    {
        public Location? Parent { get; set; } = null;

        public Vector3 LocalPosition { get; set; } = Vector3.Zero;

        private Quaternion _localRotation = Quaternion.Identity;
        public Quaternion LocalRotation
        {
            get => _localRotation;
            set => _localRotation = Quaternion.Normalize(value);
        }

        public Matrix4x4 ParentToLocal
        {
            get =>
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

        public void LookIn(Vector3 dir, bool isLocalSpace = false) =>
            LocalRotation = NumericsExtensions.LookAt(dir, FrontFor(isLocalSpace), UpFor(isLocalSpace));
        public void LookAt(Vector3 dest, bool isLocalSpace = false) =>
            LocalRotation = NumericsExtensions.LookAt(isLocalSpace ? LocalPosition : GlobalPosition, dest, FrontFor(isLocalSpace), UpFor(isLocalSpace));

        private Vector3 FrontFor(bool isLocalSpace) => isLocalSpace || Parent == null ? Vector3.UnitZ : Parent.GlobalForward;
        private Vector3 UpFor(bool isLocalSpace) => isLocalSpace || Parent == null ? Vector3.UnitY : Parent.GlobalUp;
    }
}
