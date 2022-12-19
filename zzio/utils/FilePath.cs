using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using zzio;

namespace zzio
{
    /// <summary>Represents a path to a file</summary>
    /// <remarks>This supports windows and POSIX paths and tries to ignore as much inconsistencies as possible</remarks> 
    public sealed class FilePath : IEquatable<FilePath>, IEquatable<string>
    {
        public const bool DefaultsToCaseSensitive = false;

        /// <value>The platform-dependant path separator</value>
        public static string Separator
        {
            get
            {
                return Environment.OSVersion.Platform == PlatformID.Win32NT ? "\\" : "/";
            }
        }

        private static readonly char[] separators = new char[] { '/', '\\' };

        private enum PathType
        {
            Relative, // e.g. "abc/def" or "~/ert" or "../yxc/"
            Root,     // e.g. "/abc"
            Drive     // e.g. "c:/asdk/cxv" or "file:/xcvqw" or even "pak://bla"
        }

        private readonly IReadOnlyList<string> parts;
        private readonly PathType type;
        public bool IsDirectory { get; }

        private FilePath(IReadOnlyList<string> parts, PathType type, bool isDirectory)
        {
            this.parts = parts;
            this.type = type;
            IsDirectory = isDirectory;
        }

        private static bool hasDrivePart(IReadOnlyList<string> parts) => parts.Any(p => p.EndsWith(":"));

        private static bool isDirectoryPart(string part)
        {
            return part == "." || part == ".." || part == "~" || part.EndsWith(":");
        }

        /// <summary>Constructs a new path out of a string</summary>
        public FilePath(string path)
        {
            path = path.Trim();
            if (path.Length == 0)
            {
                parts = Array.Empty<string>();
                type = PathType.Relative;
                IsDirectory = false;
                return;
            }
            parts = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            type = PathType.Relative;
            if (path.IndexOfAny(separators) == 0)
                type = PathType.Root;
            else if (hasDrivePart(parts))
                type = PathType.Drive;
            IsDirectory =
                path.LastIndexOfAny(separators) == path.Length - 1 ||
                parts.Count > 0 && isDirectoryPart(parts.Last());
        }

        /// <summary>Constructs a new path as copy of another one</summary> 
        public FilePath(FilePath path) : this(path.parts, path.type, path.IsDirectory)
        {
        }

        /// <summary>Compares two paths for equality</summary>
        /// <remarks>Case-sensitivity is dependant of the current platform</remarks>
        public bool Equals(string? path)
        {
            return path != null && Equals(new FilePath(path));
        }

        /// <summary>Compares two paths for equality</summary>
        public bool Equals(string? path, bool caseSensitive)
        {
            return path != null && Equals(new FilePath(path), caseSensitive);
        }

        /// <summary>Compares two paths for equality</summary>
        public bool Equals(FilePath? path) => path != null && Equals(path, DefaultsToCaseSensitive);

        /// <summary>Compares two paths for equality</summary>
        public bool Equals(FilePath? path, bool caseSensitive)
        {
            if (path == null)
                return false;
            FilePath me = Absolute;
            path = path.Absolute;
            if (me.parts.Count != path.parts.Count || me.type != path.type)
                return false;
            StringComparison comp = caseSensitive
                ? StringComparison.InvariantCulture
                : StringComparison.InvariantCultureIgnoreCase;
            for (int i = 0; i < me.parts.Count; i++)
            {
                if (!string.Equals(me.parts[i], path.parts[i], comp))
                    return false;
            }
            return true;
        }

        /// <summary>Compares two paths for equality</summary>
        /// <remarks>Case-sensitivity is dependant of the current platform</remarks>
        public static bool operator ==(FilePath? pathA, object? pathB)
        {
            if (ReferenceEquals(pathA, null))
                return ReferenceEquals(pathB, null);
            return pathA.Equals(pathB);
        }


        /// <summary>Compares two paths for inequality</summary>
        /// <remarks>Case-sensitivity is dependant of the current platform</remarks>
        public static bool operator !=(FilePath? pathA, object? pathB)
        {
            if (ReferenceEquals(pathA, null))
                return !ReferenceEquals(pathB, null);
            return !pathA.Equals(pathB);
        }

        public override bool Equals(object? obj)
        {
            if (obj is FilePath)
                return Equals(obj as FilePath);
            else if (obj is string)
                return Equals(obj as string);
            return false;
        }

