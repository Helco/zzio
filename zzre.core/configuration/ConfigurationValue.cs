using System;

namespace zzre;

public readonly struct ConfigurationValue :
    IEquatable<ConfigurationValue>,
    IEquatable<double>,
    IEquatable<int>, // just for convenience
    IEquatable<string>
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

    public override string ToString() =>
        @string is null ? numeric.ToString() : $"\"{@string}\"";

    public bool Equals(ConfigurationValue other) => numeric == other.numeric && @string == other.@string;
    public override bool Equals(object? obj) => obj is ConfigurationValue value && Equals(value);
    public bool Equals(double other) => IsNumeric && numeric == other;
    public bool Equals(int other) => IsNumeric && numeric == other;
    public bool Equals(string? other) => IsString && @string == other;
    public override int GetHashCode() => HashCode.Combine(numeric, @string);

    public static bool operator ==(ConfigurationValue left, ConfigurationValue right) => left.Equals(right);
    public static bool operator !=(ConfigurationValue left, ConfigurationValue right) => !(left == right);
    public static bool operator ==(ConfigurationValue left, double n) => left.Equals(n);
    public static bool operator !=(ConfigurationValue left, double n) => !left.Equals(n);
    public static bool operator ==(ConfigurationValue left, int n) => left.Equals(n);
    public static bool operator !=(ConfigurationValue left, int n) => !left.Equals(n);
    public static bool operator ==(ConfigurationValue left, string s) => left.Equals(s);
    public static bool operator !=(ConfigurationValue left, string s) => !left.Equals(s);
#pragma warning restore CA1720
}
