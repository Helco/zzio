using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using zzio.vfs;
using System.Linq;
using zzio;

namespace zzre.tools;

public class OpenDocumentSet
{
    private readonly ITagContainer diContainer;
    private readonly HashSet<IDocumentEditor> editors = new();
    private readonly Dictionary<string, Type> editorTypes = new();

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
        openEditor = editors.FirstOrDefault(editor => resource.Equals(editor.CurrentResource));
        return openEditor != null;
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

    public void AddEditorType<TEditor>(string extension) where TEditor : IDocumentEditor
    {
        editorTypes.Add(extension.ToLowerInvariant(), typeof(TEditor));
        _ = GetConstructorFor<TEditor>();
    }

    public IDocumentEditor Open(string pathText) =>
        Open(new FilePath(pathText));

    public IDocumentEditor Open(FilePath path)
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var resource = resourcePool.FindFile(path.ToPOSIXString());
        if (resource == null)
            throw new FileNotFoundException($"Could not find resource at {path.ToPOSIXString()}");
        return Open(resource);
    }

    public IDocumentEditor Open(IResource resource)
    {
        var extension = resource.Path.Extension;
        if (resource.Type is not ResourceType.File || string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Given resource is not a file or does not have an extension");
        if (!editorTypes.TryGetValue(extension.ToLowerInvariant(), out var editorType))
            throw new KeyNotFoundException($"No editor registered for extension {extension}");
        var ctor = knownConstructors[editorType];
        var newEditor = (IDocumentEditor)ctor(diContainer);
        newEditor.Load(resource);
        return newEditor;
    }

    private static readonly Dictionary<Type, Func<ITagContainer, object>> knownConstructors = new();
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
