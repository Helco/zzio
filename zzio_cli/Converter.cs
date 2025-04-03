using System;
using System.IO;
using System.Collections.Generic;

namespace zzio.cli;

public enum FileType
{
    RWBS_DFF,
    RWBS_BSP,
    SKA,
    AED,
    SCN,
    ED,
    CFG_Vars,
    CFG_Map,
    CFG_Game, //not supported, RV required
    FBS_Index,
    FBS_Data, //we have the index already I hope?
    JSON,
    CSV,

    Unknown
}

public interface IFileScanner
{
    FileType ScannerType { get; }
    bool mightBe(Stream stream);
}

public interface IConverter
{
    FileType TypeFrom { get; }
    FileType TypeTo { get; }
    void convert(string name, ParameterParser args, Stream from, Stream to); //throw an exception on error
}

public partial class ConversionMgr
{
    private static FileType getExtensionFileType(string ext)
    {
        switch (ext[1..].ToLowerInvariant())
        {
            case ("ska"): { return FileType.SKA; }
            case ("aed"): { return FileType.AED; }
            case ("ed"): { return FileType.ED; }
            case ("json"): { return FileType.JSON; } //actually a special-case :(
            case ("csv"): { return FileType.CSV; }
            case ("bsp"): { return FileType.RWBS_BSP; }
            case ("dff"): { return FileType.RWBS_DFF; }
            case ("scn"): { return FileType.SCN; }
            // no cfg or fbs as there are multiple formats 
            default: { return FileType.Unknown; }
        }
    }

    public static string getFileTypeExt(FileType type)
    {
        switch (type)
        {
            case (FileType.AED): { return ".aed"; }
            case (FileType.CFG_Game):
            case (FileType.CFG_Map):
            case (FileType.CFG_Vars): { return ".cfg"; }
            case (FileType.ED): { return ".ed"; }
            case (FileType.FBS_Data):
            case (FileType.FBS_Index): { return ".fbs"; }
            case (FileType.JSON): { return ".json"; }
            case (FileType.CSV): { return ".csv"; }
            case (FileType.RWBS_BSP): { return ".bsp"; }
            case (FileType.RWBS_DFF): { return ".dff"; }
            case (FileType.SCN): { return ".scn"; }
            case (FileType.SKA): { return ".ska"; }
            default: { return null; }
        }
    }

    private readonly Dictionary<FileType, IConverter> conversions;
    private readonly ParameterParser args;

    public ConversionMgr(ParameterParser args)
    {
        this.args = args;
        conversions = new Dictionary<FileType, IConverter>();
        var formats = Enum.GetValues<FileType>();
        foreach (FileType type in formats)
            conversions[type] = null;

        if (args["target", true] is List<object> convTypes)
        {
            foreach (string target in convTypes)
            {
                FileType targetType = FileType.Unknown;
                if (!Enum.TryParse<FileType>(target, true, out targetType))
                    throw new ParameterException("Unknown target format: \"" + target + "\"");
                foreach (FileType type in formats)
                {
                    IConverter conv = findConverter(type, targetType);
                    if (conv != null)
                        conversions[type] = conv;
                }
            }
        }

        convTypes = args["from-to", true] as List<object>;
        if (convTypes != null)
        {
            foreach (object s in convTypes)
            {
                object[] ss = s as object[];
                FileType fromType = FileType.Unknown, toType = FileType.Unknown;
                if (!Enum.TryParse<FileType>(ss[0] as string, true, out fromType))
                    throw new Exception("Unknown source format: \"" + (ss[0] as string) + "\"");
                if (!Enum.TryParse<FileType>(ss[1] as string, true, out toType))
                    throw new Exception("Unknown target format: \"" + (ss[1] as string) + "\"");
                IConverter conv = findConverter(fromType, toType);
                if (conv == null)
                    throw new Exception("Conversions from " + fromType + " to " + toType + " are not supported.");
                conversions[fromType] = conv;
            }
        }
    }

    public static FileType scanFile(Stream stream, FileType extType)
    {
        long startPosition = stream.Position;
        foreach (IFileScanner scanner in scanners)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            try
            {
                if (scanner.ScannerType != extType && scanner.mightBe(stream))
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    return scanner.ScannerType;
                }
            }
            catch (Exception)
            {
                //let just hope stream does not get destroyed
            }
        }
        stream.Seek(startPosition, SeekOrigin.Begin);
        return extType;
    }

    public static FileType[] scanFiles(IReadOnlyList<string> files)
    {
        FileType[] result = new FileType[files.Count];
        for (int i = 0; i < files.Count; i++)
        {
            try
            {
                FileStream fs = new(files[i], FileMode.Open, FileAccess.Read);
                result[i] = scanFile(fs, FileType.Unknown);
                fs.Close();
            }
            catch (Exception)
            {
                result[i] = FileType.Unknown;
            }
        }
        return result;
    }

    public FileType getTargetFileType(FileType source)
    {
        IConverter conv = conversions[source];
        if (conv == null)
            return FileType.Unknown;
        else
            return conv.TypeTo;
    }

    public void convertFile(string ext, string name, Stream from, Stream to)
    {
        FileType type = scanFile(from, getExtensionFileType(ext));
        convertScannedFile(name, from, to, type);
    }

    public void convertScannedFile(string name, Stream from, Stream to, FileType type)
    {
        if (type == FileType.Unknown)
            throw new Exception("File format of \"" + name + "\" could not be determined");
        IConverter conv = conversions[type];
        if (conv == null)
            throw new Exception("No converter enabled for " + type);
        conv.convert(name, args, from, to);
    }
}
