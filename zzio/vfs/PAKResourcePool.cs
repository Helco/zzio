using System;
using System.IO;
using zzio;
using zzio.utils;

namespace zzio.vfs
{
    public class PAKResourcePool : IResourcePool
    {
        private readonly FilePath basePath;
        private readonly PAKArchive archive;

        public PAKResourcePool(string archivePath)
        {
            string[] parts = archivePath.Split(new char[] { '?' }, StringSplitOptions.RemoveEmptyEntries);
            FileStream stream = new FileStream(parts[0], FileMode.Open, FileAccess.Read);
            archive = PAKArchive.ReadNew(stream);
            basePath = new FilePath(parts.Length > 1 ? parts[1] : "");
        }

        public string[] GetDirectoryContent(string pathString)
        {
            FilePath path = basePath.Combine(pathString);
            return archive.GetDirectoryContent(path.ToWin32String(), false);
        }

        public Stream GetFileContent(string pathString)
        {
            try
            {
                FilePath path = basePath.Combine(pathString);
                return archive.ReadFile(path.ToWin32String());
            }
            catch(Exception)
            {
                return null;
            }
        }

        public ResourceType GetResourceType(string path)
        {
            Stream fileContent = GetFileContent(path);
            if (fileContent != null)
            {
                fileContent.Close();
                return ResourceType.File;
            }

            string[] dirContent = GetDirectoryContent(path);
            if (dirContent.Length != 0)
                return ResourceType.Directory;

            return ResourceType.NonExistant;
        }
    }
}
