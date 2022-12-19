using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO;

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
        private readonly Dictionary<string, PAKArchiveEntry> entries = new();
        private readonly Dictionary<string, FilePath> directories = new(); // use Dictionary to preserve case
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
            PAKArchive archive = new(baseStream);
            using BinaryReader reader = new(baseStream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt32() != 0)
                throw new InvalidDataException("Invalid PAKArchive magic (has to be 0x00000000)");

            uint count = reader.ReadUInt32();
            for (uint i = 0; i < count; i++)
            {
                PAKArchiveEntry entry = PAKArchiveEntry.ReadNew(reader);
                string key = getPathKey(entry.path);
                archive.entries[key] = entry;

                var parent = entry.path.Parent?.WithoutDirectoryMarker();
                while (parent?.StaysInbound ?? false)
                {
                    archive.directories[getPathKey(parent)] = parent;
                    parent = parent.Parent?.WithoutDirectoryMarker();
                }
            }
            archive.baseOffset = (uint)baseStream.Position;
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

        public string[] GetDirectoryContent(string pathString, bool recursive = true) =>
            GetContentIn(entries.Values.Select(e => e.path).Concat(directories.Values), pathString, recursive);

        public string[] GetFilesIn(string pathString, bool recursive = true) =>
            GetContentIn(entries.Values.Select(e => e.path), pathString, recursive);

        public string[] GetDirectoriesIn(string pathString, bool recursive = true) =>
            GetContentIn(directories.Values, pathString, recursive);

        private string[] GetContentIn(IEnumerable<FilePath> set, string pathString, bool recursive)
        {
            FilePath dirPath = new(pathString);
            if (!dirPath.StaysInbound)
                throw new InvalidOperationException("Queried path does not stay inbounds");

            var results = set
                .Select(filePath => filePath.RelativeTo(pathString, false))
                .Where(filePath => filePath.StaysInbound && filePath.Parts.Count > 0);

            if (!recursive)
                results = results.Where(filePath => filePath.Parts.Count == 1);
            return results
                .Select(filePath => filePath.ToPOSIXString())
                .ToArray();
        }
    }
}
