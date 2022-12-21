using zzio.cli.converters;

namespace zzio.cli;

public partial class ConversionMgr
{
    private static readonly IConverter[] converters =
    {
        new AEDtoJSON(),
        new CFG_MaptoJSON(),
        new CFG_VarstoJSON(),
        new EDtoJSON(),
        new FBS_IndextoJSON(),
        new FBS_DatatoJSON(),
        new RWBS_BSPtoJSON(),
        new RWBS_DFFtoJSON(),
        new SCNtoJSON(),
        new SKAtoJSON(),
        new FBStoCSV()
    };

    private static IConverter findConverter(FileType from, FileType to)
    {
        foreach (IConverter conv in converters)
        {
            if (conv.TypeFrom == from && conv.TypeTo == to)
                return conv;
        }
        return null;
    }
}