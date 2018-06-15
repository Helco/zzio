using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace zzio.cli
{
    public class FileSelection
    {
        private bool changedSet = true;
        private HashSet<string> files;
        private string[] fileArray;

        public FileSelection()
        {
            files = new HashSet<string>();
        }

        public IReadOnlyList<string> Files
        {
            get
            {
                if (changedSet)
                {
                    fileArray = new string[files.Count];
                    files.CopyTo(fileArray);
                }
                return fileArray;
            }
        }

        public bool addSingleFile(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) > 0)
                {
                    bool success = false;
                    string[] files = Directory.GetFiles(Path.GetFullPath(path), "*", SearchOption.AllDirectories);
                    foreach (string p in files)
                    {
                        if (this.files.Add(p))
                            success = true;
                    }
                    changedSet = changedSet || success;
                    return success;
                }
                else
                {
                    if (files.Add(Path.GetFullPath(path)))
                        changedSet = true;
                    return true;
                }
            }
            catch(Exception)
            {
                return false;
            }
        }

        public bool addFilterDir(string path, string filter)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) == 0)
                    return false;
                string fullPath = Path.GetFullPath(path);
                if (!fullPath.EndsWith("" + Path.DirectorySeparatorChar))
                    fullPath += Path.DirectorySeparatorChar;
                string[] files = Directory.GetFiles(path, filter, SearchOption.AllDirectories);
                bool success = false;
                foreach (string f in files)
                {
                    if (this.files.Add(Path.Combine(fullPath, f)))
                        success = true;
                }
                changedSet = changedSet || success;
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool addRegexDir(string path, string regexFilter)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) == 0)
                    return false;
                string fullPath = Path.GetFullPath(path);
                if (!fullPath.EndsWith("" + Path.DirectorySeparatorChar))
                    fullPath += Path.DirectorySeparatorChar;
                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                Regex regex = new Regex(regexFilter);
                bool success = false;
                foreach(string f in files)
                {
                    if (regex.Match(f).Success && this.files.Add(Path.Combine(fullPath, f)))
                        success = true;
                }
                changedSet = changedSet || success;
                return success;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public bool addFromListFile(string file)
        {
            try
            {
                string fullDir = Path.GetDirectoryName(Path.GetFullPath(file));
                string[] lines = File.ReadAllLines(file);
                bool success = true;
                foreach(string s in lines)
                {
                    var f = s.Trim();
                    if (f.Length == 0 || f[0] == '#')
                        continue;
                    f = Path.Combine(fullDir, f);
                    if (!addSingleFile(f))
                        success = false;
                }
                return success;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public void addFromParameters (ParameterParser args)
        {
            List<object> entries = args["input", true] as List<object>;
            if (entries != null)
            {
                foreach (object s in entries)
                {
                    if (!addSingleFile(s as string))
                        Console.Error.WriteLine("Warning: did not include \"" + s + "\"");
                }
            }

            entries = args["finput", true] as List<object>;
            if (entries != null)
            {
                foreach(object s in entries)
                {
                    object[] ss = s as object[];
                    if (!addFilterDir(ss[0] as string, ss[1] as string))
                        Console.Error.WriteLine("Warning: did not include \"" + ss[0] + "\"");
                }
            }

            entries = args["rinput", true] as List<object>;
            if (entries != null)
            {
                foreach (object s in entries)
                {
                    object[] ss = s as object[];
                    if (!addRegexDir(ss[0] as string, ss[1] as string))
                        Console.Error.WriteLine("Warning: did not include \"" + ss[0] + "\"");
                }
            }

            entries = args["linput", true] as List<object>;
            if (entries != null)
            {
                foreach (object s in entries)
                {
                    if (!addFromListFile(s as string))
                        Console.Error.WriteLine("Warning: did not include at least one file from \"" + s + "\"");
                }
            }
        }
    }
}
