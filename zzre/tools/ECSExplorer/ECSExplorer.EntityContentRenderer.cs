namespace zzre.tools;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using DefaultEcs.Serialization;
using ImGuiNET;
using static ImGuiNET.ImGui;

internal partial class ECSExplorer
{
    public delegate void RenderComponentFunc<T>(in T component, in DefaultEcs.Entity entity);

    private abstract class ComponentRenderer : IComparable<ComponentRenderer>
    {
        public int Priority { get; init; }

        public int CompareTo(ComponentRenderer? other)
        {
            var otherPriority = other?.Priority ?? int.MinValue;
            return otherPriority - Priority; // swapped, that high priority is at the start of the list
        }
    }

    private sealed class ComponentRenderer<T> : ComponentRenderer
    {
        public RenderComponentFunc<T>? Render { get; init; }
    }

    private static readonly Dictionary<Type, LazySortedList<ComponentRenderer>> componentRenderers = [];

    public static void AddComponentRenderer<T>(int prio, RenderComponentFunc<T> render)
    {
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

    private sealed class EntityContentRenderer : IComponentReader
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

    private static void GenericComponentRenderer<T>(in T component) => GenericField(typeof(T).Name, component);

    private static void GenericField(string name, object? value)
    {
        void NameColumn()
        {
            TreeNodeEx(name, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
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
        else if (value is byte tmpByte)
        {
            NameColumn();
            int tmptmpInt = tmpByte;
            InputInt("", ref tmptmpInt);
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
        else if (value.GetType().IsValueType)
        {
            var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (properties.Length == 0 && fields.Length == 0)
            {
                NameColumn();
                Text("<marker>");
            }
            else
            {
                if (!TreeNodeEx(name, default))
                    return;
                foreach (var prop in properties)
                {
                    if (prop.GetMethod?.GetParameters().Length == 0)
                        GenericField(prop.Name, prop.GetValue(value));
                }
                foreach (var subField in fields)
                    GenericField(subField.Name, subField.GetValue(value));
            }
        }
        else
        {
            NameColumn();
            TextWrapped(value.ToString() + " (reference)");
        }
        TreePop();
    }
}
