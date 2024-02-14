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

namespace zzre.imgui;

public class ModelMaterialEdit : BaseDisposable
{
    private const float TexturePreviewSize = 5.0f;
    private const float TextureHoverSizeFactor = 0.4f;

    private readonly ResourceFactory resourceFactory;
    private readonly ImGuiRenderer imGuiRenderer;
    private IReadOnlyList<ModelMaterial> materials = Array.Empty<ModelMaterial>();
    private IntPtr[] textureBindings = Array.Empty<IntPtr>();
    private OnceAction onceAction;

    public bool OpenEntriesByDefault { get; set; } = true;

    public IReadOnlyList<ModelMaterial> Materials
    {
        get => materials;
        set
        {
            var oldMaterials = materials.ToArray();
            onceAction.Next += () => // delay removing ImGui bindings so they are still alive for last the last render
            {
                foreach (var oldMaterial in materials)
                    imGuiRenderer.RemoveImGuiBinding(oldMaterial.Texture.Texture);
            };

            materials = value;
            textureBindings = materials.Select(
                material => imGuiRenderer.GetOrCreateImGuiBinding(resourceFactory, material.Texture.Texture)
            ).ToArray();
        }
    }

    public ModelMaterialEdit(Window window, ITagContainer diContainer)
    {
        window.AddTag(this);
        onceAction = window.GetTag<OnceAction>();
        imGuiRenderer = window.Container.ImGuiRenderer;
        resourceFactory = diContainer.GetTag<GraphicsDevice>().ResourceFactory;
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        Materials = Array.Empty<ModelMaterial>();
    }

    public bool Content()
    {
        if (materials.Count == 0)
            return false;

        bool didChange = false;
        Text("Globals");
        var mat = materials.First().Factors.Value;
        didChange |= SliderFloat("Texture Factor", ref mat.textureFactor, 0f, 1f);
        didChange |= SliderFloat("Vertex Color Factor", ref mat.vertexColorFactor, 0.0f, 1.0f);
        didChange |= SliderFloat("Global Tint Factor", ref mat.tintFactor, 0.0f, 1.0f);
        didChange |= SliderFloat("Alpha Reference", ref mat.alphaReference, 0.0f, 1.0f, "%.3f");
        if (didChange)
        {
            foreach (var material in materials)
            {
                material.Factors.Ref.textureFactor = mat.textureFactor;
                material.Factors.Ref.vertexColorFactor = mat.vertexColorFactor;
                material.Factors.Ref.tintFactor = mat.tintFactor;
                material.Factors.Ref.alphaReference = mat.alphaReference;
            }
        }

        NewLine();
        Text("Materials");
        foreach (var (material, index) in materials.Indexed())
        {
            bool isVisible = materials[index].Factors.Value.alphaReference < 2f;
            PushID(index);
            if (SmallButton(isVisible ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash))
            {
                isVisible = !isVisible;
                materials[index].Factors.Ref.alphaReference = isVisible ? 0.03f : 2.0f;
                didChange = true;
            }
            PopID();

            SameLine();
            if (!TreeNodeEx($"Material #{index}", OpenEntriesByDefault ? ImGuiTreeNodeFlags.DefaultOpen : 0))
                continue;
            var color = material.Tint.Value;
            ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.Float);
            TexturePreview(materials[index].Texture.Texture, textureBindings[index]);

            TreePop();
        }

        return didChange;
    }

    private static void TexturePreview(Texture? texture, IntPtr binding)
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
