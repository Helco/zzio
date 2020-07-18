using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class MenuBar
    {
        private class MenuBarItem
        {
            public string Name { get; }
            public Action? OnClick { get; }
            public MenuBarItem? Parent { get; }
            public List<MenuBarItem> Children { get; } = new List<MenuBarItem>(); 

            public MenuBarItem(MenuBarItem? parent, string name, Action? onClick)
            {
                Parent = parent;
                Name = name;
                OnClick = onClick;
                Parent?.Children.Add(this);
            }
        }

        private MenuBarItem RootItem = new MenuBarItem(null, "", null);

        public void AddItem(string path, Action onClick)
        {
            var comp = StringComparison.InvariantCultureIgnoreCase;
            var curParent = RootItem;
            var parts = path.Split("/");
            foreach (var part in parts[0..^1])
            {
                var nextParent = curParent.Children.FirstOrDefault(i => i.Name.Equals(part, comp));
                if (nextParent == null)
                    nextParent = new MenuBarItem(curParent, part, null);
                curParent = nextParent;
            }

            var item = curParent.Children.FirstOrDefault(i => i.Name.Equals(parts.Last(), comp));
            if (item != null)
                throw new InvalidOperationException($"Item of path {path} is already defined");
            new MenuBarItem(curParent, parts.Last(), onClick);
        }

        public void Update()
        {
            if (!BeginMenuBar())
                return;
            foreach (var child in RootItem.Children)
                UpdateItem(child);
            EndMenuBar();
        }

        private void UpdateItem(MenuBarItem item)
        {
            if (item.OnClick != null)
            {
                if (MenuItem(item.Name))
                    item.OnClick();
                return;
            }

            if (!BeginMenu(item.Name))
                return;
            foreach (var child in item.Children)
                UpdateItem(child);
            EndMenu();
        }
    }
}
