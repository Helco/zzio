using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using zzio.effect.parts;
using zzre.rendering;
using zzre.rendering.effectparts;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools
{
    partial class EffectEditor
    {
        private void HandlePart(BeamStar data, BeamStarRenderer ren)
        {
            InputText("Name", ref data.name, 128);
            NewLine();

            Text("Timing:");
            InputInt("Phase 1", ref data.phase1);
            InputInt("Phase 2", ref data.phase2);

            Text("Shape/Movement:");
            EnumCombo("Complexity", ref data.complexity);
            EnumCombo("Mode", ref data.mode);
            InputFloat("Width", ref data.width);
            InputFloat("Scale Speed", ref data.scaleSpeedXY);
            InputFloat("Rotation Speed", ref data.rotationSpeed);

            Text("Material:");
            EnumCombo("Render Mode", ref data.renderMode);
            LabelText("Texture", data.texName);
            ColorEdit4("Color", ref data.color);
            InputFloat("V-Coord Shift", ref data.texShiftVStart);
            DragFloatRange2("End V-Coord", ref data.startTexVEnd, ref data.endTexVEnd);
        }
    }
}
