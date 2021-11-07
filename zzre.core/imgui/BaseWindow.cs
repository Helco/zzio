using ImGuiNET;
using System;
using Veldrid;

namespace zzre.imgui
{
    public abstract class BaseWindow : TagContainer
    {
        public WindowContainer Container { get; }
        public string Title { get; set; }
        public ImGuiWindowFlags Flags { get; set; }
        public abstract bool IsOpen { get; }
        public bool IsFocused { get; protected set; } = false;

        public event Action OnRender = () => { };
        public event Action OnBeforeContent = () => { };
        public event Action OnContent = () => { };
        public event Action<Key> OnKeyDown = _ => { };
        public event Action<Key> OnKeyUp = _ => { };

        protected BaseWindow(WindowContainer container, string title)
        {
            Container = container;
            Title = title;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Container.RemoveWindow(this);
        }

        protected void RaiseBeforeContent() => OnBeforeContent();
        protected void RaiseContent() => OnContent();
        public void HandleKeyEvent(Key sym, bool isDown) => (isDown ? OnKeyDown : OnKeyUp)(sym);
        public void HandleRender() => OnRender();

        public abstract void Update();
    }
}
