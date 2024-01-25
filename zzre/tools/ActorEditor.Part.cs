using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.vfs;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools;

public partial class ActorEditor
{
    private class Part : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly IAssetLoader<Texture> textureLoader;
        private readonly GameTime gameTime;
        private readonly string modelName; // used as ImGui ID
        private bool isPlaying = false;
        private int currentAnimationI = -1;

        public readonly Location location = new();
        public readonly DeviceBufferRange locationBufferRange;
        public readonly ClumpMesh mesh;
        public readonly ModelMaterial[] materials;
        public readonly Skeleton skeleton;
        public readonly DebugSkeletonRenderer skeletonRenderer;
        public readonly (AnimationType type, string fileName, SkeletalAnimation ani)[] animations;
        public (int BoneIdx, Vector3 TargetPos)? singleIK = null;

        public Part(ITagContainer diContainer, string modelName, (AnimationType type, string filename)[] animationNames)
        {
            this.diContainer = diContainer;
            this.modelName = modelName;
            textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            gameTime = diContainer.GetTag<GameTime>();
            var resourcePool = diContainer.GetTag<IResourcePool>();
            var modelPath = new FilePath("resources/models/actorsex/").Combine(modelName);
            var texturePath = textureLoader.GetTexturePathFromModel(modelPath);

            mesh = new ClumpMesh(diContainer, modelPath);
            if (mesh.Skin is null)
                throw new InvalidDataException("Attached actor part model does not have a skin");
            AddDisposable(mesh);

            locationBufferRange = diContainer.GetTag<LocationBuffer>().Add(location);
            var camera = diContainer.GetTag<Camera>();
            void LinkTransformsFor(IStandardTransformMaterial material)
            {
                material.LinkTransformsTo(camera);
                material.World.BufferRange = locationBufferRange;
            }

            skeleton = new Skeleton(mesh.Skin, modelName);
            skeleton.Location.Parent = location;
            skeletonRenderer = new DebugSkeletonRenderer(diContainer, mesh, skeleton);
            AddDisposable(skeletonRenderer);

            materials = new ModelMaterial[mesh.Materials.Count];
            foreach (var (rwMaterial, index) in mesh.Materials.Indexed())
            {
                var material = materials[index] = new ModelMaterial(diContainer) { IsSkinned = true };
                (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(texturePath, rwMaterial);
                material.Factors.Ref = ModelFactors.Default;
                material.Factors.Ref.vertexColorFactor = 0.0f;
                material.Tint.Ref = rwMaterial.color;
                material.Pose.Skeleton = skeleton;
                LinkTransformsFor(material);
                AddDisposable(material);
            }

            SkeletalAnimation LoadAnimation(string filename)
            {
                var animationPath = new FilePath("resources/models/actorsex/").Combine(filename);
                using var contentStream = resourcePool.FindAndOpen(animationPath);
                if (contentStream == null)
                    throw new IOException($"Could not open animation at {animationPath.ToPOSIXString()}");
                var animation = SkeletalAnimation.ReadNew(contentStream);
                if (animation.BoneCount != skeleton.Bones.Count)
                    throw new InvalidDataException($"Animation {filename} is incompatible with actor skeleton {modelName}");
                return animation;
            }
            animations = animationNames.Select(t => (t.type, t.filename, LoadAnimation(t.filename))).ToArray();
            skeleton.ResetToBinding();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            diContainer.GetTag<LocationBuffer>().Remove(locationBufferRange);
        }

        public void Render(CommandList cl)
        {
            foreach (var subMesh in mesh.SubMeshes)
            {
                (materials[subMesh.Material] as IMaterial).Apply(cl);
                materials[subMesh.Material].ApplyAttributes(cl, mesh);
                cl.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
                cl.DrawIndexed(
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    instanceCount: 1,
                    vertexOffset: 0,
                    instanceStart: 0);
            }
        }

        public void RenderDebug(CommandList cl) => skeletonRenderer.Render(cl);

        public bool PlaybackContent()
        {
            bool hasChanged = isPlaying || singleIK.HasValue;
            if (hasChanged)
            {
                skeleton.AddTime(isPlaying ? gameTime.Delta : 0.0f);
                if (singleIK.HasValue)
                    skeleton.ApplySingleIK(singleIK.Value.BoneIdx, singleIK.Value.TargetPos);
                foreach (var mat in materials)
                    mat.Pose.MarkPoseDirty();
                skeletonRenderer.BoneMaterial.Pose.MarkPoseDirty();
            }

            PushID(modelName);
            if (BeginCombo("Animation", currentAnimationI < 0 ? "" : animations[currentAnimationI].type.ToString()))
            {
                foreach (var ((type, _, ani), index) in animations.Indexed())
                {
                    PushID(index);
                    var isOverwritten = animations.Skip(index + 1).Any(a => a.type == type);
                    var name = (isOverwritten ? "(!!!) " : "") + type;
                    if (Selectable(name, index == currentAnimationI))
                    {
                        currentAnimationI = index;
                        skeleton.JumpToAnimation(ani);
                    }
                    PopID();
                }
                EndCombo();
            }

            float time = skeleton.AnimationTime;
            if (skeleton.Animation == null)
            {
                SliderFloat("Time", ref time, 0.0f, 0.0f);
                SmallButton(IconFonts.ForkAwesome.FastBackward);
                SameLine();
                SmallButton(IconFonts.ForkAwesome.Play);
                SameLine();
                SmallButton(IconFonts.ForkAwesome.FastForward);
            }
            else
            {
                if (SliderFloat("Time", ref time, 0.0f, skeleton.Animation.duration))
                {
                    skeleton.AnimationTime = time;
                    foreach (var mat in materials)
                        mat.Pose.MarkPoseDirty();
                    skeletonRenderer.BoneMaterial.Pose.MarkPoseDirty();
                    hasChanged = true;
                }
                if (SmallButton(IconFonts.ForkAwesome.FastBackward))
                    skeleton.JumpToAnimation(skeleton.Animation);
                SameLine();
                if (isPlaying && SmallButton(IconFonts.ForkAwesome.Pause))
                    isPlaying = false;
                else if (!isPlaying && SmallButton(IconFonts.ForkAwesome.Play))
                    isPlaying = true;
                SameLine();
                if (SmallButton(IconFonts.ForkAwesome.FastForward))
                    skeleton.JumpToAnimation(skeleton.Animation);
            }

            PopID();
            return hasChanged;
        }

        public bool AnimationsContent()
        {
            bool hasChanged = false;
            PushID(modelName);
            Separator();
            Columns(2, null, true);
            Text("Type");
            NextColumn();
            Text("File");
            NextColumn();
            Separator();
            Separator();

            foreach (var ((curType, filename, _), index) in animations.Indexed())
            {
                PushID(index);
                var selectableTypes = GetUnusedAnimationTypes().Append(curType).OrderBy(t => (int)t).ToArray();
                PushItemWidth(-1.0f);
                if (BeginCombo("##AnimationType", curType.ToString(), ImGuiNET.ImGuiComboFlags.HeightSmall))
                {
                    foreach (var nextType in selectableTypes)
                    {
                        PushID((int)nextType);
                        Selectable(nextType.ToString(), curType == nextType);
                        PopID();
                    }
                    EndCombo();
                }
                PopItemWidth();
                NextColumn();

                var isOverwritten = animations.Skip(index + 1).Any(a => a.type == curType);
                var nameDummy = (isOverwritten ? "(!!!) " : "") + filename;
                InputText("##name", ref nameDummy, 256, ImGuiNET.ImGuiInputTextFlags.ReadOnly);
                SameLine();
                PushStyleVar(ImGuiNET.ImGuiStyleVar.Alpha, 0.6f);
                Button(IconFonts.ForkAwesome.Pencil);
                PopStyleVar();

                NextColumn();

                PopID();
                Separator();
            }
            Columns(1);
            PopID();
            return hasChanged;
        }

        private IEnumerable<AnimationType> GetUnusedAnimationTypes() => Enum
            .GetValues(typeof(AnimationType))
            .Cast<AnimationType>()
            .Except(animations.Select(t => t.type));
    }
}
