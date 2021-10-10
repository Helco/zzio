using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using zzio.vfs;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class OpenFileModal : BaseDisposable
    {
        private const float FilterColumnWidth = 75.0f;
        private const float FileTreeSize = 400.0f;

        private readonly IResourcePool pool;
        private List<(IResource res, string nameWithId, int depth)> content = new List<(IResource, string, int)>();
        private HashSet<IResource> openDirectories = new HashSet<IResource>();
        private string filterText = "";
        private Regex filterRegex = new Regex("");
        private bool isFirstTreeContent = true;

        public Modal Modal { get; }
        public bool IsFilterChangeable { get; set; } = true;
        public string Filter
        {
            get => filterText;
            set
            {
                filterText = value;
                filterRegex = new Regex(
                    Regex.Escape(value).Replace("\\*", @"[^\/\\]*").Replace("\\?", "."),
                    RegexOptions.IgnoreCase);
            }
        }
        public IResource? SelectedResource { get; private set; } = null;
        public IResource? InitialSelectedResource { get; set; } = null;
        public event Action<IResource> OnOpenedResource = _ => { };

        public OpenFileModal(ITagContainer diContainer)
        {
            var windowContainer = diContainer.GetTag<WindowContainer>();
            pool = diContainer.GetTag<IResourcePool>();

            Modal = windowContainer.NewModal("Open file");
            Modal.Flags = ImGuiWindowFlags.NoResize;
            Modal.HasCloseButton = false;
            Modal.AddTag(this);
            Modal.OnContent += HandleContent;
            Modal.OnOpen += HandleOpen;
        }

        protected override void DisposeManaged() => Modal.Dispose();

        private void HandleOpen()
        {
            openDirectories.Clear();
            content.Clear();
            OpenResource(pool.Root, -1, -1);
            SelectedResource = InitialSelectedResource;
            MakeSelectedVisible();
            isFirstTreeContent = true;
        }

        private void HandleContent()
        {
            var Invalid = new Vector2(float.NaN, float.NaN);

            var selectedName = SelectedResource?.Path.ToPOSIXString() ?? "";
            PushItemWidth(-1.0f);
            InputText("", ref selectedName, 512, ImGuiInputTextFlags.ReadOnly);
            PopItemWidth();
            SetNextWindowSizeConstraints(Vector2.One * FileTreeSize, Vector2.One * FileTreeSize * 10);
            var initialTreeSize = GetContentRegionAvail() - GetTextLineHeightWithSpacing() * 2 * Vector2.UnitY;
            BeginChild("TreeChildWindow", initialTreeSize, true, ImGuiWindowFlags.HorizontalScrollbar);
            TreeContent();
            EndChild();

            Columns(2);
            if (SelectedResource == null)
                PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
            if (Button("Open") && SelectedResource != null)
            {
                OnOpenedResource(SelectedResource);
                Modal.Close();
            }
            if (SelectedResource == null)
                PopStyleVar(1);
            SameLine();
            if (Button("Cancel"))
            {
                Modal.Close();
            }

            NextColumn();
            if (ImGuiEx.InputTextWithHint("Filter", "e.g. *.dff;*.bsp", ref filterText, 512, IsFilterChangeable ? 0 : ImGuiInputTextFlags.ReadOnly))
                Filter = filterText; // to set the regex
            Columns(1);

        }

        private void TreeContent()
        {
            Action? onAfterTree = null;
            int curDepth = 0;
            foreach (var ((resource, nameWithId, depth), index) in content.Indexed())
            {
                while (depth < curDepth)
                {
                    TreePop();
                    curDepth--;
                }

                if (resource.Type == ResourceType.File && !filterRegex.IsMatch(resource.Path.ToPOSIXString()))
                    continue;

                var flags =
                    ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    (resource.Equals(SelectedResource) ? ImGuiTreeNodeFlags.Selected : 0) |
                    (resource.Type == ResourceType.File ? ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet : 0) |
                    (openDirectories.Contains(resource) ? ImGuiTreeNodeFlags.DefaultOpen : 0);
                var isNodeOpen = TreeNodeEx(nameWithId, flags) && resource.Type == ResourceType.Directory;
                if (isFirstTreeContent && resource.Equals(SelectedResource))
                    SetScrollHereY();

                if (IsItemClicked())
                    SelectedResource = resource;
                if (IsItemHovered() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    SelectedResource = resource;
                    OnOpenedResource(resource);
                    Modal.Close();
                }
                if (isNodeOpen != openDirectories.Contains(resource))
                {
                    // overwrite to force only one modification at a time
                    if (isNodeOpen)
                        onAfterTree = () => OpenResource(resource, index, depth);
                    else
                        onAfterTree = () => CloseResource(resource, index, depth);
                }
                if (isNodeOpen)
                    curDepth++;
            }
            while (curDepth > 0)
            {
                curDepth--;
                TreePop();
            }
            onAfterTree?.Invoke();
            isFirstTreeContent = false;
        }

        private void OpenResource(IResource resource, int parentIndex, int depth)
        {
            string GetNameWithIdFor(IResource resource) =>
                $"{resource.Name}##{resource.Path.ToPOSIXString()}";

            if (parentIndex >= 0)
                content[parentIndex] = (resource, GetNameWithIdFor(resource), depth);
            content.InsertRange(parentIndex + 1,
                resource.Directories.OrderBy(r => r.Name)
                .Concat(resource.Files.OrderBy(r => r.Name))
                .Select(r => (r, GetNameWithIdFor(r), depth + 1)));
            openDirectories.Add(resource);
        }

        private void CloseResource(IResource resource, int parentIndex, int depth)
        {
            int nextValidIndex = parentIndex + 1;
            for (; nextValidIndex < content.Count; nextValidIndex++)
            {
                if (content[nextValidIndex].depth <= depth)
                    break;
                openDirectories.Remove(content[nextValidIndex].res);
            }
            content.RemoveRange(parentIndex + 1, nextValidIndex - parentIndex - 1);
            openDirectories.Remove(resource);
        }

        private void MakeSelectedVisible()
        {
            var curResource = SelectedResource?.Parent;
            var parentStack = new Stack<IResource>();
            while (curResource != null && curResource.Parent != null)
            {
                parentStack.Push(curResource);
                curResource = curResource.Parent;
            }

            foreach (var dir in parentStack)
            {
                var dirIndex = content.IndexOf(t => t.res.Equals(dir));
                OpenResource(dir, dirIndex, content[dirIndex].depth);
            }
        }
    }
}
