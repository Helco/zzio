using zzio.effect.parts;
using zzre.rendering.effectparts;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools;

public partial class EffectEditor
{
    private void HandlePart(ParticleEmitter data, ParticleEmitterRenderer ren)
    {
        Text($"Current: {ren.CurrentParticles} / {ren.MaxParticles}");
        NewLine();

        InputText("Name", ref data.name, 128);
        EnumCombo("Type", ref data.type);
        NewLine();

        Text("Timing:");
        InputInt("Phase1", ref data.phase1);
        InputInt("Phase2", ref data.phase2);
        InputInt("MinProgress", ref data.minProgress);
        ValueRangeAnimation("Life", ref data.life, 0f);
        NewLine();

        Text("Spawning:");
        EnumCombo("Spawn Mode", ref data.spawnMode);
        InputInt("Spawn Rate", ref data.spawnRate);
        InputFloat("Hor. Radius", ref data.horRadius);
        InputFloat("Ver. Radius", ref data.verRadius);
        InputFloat("Ver. Direction", ref data.verticalDir);

        Text("Shape/Movement:");
        ValueRangeAnimation("Scale", ref data.scale, 0f); // not accurate, midpoint of shown range is minimal size
        InputFloat("Min. Velocity", ref data.minVel);
        ValueRangeAnimation("Acceleration", ref data.acc);
        InputFloat3("Gravity", ref data.gravity);
        InputFloat3("Gravity Mod.", ref data.gravityMod);
        NewLine();

        Text("Material:");
        LabelText("Texture", data.texName);
        InputInt("Tile Id", ref data.tileId);
        InputInt("Tile Count", ref data.tileCount);
        InputInt("Tile Duration", ref data.tileDuration);
        InputInt("Tile W", ref data.tileW);
        InputInt("Tile H", ref data.tileH);
        ValueRangeAnimation("Red", ref data.colorR, 0f, 1f);
        ValueRangeAnimation("Green", ref data.colorG, 0f, 1f);
        ValueRangeAnimation("Blue", ref data.colorB, 0f, 1f);
        ValueRangeAnimation("Alpha", ref data.colorA, 0f, 1f);
        EnumCombo("RenderMode", ref data.renderMode);
    }
}
