using zzio.effect.parts;
using zzre.rendering.effectparts;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools;

public partial class EffectEditor
{
    private void HandlePart(MovingPlanes data, MovingPlanesRenderer ren)
    {
        InputText("Name", ref data.name, 128);
        NewLine();

        Text("Timing:");
        InputInt("Phase1", ref data.phase1);
        InputInt("Phase2", ref data.phase2);
        InputFloat("MinProgress", ref data.minProgress);
        Checkbox("ManualProgress", ref data.manualProgress);
        NewLine();

        Text("Shape/Movement:");
        InputFloat("Width", ref data.width);
        InputFloat("Height", ref data.height);
        InputFloat("SizeModSpeed", ref data.sizeModSpeed);
        InputFloat("Target Size", ref data.targetSize);
        InputFloat("Rotation", ref data.rotation);
        InputFloat("Y Offset", ref data.yOffset);
        InputFloat("Tex Shift", ref data.texShift);
        Checkbox("CirclesAround", ref data.circlesAround);
        Checkbox("Use Direction", ref data.useDirection);
        Checkbox("Single Plane", ref data.disableSecondPlane);
        NewLine();

        Text("Material:");
        LabelText("Texture", data.texName);
        InputInt("TileId", ref data.tileId);
        InputInt("TileW", ref data.tileW);
        InputInt("TileH", ref data.tileH);
        ColorEdit4("Color", ref data.color);
        EnumCombo("RenderMode", ref data.renderMode);
    }
}
