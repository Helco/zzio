using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace zzre;

partial class Configuration
{
    private static readonly InMemoryConfigurationSource defaultSource = new("Default");
    private static readonly Dictionary<string, ConfigurationAttribute> keyMetadata = [];

    /// <summary>
    /// Registers metadata for a given configuration value which will be used for tooling and validation
    /// </summary>
    /// <remarks>This method should only be called by the source generator</remarks>
    /// <param name="key">The key of the configuration value</param>
    /// <param name="type">The type of the field used in the consuming class. (should be convertable to <see cref="string"/> or <see cref="double"/>)</param>
    /// <param name="fieldName">The name of the field used in the consuming class</param>
    /// <param name="defaultValue">A default value for the configuration</param>
    /// <exception cref="ArgumentException"></exception>
    [ExcludeFromCodeCoverage]
    public static void RegisterMetadataField(string key, Type type, string fieldName, ConfigurationValue? defaultValue = null)
    {
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var attribute = field?.GetCustomAttribute<ConfigurationAttribute>() ??
            throw new ArgumentException($"Metadata field {type.Name}.{fieldName} does not exist or does not have ConfigurationAttribute");
        keyMetadata.TryAdd(key, attribute);
        if (defaultValue.HasValue)
            defaultSource[key] = defaultValue.Value;
    }

    /// <summary>
    /// Attempts to retrieve registered metadata of a configuration value for tooling.
    /// </summary>
    /// <param name="key">The key of the configuration value</param>
    /// <returns>The metadata as <see cref="ConfigurationAttribute"/> or <c>null</c> if no metadata was registered</returns>
    [ExcludeFromCodeCoverage]
    public static ConfigurationAttribute? TryGetMetadata(string? key) => key is null ? null :
        keyMetadata.GetValueOrDefault(key);

    /// <summary>
    /// Attempts to retrieve the name of the <see cref="IConfigurationSource"/> that provides the currently-used configuration value
    /// </summary>
    /// <param name="key">The key of the configuration value</param>
    /// <returns>The name of the source or <c>null</c> if the configuration value is not provided by any source</returns>
    public string? GetControllingSourceName(string key) =>
        keyMapping.GetValueOrDefault(key)?.Name;
}
