using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace zzio.cli.converters
{
    internal static class Utils
    {
        public static string convertToJSON(object obj, IContractResolver resolver = null)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Formatting = Formatting.Indented,
                Converters = new JsonConverter[] {
                            new Newtonsoft.Json.Converters.StringEnumConverter()
                        },
                ContractResolver = resolver
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
            var obj = VarConfig.ReadNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class CFG_MaptoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.CFG_Map; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = MapMarker.ReadFile(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
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
            var obj = new zzio.db.IndexTable();
            obj.Read(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class FBS_DatatoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.FBS_Data; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = new zzio.db.Table();
            obj.Read(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class RWBSContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            if (property.DeclaringType == typeof(rwbs.Section) && property.PropertyName == "parent")
                property.ShouldSerialize = (o) => false;
            return property;
        }
    }

    public class RWBS_DFFtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.RWBS_DFF; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = zzio.rwbs.Section.ReadNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj, new RWBSContractResolver()));
            to.Write(buffer, 0, buffer.Length);
        }
    }

    public class RWBS_BSPtoJSON : IConverter
    {
        public FileType TypeFrom { get { return FileType.RWBS_BSP; } }
        public FileType TypeTo { get { return FileType.JSON; } }
        public void convert(string name, ParameterParser args, Stream from, Stream to)
        {
            var obj = zzio.rwbs.Section.ReadNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj, new RWBSContractResolver()));
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
            obj.Read(from);
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
            var obj = SkeletalAnimation.ReadNew(from);
            byte[] buffer = Encoding.Default.GetBytes(Utils.convertToJSON(obj));
            to.Write(buffer, 0, buffer.Length);
        }
    }
}