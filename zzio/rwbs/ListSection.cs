using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public abstract class ListSection : Section
    {
        public List<Section> children = new List<Section>();

        protected override void readBody(Stream stream)
        {
            children.Clear();
            long streamLength = stream.Length;
            // there might be padding bytes, check if a header would fit
            while (streamLength - stream.Position > 12)
            {
                children.Add(Section.ReadNew(stream, this));
            }
        }

        protected override void writeBody(Stream stream)
        {
            children.ForEach((section) => section.Write(stream));
        }

        public override Section FindChildById(SectionId sectionId, bool recursive)
        {
            foreach (Section child in children)
            {
                if (child.sectionId == sectionId)
                    return child;
            }
            if (recursive)
            {
                foreach (Section child in children)
                {
                    Section grandchild = child.FindChildById(sectionId, recursive);
                    if (grandchild != null)
                        return grandchild;
                }
            }
            return null;
        }
    }
}
