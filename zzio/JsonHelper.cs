using System;
using Newtonsoft.Json;
using System.Linq;
using zzio.rwbs;
using zzio.primitives;

namespace zzio {
    public class JsonDummyConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return true;
        }
        public override object ReadJson(JsonReader r, Type t, object o, JsonSerializer s) {
            throw new NotImplementedException();
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            serializer.Serialize(writer, "<some_data>");
        }
    }

    public class JsonHexByteArrayConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(byte[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.String) {
                var hex = serializer.Deserialize<string>(reader);
                if (!string.IsNullOrEmpty(hex)) {
                    return Enumerable.Range(0, hex.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                         .ToArray();
                }
            }
            return Enumerable.Empty<byte>().ToArray();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var bytes = value as byte[];
            var @string = BitConverter.ToString(bytes).Replace("-", string.Empty);
            serializer.Serialize(writer, @string);
        }
    }

    public class JsonHexNumberConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(UInt32);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            serializer.Serialize(writer, ((UInt32)value).ToString("X"));
        }
    }

    public class JsonHexNumberArrayConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(UInt32[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            UInt32[] arr = (UInt32[])value;
            writer.WriteStartArray();
            foreach (UInt32 i in arr)
                writer.WriteValue(i.ToString("X"));
            writer.WriteEndArray();
        }
    }

    public class JsonPrimitivesConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return
                objectType == typeof(Vector) ||
                objectType == typeof(TexCoord) ||
                objectType == typeof(Triangle) ||
                objectType == typeof(Normal) ||
                objectType == typeof(Frame) ||
                objectType == typeof(RWAnimationPLG.Data);
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            throw new NotImplementedException();
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (typeof(Vector) == value.GetType()) {
                Vector v = (Vector)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue(v.x);
                writer.WritePropertyName("y");
                writer.WriteValue(v.y);
                writer.WritePropertyName("z");
                writer.WriteValue(v.z);
                writer.WriteEndObject();
            }
            else if (typeof(TexCoord) == value.GetType()) {
                TexCoord t = (TexCoord)value;
                writer.WriteStartObject();
                writer.WritePropertyName("u");
                writer.WriteValue(t.u);
                writer.WritePropertyName("v");
                writer.WriteValue(t.v);
                writer.WriteEndObject();
            }
            else if (typeof(Triangle) == value.GetType()) {
                Triangle t = (Triangle)value;
                writer.WriteStartObject();
                writer.WritePropertyName("m");
                writer.WriteValue(t.m);
                writer.WritePropertyName("v1");
                writer.WriteValue(t.v1);
                writer.WritePropertyName("v2");
                writer.WriteValue(t.v2);
                writer.WritePropertyName("v3");
                writer.WriteValue(t.v3);
                writer.WriteEndObject();
            }
            else if (typeof(Normal) == value.GetType()) {
                Normal n = (Normal)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x");
                writer.WriteValue((short)n.x);
                writer.WritePropertyName("y");
                writer.WriteValue((short)n.y);
                writer.WritePropertyName("z");
                writer.WriteValue((short)n.z);
                writer.WritePropertyName("p");
                writer.WriteValue((short)n.p);
                writer.WriteEndObject();
            }
            else if (typeof(Frame) == value.GetType()) {
                Frame f = (Frame)value;
                writer.WriteStartObject();
                writer.WritePropertyName("rot");
                writer.WriteStartArray();
                for (int i = 0; i < 9; i++)
                    writer.WriteValue(f.rotMatrix[i]);
                writer.WriteEndArray();
                writer.WritePropertyName("pos");
                serializer.Serialize(writer, f.position);
                writer.WritePropertyName("frameIndex");
                writer.WriteValue(f.frameIndex);
                writer.WritePropertyName("creationFlags");
                writer.WriteValue(f.creationFlags);
                writer.WriteEndObject();
            }
            else if (typeof(RWAnimationPLG.Data) == value.GetType()) {
                RWAnimationPLG.Data d = (RWAnimationPLG.Data)value;
                writer.WriteStartObject();
                writer.WritePropertyName("name");
                writer.WriteValue(d.name);
                writer.WritePropertyName("type");
                serializer.Serialize(writer, d.type);
                if (d.items1_3F != null) {
                    writer.WritePropertyName("items1_3F");
                    serializer.Serialize(writer, d.items1_3F);
                }
                else if (d.items1_4F != null) {
                    writer.WritePropertyName("items1_4F");
                    serializer.Serialize(writer, d.items1_4F);
                }
                if (d.items2 != null) {
                    writer.WritePropertyName("items2");
                    serializer.Serialize(writer, d.items2);
                }
                writer.WriteEndObject();
            }
        }
    }
}