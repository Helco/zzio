using System;
using System.IO;

namespace zzio.scn
{
    public interface ISceneSection
    {
        void read(Stream stream);
        void write(Stream stream);
    }
}
