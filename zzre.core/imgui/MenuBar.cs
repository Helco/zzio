using System;
using System.Collections.Generic;
using System.Linq;
using static ImGuiNET.ImGui;

namespace zzre.imgui;

public class MenuBar
{
    private sealed class MenuBarItem
    {
        public string Name { get; }
        public Action<string>? OnContent { get; }
        public MenuBarItem? Parent { get; }
        public List<MenuBarItem> Children { get; } = [];

        public MenuBarItem(MenuBarItem? parent, string name, Action<string>? onClick)
        {
            Parent = parent;
            Name = name;
            OnContent = onClick;
            Parent?.Children.Add(this);
        }
    }

    private readonly MenuBarItem RootItem = new(null, "", null);

    public void AddItem(string path, Action<string> onContent)
    {
        var comp = StringComparison.InvariantCultureIgnoreCase;
        var curParent = RootItem;
        var parts = path.Split("/");
        foreach (var part in parts[..^1])
        {
            var nextParent = curParent.Children.FirstOrDefault(i => i.Name.Equals(part, comp)) ?? new MenuBarItem(curParent, part, null);
            curParent = nextParent;
        }

        var item = curParent.Children.FirstOrDefault(i => i.Name.Equals(parts.Last(), comp));
        if (item != null)
            throw new InvalidOperationException($"Item of path {path} is already defined");
        new MenuBarItem(curParent, parts.Last(), onContent);
    }

    public void AddButton(string path, Action onClick) => AddItem(path, name =>
    {
        if (MenuItem(name))
            onClick();
    });

    public void AddButton(string path, Action onClick, Func<bool> isEnabled) => AddItem(path, name =>
    {
        BeginDisabled(!isEnabled());
        if (MenuItem(name))
            onClick();
        EndDisabled();
    });

    public delegate ref T GetRefValueFunc<T>();

    public void AddCheckbox(string path, GetRefValueFunc<bool> isChecked, Action? onChanged = null) => AddItem(path, name =>
    {
        if (MenuItem(name, "", ref isChecked()))
            onChanged?.Invoke();
    });

    public void AddRadio(string path, IReadOnlyList<string> labels, GetRefValueFunc<int> getValue, Action? onChanged = null) => AddItem(path, name =>
    {
        if (!BeginMenu(name))
            return;
        ref int curValue = ref getValue();
        for (int i = 0; i < labels.Count; i++)
        {
            if (MenuItem(labels[i], "", curValue == i))
            {
                curValue = i;
                onChanged?.Invoke();
            }
        }
        EndMenu();
    });

    public void AddSlider(string path, float minVal, float maxVal, GetRefValueFunc<float> getValue, Action? onChanged = null) => AddItem(path, name =>
    {
        if (SliderFloat(name, ref getValue(), minVal, maxVal))
            onChanged?.Invoke();
    });

    public void AddSlider(string path, int minVal, int maxVal, GetRefValueFunc<int> getValue, Action? onChanged = null) => AddItem(path, name =>
    {
        if (SliderInt(name, ref getValue(), minVal, maxVal))
            onChanged?.Invoke();
    });

    public void Update()
    {
        if (!BeginMenuBar())
            return;
        foreach (var child in RootItem.Children)
            UpdateItem(child);
        EndMenuBar();
    }

    private static void UpdateItem(MenuBarItem item)
    {
        if (item.OnContent != null)
        {
            item.OnContent(item.Name);
            return;
        }

        if (!BeginMenu(item.Name))
            return;
        foreach (var child in item.Children)
            UpdateItem(child);
        EndMenu();
    }
}
