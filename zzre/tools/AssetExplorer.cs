using System;
using System.Collections.Generic;
using ImGuiNET;
using zzre.imgui;

using static ImGuiNET.ImGui;
using static zzre.IAssetRegistryDebug;

namespace zzre.tools;

internal sealed class AssetRegistryList
{
    private readonly List<(string, IAssetRegistryDebug)> registries = [];
    public IReadOnlyList<(string Name, IAssetRegistryDebug Registry)> Registries
    {
        get
        {
            registries.RemoveAll(t => t.Item2.WasDisposed);
            return registries;
        }
    }
    public void Register(string name, IAssetRegistryDebug registry) => registries.Add((name, registry));

    internal AssetExplorer? OpenExplorer { get; set; }
}

internal sealed class AssetExplorer
{
    private readonly AssetRegistryList registryList;
    private readonly List<AssetInfo> assets = new(256);
    private Guid selectedRow;
    private bool focusSelectedRow;

    public Window Window { get; }
    public Guid SelectedRow
    {
        get => selectedRow;
        set
        {
            selectedRow = value;
            focusSelectedRow = true;
        }
    }

    public static void Open(ITagContainer diContainer)
    {
        var registryList = diContainer.GetTag<AssetRegistryList>();
        if (registryList.OpenExplorer == null)
            registryList.OpenExplorer = new AssetExplorer(diContainer);
        else
            registryList.OpenExplorer.Window.Focus();            
    }

    private AssetExplorer(ITagContainer diContainer)
    {
        registryList = diContainer.GetTag<AssetRegistryList>();
        Window = diContainer.GetTag<WindowContainer>().NewWindow("Asset Explorer");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 1000, 400);
        Window.OnClose += () => registryList.OpenExplorer = null;
        Window.OnContent += HandleContent;
    }

    private void HandleContent()
    {
        var registries = registryList.Registries;
        BeginTabBar("Registries", ImGuiTabBarFlags.None);
        foreach (var (name, registry) in registries)
        {
            if (BeginTabItem($"{name} ({registry.LocalStats.Total})###{name}"))
            {
                HandleContentFor(registry);
                EndTabItem();
            }
        }
        EndTabBar();
    }

    private enum Column
    {
        ID,
        Type,
        Name,
        RefCount,
        State,
        Priority
    }

    private void HandleContentFor(IAssetRegistryDebug registry)
    {
        registry.CopyDebugInfo(assets);
        if (!BeginTable("Assets", 6,
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Reorderable |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.Hideable |
            ImGuiTableFlags.Sortable |
            ImGuiTableFlags.SortMulti |
            ImGuiTableFlags.SortTristate |
            ImGuiTableFlags.SizingFixedFit |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.NoSavedSettings))
            return;

        TableSetupColumn("ID", default, 0f, (uint)Column.ID);
        TableSetupColumn("Type", ImGuiTableColumnFlags.DefaultHide, 0f, (uint)Column.Type);
        TableSetupColumn("Name", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide, 0f, (uint)Column.Name);
        TableSetupColumn("State", default, 0f, (uint)Column.State);
        TableSetupColumn("RefCount", default, 0f, (uint)Column.RefCount);
        TableSetupColumn("Priority", default, 0f, (uint)Column.Priority);
        TableSetupScrollFreeze(0, 1);
        TableHeadersRow();

        SortAssets();
        foreach (var asset in assets)
        {
            TableNextRow();
            int i = 0;
            if (TableSetColumnIndex(i++)) ImGuiEx.Text(asset.ID);
            if (TableSetColumnIndex(i++)) Text(asset.Type.Name);
            if (TableSetColumnIndex(i++))
                if (Selectable(asset.Name, selectedRow == asset.ID, ImGuiSelectableFlags.SpanAllColumns))
                    selectedRow = asset.ID;
            if (TableSetColumnIndex(i++)) ImGuiEx.Text(asset.State);
            if (TableSetColumnIndex(i++)) Text(asset.RefCount.ToString());
            if (TableSetColumnIndex(i++)) ImGuiEx.Text(asset.Priority);
            if (selectedRow == asset.ID && focusSelectedRow)
            {
                SetScrollHereY();
                focusSelectedRow = false;
            }
        }

        EndTable();
    }

    private unsafe void SortAssets()
    {
        var specs = TableGetSortSpecs();
        var columnSpecsArray = specs.Specs.NativePtr;
        if (specs.SpecsCount <= 0)
            return;
        var guidComparer = Comparer<Guid>.Default;
        var stringComparer = Comparer<string>.Default;
        assets.Sort((a, b) =>
        {
            for (int i = 0; i < specs.SpecsCount; i++)
            {
                var columnSpecs = new ImGuiTableColumnSortSpecsPtr(columnSpecsArray + i);
                var comp = (Column)columnSpecs.ColumnUserID switch
                {
                    Column.ID => guidComparer.Compare(a.ID, b.ID),
                    Column.Type => stringComparer.Compare(a.Type.Name, b.Type.Name),
                    Column.Name => stringComparer.Compare(a.Name, b.Name),
                    Column.RefCount => b.RefCount - a.RefCount,
                    Column.State => (int)b.State - (int)a.State,
                    Column.Priority => (int)b.Priority - (int)a.Priority,
                    _ => 0
                };
                if (comp != 0)
                    return columnSpecs.SortDirection == ImGuiSortDirection.Ascending ? comp : -comp;
            }
            return 0;
        });
    }
}
