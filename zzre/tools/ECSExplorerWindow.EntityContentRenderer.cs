namespace zzre.tools;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using DefaultEcs.Serialization;
using ImGuiNET;
using Vulkan;
using zzre.imgui;
using static ImGuiNET.ImGui;

internal partial class ECSExplorerWindow
{
    public delegate void RenderComponentFunc<T>(in T component, in DefaultEcs.Entity entity);

    private abstract class ComponentRenderer : IComparable<ComponentRenderer>
    {
        public int Priority { get; init; } = 0;

        public int CompareTo(ComponentRenderer? other)
        {
            var otherPriority = other?.Priority ?? int.MinValue;
            return otherPriority - Priority; // swapped, that high priority is at the start of the list
        }
    }

    private class ComponentRenderer<T> : ComponentRenderer
    {
        public RenderComponentFunc<T>? Render { get; init; }
    }

    private class ComponentRendererList : List<ComponentRenderer>
    {
        private bool isSorted = true;

        public new void Add(ComponentRenderer renderer)
        {
            base.Add(renderer);
            isSorted = false;
        }

        public void SortIfNecessary()
        {
            if (isSorted)
                return;
            isSorted = true;
            Sort();
        }
    }

    private static Dictionary<Type, ComponentRendererList> componentRenderers = new();

    public static void AddComponentRenderer<T>(int prio = 0, RenderComponentFunc<T>? render = null)
    {
        if (render == null)
            throw new ArgumentException("At least one of the functions have to be implemented");

        if (!componentRenderers.TryGetValue(typeof(T), out var list))
            componentRenderers.Add(typeof(T), list = new());
        list.Add(new ComponentRenderer<T>()
        {
            Priority = prio,
            Render = render
        });
    }

    private static ComponentRenderer<T>? FindComponentRendererThat<T>(Func<ComponentRenderer<T>, bool> predicate)
    {
        if (!componentRenderers.TryGetValue(typeof(T), out var list))
            return null;
        list.SortIfNecessary();
        return list
            .Select(r => r as ComponentRenderer<T>)
            .Where(r => r != null)
            .FirstOrDefault(predicate!);
    }

    private class EntityContentRenderer : IComponentReader
    {
        public void OnRead<T>(in T component, in DefaultEcs.Entity entity)
        {
            PushID(typeof(T).FullName);
            var renderering = FindComponentRendererThat<T>(r => r.Render != null);
            if (renderering == null)
                GenericComponentRenderer(component);
            else
                renderering?.Render!(component, entity);
            PopID();
        }
    }

    private static void GenericComponentRenderer<T>(in T component)
    {
        TableNextRow();
        TableNextColumn();

        var type = typeof(T);
        if (!type.IsValueType)
        {
            TreeNodeEx(type.Name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            TableNextColumn();
            Text("(reference)");
            TreePop();
            return;
        }

        if (type.IsEnum)
        {
            var tmpComponent = component;
            TreeNodeEx(type.Name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            TableNextColumn();
            ImGuiEx.EnumComboUnsafe<T>("", ref tmpComponent);
            TreePop();
            return;
        }

        if (!TreeNodeEx(type.Name, default))
            return;

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        foreach (var field in fields)
            GenericField(field.GetValue(component)!, field);

        TreePop();
    }

    private static void GenericField(object value, FieldInfo field)
    {
        void NameColumn()
        {
            TreeNodeEx(field.Name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            TableNextColumn();
        }

        TableNextRow();
        TableNextColumn();

        if (value is null)
        {
            NameColumn();
            Text("<null>");
        }
        else if (value is string tmpText)
        {
            NameColumn();
            InputText("", ref tmpText, (uint)tmpText.Length);
        }
        else if (value is bool tmpBool)
        {
            NameColumn();
            Checkbox("", ref tmpBool);
        }
        else if (value is float tmpFloat)
        {
            NameColumn();
            InputFloat("", ref tmpFloat);
        }
        else if (value is int tmpInt)
        {
            NameColumn();
            InputInt("", ref tmpInt);
        }
        else if (value is Vector2 tmpVec2)
        {
            NameColumn();
            InputFloat2("", ref tmpVec2);
        }
        else if (value is Vector3 tmpVec3)
        {
            NameColumn();
            InputFloat3("", ref tmpVec3);
        }
        else if (value is Vector4 tmpVec4)
        {
            NameColumn();
            InputFloat4("", ref tmpVec4);
        }
        else if (value is Enum tmpEnum)
        {
            var tmpIndex = 0;
            NameColumn();
            Combo("", ref tmpIndex, tmpEnum.ToString());
        }
        else if (field.FieldType.IsValueType)
        {
            if (!TreeNodeEx(field.Name, default))
                return;
            var fields = field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var subField in fields)
                GenericField(subField.GetValue(value)!, subField);
        }
        else
        {
            NameColumn();
            Text(value.ToString());
        }
        TreePop();
    }
}
