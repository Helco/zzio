using System;
using System.IO;

namespace zzio.cli;

public static class OutputHelper
{
    private class HandleGuard
    {
        ~HandleGuard()
        {
            if (stdoutStream != null && stdoutStream.CanWrite)
            {
                stdoutWriter.Flush();
                stdoutStream.Close();
            }
            if (stderrStream != null && stdoutStream.CanWrite)
            {
                stderrWriter.Flush();
                stderrStream.Close();
            }
            stdoutStream = stderrStream = null;
            stdoutWriter = stderrWriter = null;
        }
    }
    private static readonly HandleGuard guard = new();
    private static FileStream stdoutStream = null;
    private static FileStream stderrStream = null;
    private static StreamWriter stdoutWriter = null;
    private static StreamWriter stderrWriter = null;

    public static void initialize(ParameterParser args)
    {
        if (args["stdout"] != null)
        {
            if (stdoutStream != null)
            {
                stdoutWriter.Flush();
                stdoutStream.Close();
            }
            try
            {
                stdoutStream = new FileStream(args["stdout"] as string, FileMode.OpenOrCreate, FileAccess.Write);
                stdoutWriter = new StreamWriter(stdoutStream)
                {
                    AutoFlush = true
                };
                Console.SetOut(stdoutWriter);
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Could not redirect stdout to \"" + (args["stdout"] as string) + "\"");
            }
        }
        else if (args["stderr"] != null)
        {
            if (stderrStream != null)
            {
                stderrWriter.Flush();
                stderrStream.Close();
            }
            try
            {
                stderrStream = new FileStream(args["stderr"] as string, FileMode.OpenOrCreate, FileAccess.Write);
                stderrWriter = new StreamWriter(stderrStream)
                {
                    AutoFlush = true
                };
                Console.SetError(stderrWriter);
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Could not redirect stderr to \"" + (args["stderr"] as string) + "\"");
            }
        }
    }
}

internal class Program
{
    private static readonly ParameterInfo[] paramTypes = {
        new ParameterInfo('h', "help", "Displays this usage manual and returns", null),
        new ParameterInfo('\0', "stdout", "<file> - Redirects stdout to a file", ParameterType.Text, null),
        new ParameterInfo('\0', "stderr", "<file> - Redirects stderr to a file", ParameterType.Text, null),
        new ParameterInfo('i', "input", "<file> - Includes a single file or an unfiltered directory", ParameterType.Text, null),
        new ParameterInfo('f', "finput", "<dir> <pattern> - Includes the pattern filtered content of a directory",
            new ParameterType[] { ParameterType.Text, ParameterType.Text }, null),
        new ParameterInfo('r', "rinput", "<dir> <regex> - Includes the regex filtered content of a directory",
            new ParameterType[] { ParameterType.Text, ParameterType.Text }, null),
        new ParameterInfo('l', "linput", "<list-file> - Includes a list of files (like -i), read from a file", ParameterType.Text, null),
        new ParameterInfo('o', "output", "<dir> - Sets the output directory", ParameterType.Text, "./"),
        //new ParameterInfo('\0', "keep-hierachy", "keeping (most) of the original file hierachy", ParameterType.Boolean, true)

        new ParameterInfo('t', "target", "<format> - Sets the conversions for all files that can be converted into <format>", ParameterType.Text, null),
        new ParameterInfo('\0', "from-to", "<from> <to> - Sets the conversion from format <from> to format <to>",
            new ParameterType[] { ParameterType.Text, ParameterType.Text }, null),

        new ParameterInfo('\0', "map-db", "mapping of the database (more strict input rules)", ParameterType.Boolean, true),
        new ParameterInfo('\0', "sql-output", "Sets the filename of the SQLite database (default \"zz.db\")", ParameterType.Text, "zz.db"),
        new ParameterInfo('\0', "sql-human", "creation of human readable views/tables", ParameterType.Boolean, true),
        new ParameterInfo('\0', "sql-tables", "creation of tables instead of views for hr data", ParameterType.Boolean, false),
        new ParameterInfo('\0', "db-decompile", "decompilation of database scripts (requires a table for SQLite)", ParameterType.Boolean, true)
    };

