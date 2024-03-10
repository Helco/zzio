using System;
using System.Collections.Generic;
using System.Reflection;

namespace zzre;

partial class Configuration
{
    private static readonly Dictionary<string, ConfigurationAttribute> keyMetadata = [];

    public static void RegisterMetadataField(string key, Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var attribute = field?.GetCustomAttribute<ConfigurationAttribute>();
        if (attribute is null)
            throw new ArgumentException($"Metadata field {type.Name}.{fieldName} does not exist or does not have ConfigurationAttribute");
        keyMetadata.TryAdd(key, attribute);
    }

    public static ConfigurationAttribute? TryGetMetadata(string? key) => key is null ? null :
        keyMetadata.GetValueOrDefault(key);

    public string? GetControllingSourceName(string key) =>
        keyMapping.GetValueOrDefault(key)?.Name;
}