        public override int GetHashCode()
        {
            // simple hash combine with XOR and some random constants
            int hash = 0;
            foreach (string part in Normalized.Parts)
                hash = hash << 2 ^ part.ToLowerInvariant().GetHashCode();
            hash = hash << 2 ^ (int)type * 0x3fa5bde0;
            return hash;
        }

        /// <value>The root for this path</value>
        /// <remarks>For unix this is always "/", for windows it is the drive letter (in the path or current)</remarks>
        public FilePath Root
        {
            get
            {
                if (type == PathType.Relative)
                    return new FilePath(Environment.CurrentDirectory).Root;
                else if (type == PathType.Root)
                    return new FilePath(Array.Empty<string>(), PathType.Root, true);
                else if (type == PathType.Drive)
                {
                    string drive = parts.Last(part => part.EndsWith(":"));
                    return new FilePath(new string[] { drive }, PathType.Drive, true);
                }
                else
                    throw new NotSupportedException("PathType \"" + type + "\" not yet supported in Root property");
            }
        }

        /// <summary>Combines this path with some other paths</summary>
        /// <remarks>The combined path is normalized</remarks>
        public FilePath Combine(params string[] paths)
        {
            return Combine((IEnumerable<string>)paths);
        }

        /// <summary>Combines this path with some other paths</summary>
        /// <remarks>The combined path is normalized</remarks>
        public FilePath Combine(IEnumerable<string> paths)
        {
            return Combine(paths.Select(pathString => new FilePath(pathString)));
        }

        /// <summary>Combines this path with some other paths</summary>
        /// <remarks>The combined path is normalized</remarks>
        public FilePath Combine(params FilePath[] paths)
        {
            return Combine((IEnumerable<FilePath>)paths);
        }

        /// <summary>Combines this path with some other paths</summary>
        /// <remarks>The combined path is normalized</remarks>
        public FilePath Combine(IEnumerable<FilePath> paths)
        {
            List<string> newParts = new(parts.Count + paths.Sum(p => p.parts.Count));
            newParts.AddRange(parts);

            bool lastIsDirectory = IsDirectory;
            foreach (FilePath path in paths)
            {
                if (path.type != PathType.Relative)
                    throw new InvalidOperationException("Only relative paths can be combined");
                newParts.AddRange(path.parts);
                lastIsDirectory = path.IsDirectory;
            }
            return new FilePath(newParts, type, lastIsDirectory).Normalized;
        }

        /// <value>Normalizes this path by removing unnecessary navigation</value>
        public FilePath Normalized
        {
            get
            {
                List<string> newParts = new();
                foreach (string part in parts)
                {
                    if (part == ".")
                        continue;
                    else if (part == ".." && newParts.Count > 0 && newParts.Last() != "..")
                        newParts.RemoveAt(newParts.Count - 1);
                    else if (part == "~" || part.EndsWith(":"))
                    {
                        newParts.Clear();
                        newParts.Add(part);
                    }
                    else
                        newParts.Add(part);
                }
                bool newIsDirectory = IsDirectory ||
                    newParts.Count > 0 && isDirectoryPart(newParts.Last());
                return new FilePath(newParts, type, newIsDirectory);
            }
        }

        /// <value>The absolute and normalized path (based on the current directory)</value>
        public FilePath Absolute => type == PathType.Relative
            ? new FilePath(Environment.CurrentDirectory).Combine(this)
            : Normalized;

        /// <value>Wether a path is absolute (or relative)</value>
        public bool IsAbsolute => type != PathType.Relative;

        /// <value>An array of all parts of this path without the separators</value>
        public IReadOnlyList<string> Parts => parts;

        /// <value>Whether the path stays in the boundary of its base</value>
        public bool StaysInbound
        {
            get
            {
                int minNested = type == PathType.Drive ? 1 : 0;
                int nested = 0;
                FilePath norm = Normalized;
                foreach (string part in norm.parts)
                {
                    if (part == "..")
                    {
                        if (--nested < minNested)
                            return false;
                    }
                    else
                        nested++;
                }
                return nested >= minNested;
            }
        }

        /// <value>The containing normalized path of this one or `null` if rooted</value>
        public FilePath? Parent
        {
            get
            {
                FilePath norm = Normalized;
                if (!norm.StaysInbound)
                    return norm.Combine("../");
                else if (norm.parts.Count > 1)
                    return new FilePath(norm.parts.Take(norm.parts.Count - 1).ToArray(), type, true);
                else if (type != PathType.Relative)
                    return null;
                else if (norm.parts.Count == 1)
                    return new FilePath("./");
                else
                    return new FilePath("../");
            }
        }

