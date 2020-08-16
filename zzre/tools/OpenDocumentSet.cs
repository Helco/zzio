using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using zzio.vfs;
using zzre.imgui;

namespace zzre.tools
{
    public class OpenDocumentSet
    {
        private HashSet<IDocumentEditor> editors = new HashSet<IDocumentEditor>();

        public void AddEditor(IDocumentEditor editor)
        {
            if (!editors.Add(editor))
                return;
            editor.Window.OnClose += () => RemoveEditor(editor);
        }

        public void RemoveEditor(IDocumentEditor editor)
        {
            editors.Remove(editor);
        }

        public bool TryGetEditorFor(IResource resource, [NotNullWhen(true)] out IDocumentEditor? openEditor)
        {
            foreach (var editor in editors)
            {
                if (resource.Equals(editor.CurrentResource))
                {
                    openEditor = editor;
                    return true;
                }
            }

            openEditor = null;
            return false;
        }
    }
}
