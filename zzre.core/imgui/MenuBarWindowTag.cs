using System;

namespace zzre.imgui
{
    public class MenuBarWindowTag : MenuBar
    {
        public BaseWindow Window { get; }

        public MenuBarWindowTag(BaseWindow parent)
        {
            Window = parent;
            Window.AddTag(this);
            Window.Flags = Window.Flags | ImGuiNET.ImGuiWindowFlags.MenuBar;
            Window.OnContent += Update;
        }
    }
}
