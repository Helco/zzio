using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace zzio.rwbs;

[Serializable]
public abstract class ListSection : Section
{
    public List<Section> children = new();

    protected override void readBody(Stream stream)
    {
        children.Clear();
        long streamLength = stream.Length;
        // there might be padding bytes, check if a header would fit
        while (streamLength - stream.Position >= 12)
        {
            children.Add(ReadNew(new GatekeeperStream(stream), this));
        }
    }

    protected override void writeBody(Stream stream)
    {
        children.ForEach((section) => section.Write(new GatekeeperStream(stream)));
    }

    public override Section? FindChildById(SectionId sectionId, bool recursive) => recursive
        ? FindChildById(sectionId, false) ?? children
            .Select(c => c.FindChildById(sectionId, recursive))
            .FirstNotNullOrNull()
        : children.FirstOrDefault(c => c.sectionId == sectionId);

    public override IEnumerable<Section> FindAllChildrenById(SectionId sectionId, bool recursive = true)
    {
        var result = children.Where(s => s.sectionId == sectionId);
        if (recursive)
            result = result.Concat(children.SelectMany(s => s.FindAllChildrenById(sectionId, recursive)));
        return result;
    }
}
