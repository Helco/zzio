using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools
{
    public partial class SceneEditor
    {
        private class DatasetComponent
        {
            private readonly ITagContainer diContainer;
            private readonly SceneEditor editor;
            private zzio.scn.Dataset? dataset;
            private zzio.scn.Version? version;

            public DatasetComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;

                editor = diContainer.GetTag<SceneEditor>();
                editor.OnLoadScene += HandleLoadScene;
                editor.editor.AddInfoSection("Info", HandleContent, false);
            }

            private void HandleLoadScene()
            {
                dataset = editor.scene?.dataset;
                version = editor.scene?.version;
            }

            private void HandleContent()
            {
                if (dataset == null || version == null)
                    return;
                Text("Dataset");
                int sceneId = (int)dataset.sceneId;
                var nameUID = dataset.nameUID.ToString("X8");
                int unk1 = dataset.unk1;
                int unk4 = (int)dataset.unk4;
                int v3 = (int)version.v3;
                int buildVersion = (int)version.buildVersion;
                int year = (int)version.year;
                int vv2 = (int)version.vv2;

                InputInt("Scene ID", ref sceneId);
                EnumCombo("Scene Type", ref dataset.sceneType);
                InputText("Name UID", ref nameUID, 8);
                InputInt("Unk1", ref unk1);
                Checkbox("Unk2", ref dataset.isInterior);
                Checkbox("Is London", ref dataset.isLondon);
                InputInt("Unk4", ref unk4);
                Checkbox("Unk5", ref dataset.isHotScene);
                Checkbox("Unk6", ref dataset.unk6);
                InputText("S1", ref dataset.s1, 256);
                InputText("S2", ref dataset.s2, 256);

                NewLine();
                Text("Version");
                InputText("Author", ref version.author, 256);
                EnumCombo("Country", ref version.country);
                EnumCombo("Build", ref version.type);
                InputInt("V3", ref v3);
                InputInt("Version", ref buildVersion);
                InputInt("Year", ref year);
                InputInt("VV2", ref vv2);
            }
        }
    }
}
