using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using zzio.utils;
using zzio.vfs;
using zzre.imgui;

namespace zzre.tools
{
    public class OpenDocumentSet
    {
        private readonly ITagContainer diContainer;
        private HashSet<IDocumentEditor> editors = new HashSet<IDocumentEditor>();

        public OpenDocumentSet(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
        }

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

        public TEditor OpenWith<TEditor>(string pathText) where TEditor : IDocumentEditor =>
            OpenWith<TEditor>(new FilePath(pathText));

        public TEditor OpenWith<TEditor>(FilePath path) where TEditor : IDocumentEditor
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            var resource = resourcePool.FindFile(path.ToPOSIXString());
            if (resource == null)
                throw new FileNotFoundException($"Could not find resource at {path.ToPOSIXString()}");
            return OpenWith<TEditor>(resource);
        }

        public TEditor OpenWith<TEditor>(IResource resource) where TEditor : IDocumentEditor
        {
            if (TryGetEditorFor(resource, out var prevEditor))
            {
                prevEditor.Window.Container.OnceAfterUpdate += prevEditor.Window.Focus;
                return (TEditor)prevEditor;
            }
            var ctor = GetConstructorFor<TEditor>();
            var newEditor = ctor(diContainer);
            newEditor.Load(resource);
            return newEditor;
        }

        static Dictionary<Type, Func<ITagContainer, object>> knownConstructors = new Dictionary<Type, Func<ITagContainer, object>>();
        private static Func<ITagContainer, TEditor> GetConstructorFor<TEditor>() where TEditor : IDocumentEditor
        {
            var type = typeof(TEditor);
            if (knownConstructors.TryGetValue(type, out var prevCtor))
                return diContainer => (TEditor)prevCtor(diContainer);

            var constructor = type.GetConstructor(new[] { typeof(ITagContainer) });
            if (constructor == null)
                throw new InvalidProgramException($"Editor {type} has no compatible constructor");
            var ctor = knownConstructors[type] = diContainer => constructor.Invoke(new object[] { diContainer });
            return diContainer => (TEditor)ctor(diContainer);
        }
    }
}
