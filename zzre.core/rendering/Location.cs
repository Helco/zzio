using System;
using System.Numerics;

namespace zzre
{
    public class Location 
    {
        public Location? Parent { get; set; } = null;

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
                if (!Matrix4x4.Decompose(value, out var newScale, out var newRotation, out var newTranslation))
                {
                    newRotation = Quaternion.Normalize(
                        Quaternion.CreateFromRotationMatrix(value));
                }
                LocalPosition = newTranslation;
                LocalRotation = newRotation;
                LocalScale = newScale;
            }
        }

        public Matrix4x4 WorldToLocal
        {
            get => ParentToLocal * (Parent?.WorldToLocal ?? Matrix4x4.Identity);
            set => ParentToLocal = Parent == null ? value : value * Parent.LocalToWorld;
        }

        public Matrix4x4 LocalToWorld
        {
            get
            {
                if (!Matrix4x4.Invert(WorldToLocal, out var localToWorld))
                    throw new InvalidOperationException();
                return localToWorld;
            }
            set
            {
                var localToParent = (Parent?.WorldToLocal ?? Matrix4x4.Identity) * value;
                if (!Matrix4x4.Invert(localToParent, out var parentToLocal))
                    throw new InvalidOperationException();
                ParentToLocal = parentToLocal;
            }
        }

        // TODO: We might want setters for these three as well 

        public Vector3 GlobalPosition => WorldToLocal.Translation;
        public Quaternion GlobalRotation => Quaternion.CreateFromRotationMatrix(WorldToLocal);
        public Vector3 GlobalScale => (Parent?.LocalScale ?? Vector3.One) * LocalScale;

        public Vector3 GlobalForward => Vector3.Transform(Vector3.UnitZ, GlobalRotation);
        public Vector3 GlobalUp => Vector3.Transform(Vector3.UnitY, GlobalRotation);
        public Vector3 GlobalRight => Vector3.Transform(Vector3.UnitX, GlobalRotation);
    }
}
