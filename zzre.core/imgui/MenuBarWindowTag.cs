using System;

namespace zzre.imgui
{
    public class MenuBarWindowTag : MenuBar
    {
        public Window Window { get; }

        public MenuBarWindowTag(Window parent)
        {
            Window = parent;
            Window.AddTag(this);
            Window.Flags = Window.Flags | ImGuiNET.ImGuiWindowFlags.MenuBar;
            Window.OnContent += Update;
        }
    }
}
