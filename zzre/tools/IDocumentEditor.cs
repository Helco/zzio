using zzio.vfs;
using zzre.imgui;

namespace zzre.tools;

public interface IDocumentEditor
{
    IResource? CurrentResource { get; }
    Window Window { get; }

    void Load(IResource resource);
}
