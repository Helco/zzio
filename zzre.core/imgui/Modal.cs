using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class Modal : BaseWindow
    {
        private enum NextAction
        {
            None,
            Open,
            Close
        }

        private string uniqueId = $"###{Guid.NewGuid()}";
        private string FullTitle => Title + uniqueId;
        private bool isOpen = false;
        private NextAction nextAction = NextAction.None;

        public override bool IsOpen => isOpen && nextAction != NextAction.Close;
        public bool HasCloseButton { get; set; } = true;
        public event Action OnOpen = () => { };

        public Modal(WindowContainer container, string title = "Modal") : base(container, title) { }

        public override void Update()
        {
            var thisAction = nextAction;
            nextAction = NextAction.None;
            IsFocused = false;

            if (thisAction == NextAction.Close)
            {
                if (BeginPopupModal(FullTitle, ref isOpen, Flags))
                {
                    CloseCurrentPopup();
                    EndPopup();
                }
                isOpen = false;
                return;
            }

            if (thisAction == NextAction.Open)
            {
                OpenPopup(FullTitle);
                isOpen = true;
                OnOpen();
            }

            if (isOpen)
                RaiseBeforeContent();

            SetNextWindowPos(GetIO().DisplaySize / 2, ImGuiCond.Appearing, Vector2.One / 2);
            isOpen = HasCloseButton
                ? BeginPopupModal(FullTitle, ref isOpen, Flags)
                : ImGuiEx.BeginPopupModal(FullTitle, Flags);
            if (!isOpen)
                return;
            IsFocused = IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            RaiseContent();
            EndPopup();
        }

        public void Open()
        {
            nextAction = NextAction.Open;
            uniqueId = $"###{Guid.NewGuid()}";
        }
        public void Close() => nextAction = NextAction.Close;

    }
}
