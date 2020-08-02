using System;
using System.IO;
using System.Linq;
using zzio.utils;

namespace zzio.vfs
{
    public class FileResourcePool_OLD : IResourcePool_OLD
    {
        private readonly FilePath basePath;

        public FileResourcePool_OLD(string basePathString)
        {
            this.basePath = new FilePath(basePathString).Absolute;
        }

        public ResourceType_OLD GetResourceType(string path)
        {
            try
            {
                var attr = File.GetAttributes(basePath.Combine(path).ToString());
                if ((attr & FileAttributes.Directory) > 0)
                    return ResourceType_OLD.Directory;
                return ResourceType_OLD.File;
            }
            catch (IOException)
            {
                return ResourceType_OLD.NonExistant;
            }
        }

        public Stream GetFileContent(string path)
        {
            try
            {
                return new FileStream(basePath.Combine(path).ToString(),
                    FileMode.Open, FileAccess.Read);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string[] GetDirectoryContent(string path)
        {
            try
            {
                string realPath = basePath.Combine(path).ToString();
                return Directory.GetFiles(realPath)
                    .Concat(Directory.GetDirectories(realPath))
                    .Select(absPath => new FilePath(absPath).RelativeTo(realPath).ToPOSIXString())
                    .ToArray();
            }
            catch (Exception)
            {
                return new string[0];
            }
        }
    }
}
