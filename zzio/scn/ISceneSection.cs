using System;
using System.IO;

namespace zzio.scn
{
    public interface ISceneSection
    {
        void Read(Stream stream);
        void Write(Stream stream);
    }
}
