using System;
using System.Collections.Generic;
using IconFonts;
using ImGuiNET;
using Microsoft.Extensions.Primitives;
using zzre.imgui;

using static ImGuiNET.ImGui;

namespace zzre.tools;

internal sealed class ConfigExplorer
{
    private readonly Configuration configuration;
    private readonly List<string> sortedKeys = new(512);
    private readonly List<(int pop, int push)> hierarchy = new(512);
    private string? selectedKey;
    private int keyVersion = -1;

    public Window Window { get; }
    
    public static void Open(ITagContainer diContainer)
    {
        if (diContainer.TryGetTag<ConfigExplorer>(out var prevExplorer))
            prevExplorer.Window.Focus();
        else
            diContainer.AddTag(new ConfigExplorer(diContainer));
    }

    private ConfigExplorer(ITagContainer diContainer)
    {
        configuration = diContainer.GetTag<Configuration>();
        Window = diContainer.GetTag<WindowContainer>().NewWindow("Config Explorer");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 400, 1000);
        Window.OnClose += () => diContainer.RemoveTag<ConfigExplorer>();
        Window.OnContent += HandleContent;
    }

    private void HandleContent()
    {
        if (keyVersion != configuration.KeyVersion)
        {
            keyVersion = configuration.KeyVersion;
            sortedKeys.Clear();
            sortedKeys.AddRange(configuration.Keys);
            sortedKeys.Sort(StringComparer.OrdinalIgnoreCase);

            hierarchy.Clear();
            hierarchy.EnsureCapacity(sortedKeys.Count);
            string[] prevParts = [""];
            foreach (var key in sortedKeys)
            {
                var curParts = key.Split('.');
                int equalParts;
                for (equalParts = 0;
                    equalParts < curParts.Length - 1 &&
                    equalParts < prevParts.Length - 1 &&
                    prevParts[equalParts] == curParts[equalParts];
                    equalParts++) ;

                var pop = prevParts.Length - 1 - equalParts;
                var push = curParts.Length - 1 - equalParts;
                hierarchy.Add((pop, push));
                prevParts = curParts;
            }
        }

        if (selectedKey is null)
            TextWrapped("");
        else
            TextWrapped(
                Configuration.TryGetMetadata(selectedKey)?.Description ??
                "No description available for this configuration key.");

        if (!BeginTable("Variables", 3,
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.SizingFixedFit |
            ImGuiTableFlags.ScrollX |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.Hideable |
            ImGuiTableFlags.NoSavedSettings,
            GetContentRegionAvail()))
            return;

        TableSetupColumn("Key", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide, 2f);
        TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide, 1f);
        TableSetupColumn("Source", ImGuiTableColumnFlags.DefaultHide, -1f);
        TableSetupScrollFreeze(0, 1);
        TableHeadersRow();

        var curDepth = 0;
        var targetDepth = 0;
        for (int i = 0; i < sortedKeys.Count; i++)
        {
            var tokenizer = new StringTokenizer(sortedKeys[i], ['.']).GetEnumerator();

            var (pop, push) = hierarchy[i];
            targetDepth -= pop;
            while (curDepth > targetDepth)
            {
                TreePop();
                curDepth--;
            }
            for (int j = 0; j < curDepth; j++)
                tokenizer.MoveNext();
            if (push > 0)
            {
                targetDepth += push;
                for (int j = 0; j < push; j++)
                {
                    tokenizer.MoveNext();
                    TableNextRow();
                    TableNextColumn();
                    if (!TreeNodeEx(tokenizer.Current.AsSpan(),
                        ImGuiTreeNodeFlags.DefaultOpen |
                        ImGuiTreeNodeFlags.OpenOnArrow |
                        ImGuiTreeNodeFlags.OpenOnDoubleClick))
                        break;
                    TableNextColumn();
                    TableNextColumn();
                    curDepth++;
                }
            }
            if (targetDepth == curDepth)
            {
                tokenizer.MoveNext();
                VariableRow(tokenizer.Current, sortedKeys[i]);
            }
        }
        for (; curDepth > 0; curDepth--)
            TreePop();

        EndTable();
    }

    private unsafe void VariableRow(StringSegment segment, string key)
    {
        bool isSelected = selectedKey == key;
        TableNextRow();
        TableNextColumn();
        Selectable(segment, ref isSelected,
            ImGuiSelectableFlags.AllowDoubleClick);
        if (isSelected)
            selectedKey = key;
        TableNextColumn();
        PushID(key);

        BeginDisabled(!configuration.IsOverwritten(key));
        if (SmallButton(ForkAwesome.History))
        {
            configuration.ResetValue(key);
            return; // to prevent us from using an outdate key mapping
        }
        EndDisabled();
        SameLine();

        PushItemWidth(-1);
        if (!configuration.TryGetValue(key, out var value))
            Text("???");
        else if (value.IsNumeric)
        {
            var metadata = Configuration.TryGetMetadata(key);
            if (metadata is { IsInteger: true })
            {
                long numeric = checked((long)value.Numeric);
                bool changed;
                if (double.IsFinite(metadata.Min) && double.IsFinite(metadata.Max))
                {
                    long min = (long)metadata.Min;
                    long max = (long)metadata.Max;
                    changed = DragScalar("", ImGuiDataType.S64, (nint)(&numeric), 1f, (nint)(&min), (nint)(&max));
                }
                else
                    changed = DragScalar("", ImGuiDataType.S64, (nint)(&numeric));
                if (changed)
                    configuration.SetValue(key, numeric);
            }
            else
            {
                double numeric = value.Numeric;
                bool changed;
                if (metadata is not null && double.IsFinite(metadata.Min) && double.IsFinite(metadata.Max))
                {
                    double min = metadata.Min;
                    double max = metadata.Max;
                    changed = DragScalar("", ImGuiDataType.Double, (nint)(&numeric), 1f, (nint)(&min), (nint)(&max));
                }
                else
                    changed = DragScalar("", ImGuiDataType.Double, (nint)(&numeric));
                if (changed)
                    configuration.SetValue(key, numeric);
            }
        }
        else
        {
            var text = value.String;
            if (InputText("", ref text, 1024, ImGuiInputTextFlags.EnterReturnsTrue))
                configuration.SetValue(key, text);
        }
        PopItemWidth();
        PopID();
        TableNextColumn();
        Text(configuration.GetControllingSourceName(key) ?? "");
    }
}
