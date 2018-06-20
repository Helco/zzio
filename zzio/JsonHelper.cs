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
}