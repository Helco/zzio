using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using zzio.utils;
using System.Reflection.PortableExecutable;

namespace zzio
{
    public struct PAKArchiveEntry
    {
        public readonly FilePath path;
        public readonly uint offset, length;

        private PAKArchiveEntry(FilePath path, uint offset, uint length)
        {
            this.path = path;
            this.offset = offset;
            this.length = length;
        }

        public static PAKArchiveEntry ReadNew(BinaryReader reader)
        {
            return new PAKArchiveEntry(
                new FilePath("pak/").Combine(reader.ReadZString()).Normalized, // DATA_0.pak has a "../" prefix for every file...
                reader.ReadUInt32() + 4, // a file is surrounded by 4 bytes each
                reader.ReadUInt32() - 8
            );
        }
    }

    public class PAKArchive
    {
        private readonly Stream stream;
        private Dictionary<string, PAKArchiveEntry> entries = new Dictionary<string, PAKArchiveEntry>();
        private Dictionary<string, FilePath> directories = new Dictionary<string, FilePath>(); // use Dictionary to preserve case
        private uint baseOffset;

        private PAKArchive(Stream str)
        {
            stream = str;
        }   

        // transform a path into a comparable path string as key to the entry map
        private static string getPathKey(FilePath path) => path.Normalized.ToPOSIXString().ToLowerInvariant();

        public static PAKArchive ReadNew(Stream baseStream)
        {
            if (!baseStream.CanRead || !baseStream.CanSeek)
                throw new InvalidDataException("PAKArchive stream has to be readable and seekable");
            PAKArchive archive = new PAKArchive(baseStream);
            BinaryReader reader = new BinaryReader(baseStream);
            if (reader.ReadUInt32() != 0)
                throw new InvalidDataException("Invalid PAKArchive magic (has to be 0x00000000)");

            uint count = reader.ReadUInt32();
            for (uint i = 0; i < count; i++)
            {
                PAKArchiveEntry entry = PAKArchiveEntry.ReadNew(reader);
                string key = getPathKey(entry.path);
                archive.entries[key] = entry;

                var parent = entry.path.Parent;
                while (parent?.StaysInbound ?? false)
                {
                    archive.directories[getPathKey(parent)] = parent;
                    parent = parent.Parent;
                }
            }
            archive.baseOffset = (UInt32)baseStream.Position;
            return archive;
        }

        public bool ContainsFile(string pathString)
        {
            string pathKey = getPathKey(new FilePath(pathString));
            return entries.ContainsKey(pathKey);
        }

        public bool ContainsDirectory(string pathString) => directories.ContainsKey(getPathKey(new FilePath(pathString).Normalized));

        public Stream ReadFile(string pathString)
        {
            string pathKey = getPathKey(new FilePath(pathString));
            PAKArchiveEntry entry = entries[pathKey];
            stream.Position = baseOffset + entry.offset;
            return new RangeStream(stream, entry.length, false, false);
        }

        public string[] GetDirectoryContent(string pathString, bool recursive = true)
        {
            FilePath dirPath = new FilePath(pathString);
            if (!dirPath.StaysInbound)
                throw new InvalidOperationException("Queried path does not stay inbounds");

            var results = entries.Values
                .Select(entry => entry.path.RelativeTo(pathString, false))
                .Concat(directories.Values
                    .Select(entry => entry.RelativeTo(pathString, false))
                    .Where(filePath => filePath.StaysInbound)
                )
                .Where(filePath => filePath.StaysInbound && filePath.Parts.Length > 0);

            if (!recursive)
                results = results.Where(filePath => filePath.Parts.Length == 1);
            return results
                .Select(filePath => filePath.ToPOSIXString())
                .ToArray();
        }
    }
}
