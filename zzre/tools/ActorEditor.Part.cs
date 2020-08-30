using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;
using zzre.core;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools
{
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

            public Location location = new Location();
            public DeviceBufferRange locationBufferRange;
            public ClumpBuffers geometry;
            public ModelSkinnedMaterial[] materials;
            public Skeleton skeleton;
            public DebugSkeletonRenderer skeletonRenderer;
            public (AnimationType type, string fileName, SkeletalAnimation ani)[] animations;
            
            public Part(ITagContainer diContainer, string modelName, (AnimationType type, string filename)[] animationNames)
            {
                this.diContainer = diContainer;
                this.modelName = modelName;
                textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
                gameTime = diContainer.GetTag<GameTime>();
                var modelPath = new FilePath("resources/models/actorsex/").Combine(modelName);
                var texturePath = textureLoader.GetTexturePathFromModel(modelPath);

                IResourcePool resourcePool = diContainer.GetTag<IResourcePool>();
                using var contentStream = resourcePool.FindAndOpen(modelPath);
                if (contentStream == null)
                    throw new IOException($"Could not open model at {modelPath.ToPOSIXString()}");
                var clump = Section.ReadNew(contentStream);
                if (clump.sectionId != SectionId.Clump)
                    throw new InvalidDataException($"Expected a root clump section, got a {clump.sectionId}");
                var skin = clump.FindChildById(SectionId.SkinPLG) as RWSkinPLG;
                if (skin == null)
                    throw new InvalidDataException($"Attached actor part model does not have a skin");

                geometry = new ClumpBuffers(diContainer, (RWClump)clump);
                AddDisposable(geometry);

                locationBufferRange = diContainer.GetTag<LocationBuffer>().Add(location);
                var materialLinkTarget = diContainer.GetTag<IStandardTransformMaterial>();
                void LinkTransformsFor(IStandardTransformMaterial material)
                {
                    material.LinkTransformsTo(materialLinkTarget);
                    material.World.BufferRange = locationBufferRange;
                }

                skeleton = new Skeleton(skin);
                skeleton.Location.Parent = location;
                skeletonRenderer = new DebugSkeletonRenderer(diContainer, geometry, skeleton);
                AddDisposable(skeletonRenderer);

                materials = new ModelSkinnedMaterial[geometry.SubMeshes.Count];
                foreach (var (rwMaterial, index) in geometry.SubMeshes.Select(s => s.Material).Indexed())
                {
                    var material = materials[index] = new ModelSkinnedMaterial(diContainer);
                    (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(texturePath, rwMaterial);
                    material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                    material.Uniforms.Ref.vertexColorFactor = 0.0f;
                    material.Uniforms.Ref.tint = rwMaterial.color.ToFColor();
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
                foreach (var (subMesh, index) in geometry.SubMeshes.Indexed())
                {
                    (materials[index] as IMaterial).Apply(cl);
                    geometry.SetBuffers(cl);
                    geometry.SetSkinBuffer(cl);
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
                bool hasChanged = isPlaying;
                if (isPlaying)
                {
                    skeleton.AddTime(gameTime.Delta);
                    foreach (var mat in materials)
                        mat.Pose.MarkPoseDirty();
                    skeletonRenderer.BoneMaterial.Pose.MarkPoseDirty();
                }

                PushID(modelName);
                if (BeginCombo("Animation", currentAnimationI < 0 ? "" : animations[currentAnimationI].type.ToString()))
                {
                    foreach (var ((type, _, ani), index) in animations.Indexed())
                    {
                        PushID((int)type);
                        if (Selectable(type.ToString(), index == currentAnimationI))
                        {
                            currentAnimationI = index;
                            skeleton.JumpToAnimation(ani);
                        }
                        PopID();
                    }
                    EndCombo();
                }

                float time = skeleton.AnimationTime;
                if (skeleton.CurrentAnimation == null)
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
                    if (SliderFloat("Time", ref time, 0.0f, skeleton.CurrentAnimation.duration))
                    {
                        skeleton.AnimationTime = time;
                        foreach (var mat in materials)
                            mat.Pose.MarkPoseDirty();
                        skeletonRenderer.BoneMaterial.Pose.MarkPoseDirty();
                        hasChanged = true;
                    }
                    if (SmallButton(IconFonts.ForkAwesome.FastBackward))
                        skeleton.JumpToAnimation(skeleton.CurrentAnimation);
                    SameLine();
                    if (isPlaying && SmallButton(IconFonts.ForkAwesome.Pause))
                        isPlaying = false;
                    else if (!isPlaying && SmallButton(IconFonts.ForkAwesome.Play))
                        isPlaying = true;
                    SameLine();
                    if (SmallButton(IconFonts.ForkAwesome.FastForward))
                        skeleton.JumpToAnimation(skeleton.CurrentAnimation);
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
                            if (Selectable(nextType.ToString(), curType == nextType))
                            {
                                // Disabled for now to not actually change anything
                                /*animations[index].type = nextType;
                                skeleton.JumpToAnimation(null);
                                isPlaying = false;
                                hasChanged = true;*/
                            }
                            PopID();
                        }
                        EndCombo();
                    }
                    PopItemWidth();
                    NextColumn();

                    
                    var nameDummy = filename;
                    InputText("##name", ref nameDummy, 256, ImGuiNET.ImGuiInputTextFlags.ReadOnly);
                    SameLine();
                    PushStyleVar(ImGuiNET.ImGuiStyleVar.Alpha, 0.6f);
                    if (Button(IconFonts.ForkAwesome.Pencil))
                    {
                        // not yet
                    }
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
}
