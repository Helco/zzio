using System;

namespace zzre;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ConfigurationAttribute : Attribute
{
    public string Key { get; init; } = "";
    public string Description { get; init; } = "";
    public double Min { get; init; } = double.NegativeInfinity;
    public double Max { get; init; } = double.PositiveInfinity;
    public bool IsInteger { get; init; }
}