    public static void Main(string[] args)
    {
        CommandLine cl = new();
        ParameterParser paramParser = null;
        try
        {
            paramParser = new ParameterParser(cl, paramTypes);
        }
        catch (ParameterException exp)
        {
            Console.Error.WriteLine(exp.Message);
            Environment.ExitCode = -1;
            return;
        }
        OutputHelper.initialize(paramParser);

        if (paramParser["help"] != null)
        {
            Console.Out.WriteLine("usage: zzio_cli.exe <options>");
            Console.Out.WriteLine();
            Console.Out.WriteLine("Options:");
            foreach (ParameterInfo info in paramTypes)
            {
                Console.Out.Write("  ");
                if (info.shortName != (char)0)
                    Console.Out.Write("-" + info.shortName + ", ");
                Console.Out.Write("--" + info.longName + " ");
                if (info.values.Length == 1 && info.values[0] == ParameterType.Boolean)
                {
                    Console.Out.WriteLine("- Enables " + info.description);
                    Console.Out.Write("  ");
                    if (info.shortName != (char)0)
                        Console.Out.Write("-d" + info.shortName + ", ");
                    Console.Out.WriteLine("--disable-" + info.longName + " - Disables " + info.description);
                }
                else
                    Console.Out.WriteLine(info.description);
            }
            Console.Out.WriteLine();
            Console.Out.WriteLine("Formats:");
            Array formats = Enum.GetValues(typeof(FileType));
            foreach (FileType type in formats)
            {
                if (type != FileType.Unknown)
                    Console.Out.WriteLine("  " + type.ToString());
            }
            return;
        }

        FileSelection fs = new();
        fs.addFromParameters(paramParser);
        ConversionMgr convMgr = null;
        try
        {
            convMgr = new ConversionMgr(paramParser);
        }
        catch (ParameterException e)
        {
            Console.Error.WriteLine(e.Message);
            Environment.Exit(-1);
            return;
        }
        FileType[] types = ConversionMgr.scanFiles(fs.Files);

        bool doMapDB = (bool)paramParser["map-db"];
        bool ignoreDB = false;
        if (doMapDB)
        {
            int indexI = -1, firstDataI = -1;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == FileType.FBS_Index)
                {
                    if (indexI < 0)
                        indexI = 0;
                    else
                    {
                        Console.Error.WriteLine("Warning: Multiple database index files, no database output");
                        ignoreDB = true;
                        break;
                    }
                }
                else if (types[i] == FileType.FBS_Data && firstDataI < 0)
                    firstDataI = i;
            }
            if (!ignoreDB && (indexI < 0 || firstDataI < 0) && !(indexI < 0 && firstDataI < 0))
            {
                Console.Error.WriteLine("Warning: Database mapping requires both an index and a module file included, no database output");
                ignoreDB = true;
            }
        }

        string outDir = Path.GetFullPath(paramParser["output"] as string);
        for (int i = 0; i < fs.Files.Count; i++)
        {
            string f = fs.Files[i];
            if (types[i] == FileType.Unknown)
            {
                Console.Error.WriteLine("Warning: The format of " + Path.GetFileName(f) + " could not be determined, it will be ignored");
                continue;
            }
            else if (ignoreDB && (types[i] == FileType.FBS_Data || types[i] == FileType.FBS_Index))
                continue;
            FileType targetType = convMgr.getTargetFileType(types[i]);
            if (targetType == FileType.Unknown)
            {
                Console.Error.WriteLine("Warning: No " + types[i] + " converter enabled for " + Path.GetFileName(f) + ", it will be ignored");
                continue;
            }

            var outFn = Path.Combine(outDir, Path.GetFileName(f)) + ConversionMgr.getFileTypeExt(targetType);
            FileStream fromStream = null, toStream = null;
            try
            {
                //open files
                fromStream = new FileStream(f, FileMode.Open, FileAccess.Read);
                try
                {
                    toStream = new FileStream(outFn, FileMode.OpenOrCreate, FileAccess.Write);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("Warning: Could not open output file: " + outFn);
                }

                //convert
                try
                {
                    convMgr.convertScannedFile(Path.GetFileName(f), fromStream, toStream, types[i]);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Warning: Could not convert " + Path.GetFileName(f) + ": " + e.Message);
                }

                //close files
                if (toStream != null)
                    toStream.Close();
                fromStream.Close();
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Warning: Could not open input file: " + f);
            }
        }
    }
}
