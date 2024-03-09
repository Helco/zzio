using System;

namespace zzre;

public readonly struct ConfigurationValue
{
#pragma warning disable CA1720 // Identifier contains type name
    private readonly double numeric;
    private readonly string? @string;

    public bool IsNumeric => @string is null;
    public bool IsString => @string is not null;

    public double Numeric
    {
        get => IsNumeric ? numeric
            : throw new InvalidOperationException("ConfigurationValue is not numeric");
    }

    public string String
    {
        get => @string ??
            throw new InvalidOperationException("ConfigurationValue is not a string");
    }

    public ConfigurationValue(double numeric)
    {
        this.numeric = numeric;
        @string = null;
    }

    public ConfigurationValue(string @string)
    {
        this.@string = @string;
        numeric = double.NaN;
    }
#pragma warning restore CA1720
}
