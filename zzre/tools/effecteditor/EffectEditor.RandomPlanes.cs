using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zzio.effect.parts;
using zzre.imgui;
using zzre.rendering.effectparts;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools
{
    partial class EffectEditor
    {
        private void HandlePart(RandomPlanes data, RandomPlanesRenderer ren)
        {
            InputText("Name", ref data.name, 128);
            NewLine();

            Text("Timing:");
            InputInt("Phase1", ref data.phase1);
            InputInt("Phase2", ref data.phase2);
            InputInt("Extra phase", ref data.extraPhase);
            InputFloat("MinProgress", ref data.minProgress);
            Checkbox("Ignore Phases", ref data.ignorePhases);
            NewLine();

            Text("Shape/Movement:");
            InputFloat("Width", ref data.width);
            InputFloat("Height", ref data.height);
            DragFloatRange2("Scale Speed Range", ref data.minScaleSpeed, ref data.maxScaleSpeed, 1f, 0f);
            InputFloat("Scale Speed Mult.", ref data.scaleSpeedMult);
            InputFloat("Target Size", ref data.targetSize);
            InputFloat2("Offset", ref data.minPosX, ref data.yOffset);
            InputFloat2("Pos Range", ref data.amplPosX, ref data.amplPosY);
            InputFloat("Rotation Speed Mult.", ref data.rotationSpeedMult);
            InputFloat("Tex Shift", ref data.texShift);
            Checkbox("Circles Around", ref data.circlesAround);
            NewLine();

            Text("Material:");
            LabelText("Texture", data.texName);
            InputInt("Tile Id", ref data.tileId);
            InputInt("Tile Count", ref data.tileCount);
            InputInt("Tile Duration", ref data.tileDuration);
            InputInt("Tile W", ref data.tileW);
            InputInt("Tile H", ref data.tileH);
            ColorEdit4("Color", ref data.color);
            EnumCombo("RenderMode", ref data.renderMode);
        }
    }
}
