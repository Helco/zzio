using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace zzio.cli.converters
{
    static class Utils
    {
        public static string convertToJSON(object obj)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Formatting = Formatting.Indented,
                Converters = new JsonConverter[] {
                            new Newtonsoft.Json.Converters.StringEnumConverter()
                        }
            });
        }
    }

    public class AEDtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.AED; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = ActorExDescription.ReadNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class CFG_VarstoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.CFG_Vars; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var obj = VarConfig.read(buffer);
            buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class CFG_MaptoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.CFG_Map; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var obj = MapMarker.read(buffer);
            buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class EDtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.ED; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            effect.EffectCombiner obj = new effect.EffectCombiner();
            obj.Read(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class FBS_IndextoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.FBS_Index; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var obj = ZZDatabaseIndex.read(buffer);
            buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class FBS_DatatoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.FBS_Data; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var obj = ZZDatabase.read(buffer);
            buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class RWBS_DFFtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.RWBS_DFF; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = zzio.rwbs.Section.readNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class RWBS_BSPtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.RWBS_BSP; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = zzio.rwbs.Section.readNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }


    public class SCNtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.SCN; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = new scn.Scene();
            obj.read(from, false);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class SKAtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.SKA; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            byte[] buffer = new byte[from.Length];
            from.Read(buffer, 0, (int)from.Length);
            var obj = SkeletalAnimation.read(buffer);
            buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }
}