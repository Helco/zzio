using zzio.effect.parts;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools;

partial class EffectEditor
{
    private static void HandlePart(Sound data)
    {
        data.Name = InputText("Name", data.Name, 128);
        LabelText("Filename", data.fileName);
        SliderInt("Volume", ref data.volume, 0, 127);
        DragFloatRange2("Distance", ref data.minDist, ref data.maxDist);
        Checkbox("Is disabled", ref data.isDisabled);
    }
}
