using System;
using zzio;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools;

partial class SceneEditor
{
    private class MiscComponent
    {
        private readonly ITagContainer diContainer;
        private readonly SceneEditor editor;
        private zzio.scn.Misc misc = new();

        public MiscComponent(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            editor = diContainer.GetTag<SceneEditor>();
            editor.OnLoadScene += HandleLoadScene;
            editor.editor.AddInfoSection("Misc", HandleContent, false);
        }

        private void HandleLoadScene()
        {
            misc = editor.scene?.misc ?? new();
        }

        private void HandleContent()
        {
            if (Hyperlink("World", misc.worldFile))
            {
                var fullPath = new FilePath("resources").Combine(misc.worldPath, misc.worldFile + ".bsp");
                diContainer.GetTag<OpenDocumentSet>().Open(fullPath);
            }
            LabelText("World Path", misc.worldPath);
            LabelText("Texture Path", misc.texturePath);

            NewLine();
            Text("Lighting");
            ColorEdit4("Ambient Light", ref misc.ambientLight);
            InputFloat3("V1", ref misc.v1);
            InputFloat3("V2", ref misc.v2);
            ColorEdit4("Clear Color", ref misc.clearColor);

            NewLine();
            Text("Fog");
            EnumCombo("Fog Type", ref misc.fogType);
            ColorEdit4("Fog Color", ref misc.fogColor);
            InputFloat("Fog Distance", ref misc.fogDistance);
            InputFloat("Fog Density", ref misc.fogDensity);
            InputFloat("Far Clip", ref misc.farClip);
        }
    }
}
