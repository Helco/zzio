using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.materials;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.imgui
{
    public class ModelMaterialEdit : BaseDisposable
    {
        private const float TexturePreviewSize = 5.0f;
        private const float TextureHoverSizeFactor = 0.4f;

        private readonly ResourceFactory resourceFactory;
        private readonly ImGuiRenderer imGuiRenderer;
        private IReadOnlyList<ModelStandardMaterial> materials = new ModelStandardMaterial[0];
        private IntPtr[] textureBindings = new IntPtr[0];

        public bool OpenEntriesByDefault { get; set; } = true;

        public IReadOnlyList<ModelStandardMaterial> Materials
        {
            get => materials;
            set
            {
                foreach (var oldMaterial in materials)
                    imGuiRenderer.RemoveImGuiBinding(oldMaterial.MainTexture.Texture);

                materials = value;
                textureBindings = materials.Select(
                    material => imGuiRenderer.GetOrCreateImGuiBinding(resourceFactory, material.MainTexture.Texture)
                ).ToArray();
            }
        }

        public ModelMaterialEdit(Window window, ITagContainer diContainer)
        {
            window.AddTag(this);
            imGuiRenderer = window.Container.ImGuiRenderer;
            resourceFactory = diContainer.GetTag<GraphicsDevice>().ResourceFactory;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Materials = new ModelStandardMaterial[0];
        }

        public bool Content()
        {
            if (materials.Count == 0)
                return false;

            bool didChange = false;
            Text("Globals");
            var mat = materials.First().Uniforms.Value;
            didChange |= SliderFloat("Vertex Color Factor", ref mat.vertexColorFactor, 0.0f, 1.0f);
            didChange |= SliderFloat("Global Tint Factor", ref mat.tintFactor, 0.0f, 1.0f);
            didChange |= SliderFloat("Alpha Reference", ref mat.alphaReference, 0.0f, 1.0f, "%.3f");
            if (didChange)
            {
                foreach (var material in materials)
                {
                    material.Uniforms.Ref.vertexColorFactor = mat.vertexColorFactor;
                    material.Uniforms.Ref.tintFactor = mat.tintFactor;
                    material.Uniforms.Ref.alphaReference = mat.alphaReference;
                }
            }

            NewLine();
            Text("Materials");
            foreach (var (material, index) in materials.Indexed())
            {
                bool isVisible = materials[index].Uniforms.Value.alphaReference < 2f;
                PushID(index);
                if (SmallButton(isVisible ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash))
                {
                    isVisible = !isVisible;
                    materials[index].Uniforms.Ref.alphaReference = isVisible ? 0.03f : 2.0f;
                    didChange = true;
                }
                PopID();

                SameLine();
                if (!TreeNodeEx($"Material #{index}", OpenEntriesByDefault ? ImGuiTreeNodeFlags.DefaultOpen : 0))
                    continue;
                var color = material.Uniforms.Value.tint;
                ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.Float);
                TexturePreview(materials[index].MainTexture.Texture, textureBindings[index]);

                TreePop();
            }

            return didChange;
        }

        private void TexturePreview(Texture? texture, IntPtr binding)
        {
            if (texture == null)
                return;
            Columns(2, null, false);
            var previewTexSize = GetTextLineHeight() * TexturePreviewSize;
            SetColumnWidth(0, previewTexSize + GetStyle().FramePadding.X * 3);
            Image(binding, Vector2.One * previewTexSize);
            if (IsItemHovered())
            {
                BeginTooltip();
                var viewportSize = GetWindowViewport().Size;
                var hoverTexSize = Math.Min(viewportSize.X, viewportSize.Y) * TextureHoverSizeFactor * Vector2.One;
                Image(binding, hoverTexSize);
                EndTooltip();
            }
            NextColumn();
            Text(texture?.Name ?? "");
            Text($"{texture?.Width}x{texture?.Height}");
            Text($"{texture?.Format}");
            Columns(1);
        }
    }
}