        /// <summary>Returns the normalized path navigating to `this` from `basePath`</summary>
        /// <remarks>Case-sensitivity is dependant of the current platform</remarks>
        public FilePath RelativeTo(string basePath)
        {
            return RelativeTo(new FilePath(basePath));
        }

        /// <summary>Returns the normalized path navigating to `this` from `basePath`</summary>
        public FilePath RelativeTo(string basePath, bool caseSensitive)
        {
            return RelativeTo(new FilePath(basePath), caseSensitive);
        }

        /// <summary>Returns the normalized path navigating to `this` from `basePath`</summary>
        public FilePath RelativeTo(FilePath basePath, bool caseSensitive = DefaultsToCaseSensitive)
        {
            FilePath me = Absolute;
            basePath = basePath.Absolute;
            if (me.Root != basePath.Root)
                throw new InvalidOperationException("Sub- and base path have different roots");

            // Skip over equal parts
            StringComparison comp = caseSensitive
                ? StringComparison.InvariantCulture
                : StringComparison.InvariantCultureIgnoreCase;
            int minPartCount = Math.Min(me.parts.Count, basePath.parts.Count);
            int partI = 0;
            for (; partI < minPartCount; partI++)
            {
                if (!string.Equals(me.parts[partI], basePath.parts[partI], comp))
                    break;
            }

            // Case 1: "a/b/c" relativeto "a/b/d" should return "../c"
            if (partI < me.parts.Count && partI < basePath.parts.Count)
            {
                List<string> newParts = new(basePath.parts.Count + me.parts.Count - partI);
                for (int i = partI; i < basePath.parts.Count; i++)
                    newParts.Add("..");
                newParts.AddRange(me.parts.Skip(partI));
                return new FilePath(newParts, PathType.Relative, IsDirectory);
            }
            // Case 2: "a/b/c/d" relative to "a/b" should return "c/d"
            else if (partI < me.parts.Count)
            {
                return new FilePath(me.parts.Skip(partI).ToArray(), PathType.Relative, IsDirectory);
            }
            // Case 3: "a/b" relative to "a/b/c/d" should return "../../"
            else if (partI < basePath.parts.Count)
            {
                string[] newParts = new string[basePath.parts.Count - partI];
                for (int i = 0; i < newParts.Length; i++)
                    newParts[i] = "..";
                return new FilePath(newParts, PathType.Relative, true);
            }
            // Case 4: "a/b/c" relative to "a/b/c" should return ""
            else
                return new FilePath(Array.Empty<string>(), PathType.Relative, false);

            // Only to silent the compiler
            throw new InvalidOperationException("This should never happen");
        }

        public FilePath WithoutDirectoryMarker() => new(parts, type, false);

        public string? Extension
        {
            get
            {
                if (IsDirectory || parts.Count == 0)
                    return null;
                int extensionMarker = parts.Last().LastIndexOf('.');
                if (extensionMarker < 0 || extensionMarker + 1 == parts.Last().Length)
                    return null;
                return parts.Last()[(extensionMarker + 1)..];
            }
        }

        /// <summary>Returns this path as windows path string ('\' as separator)</summary>
        /// <remarks>POSIX rooted paths (e.g. '/a/b') cannot be printed as windows path</remarks>
        public string ToWin32String()
        {
            if (type == PathType.Root)
                throw new NotSupportedException("Rooted paths are not supported as windows paths");
            StringBuilder result = new();
            foreach (string part in parts)
            {
                if (result.Length > 0)
                    result.Append('\\');
                result.Append(part);
            }
            if (IsDirectory && parts.Count > 0)
                result.Append('\\');
            return result.ToString();
        }

        /// <summary>Returns this path as POSIX path string ('/' as separator)</summary>
        public string ToPOSIXString()
        {
            StringBuilder result = new();
            foreach (string part in parts)
            {
                if (result.Length > 0 || type == PathType.Root)
                    result.Append('/');
                result.Append(part);
            }
            if (IsDirectory && parts.Count > 0)
                result.Append('/');

            if (type == PathType.Root && parts.Count == 0)
                return "/";
            return result.ToString();
        }

        /// <summary>Returns this path as string complying with the current platform</summary>
        public override string ToString() => Environment.OSVersion.Platform == PlatformID.Win32NT
            ? ToWin32String()
            : ToPOSIXString();
    }
}
