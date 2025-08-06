using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Serilog;

namespace zzre;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
    InternalError
}

public class DiagnosticCategory
{
    private readonly List<DiagnosticType> types = [];

    public string Name { get; }
    public IReadOnlyList<DiagnosticType> Types => types;

    public DiagnosticCategory(string name) => Name = name;

    public DiagnosticType Error(string message, string? footNote = null, params string[] sourceInfoMessages) =>
        Create(DiagnosticSeverity.Error, message, footNote, sourceInfoMessages);
    public DiagnosticType Warning(string message, string? footNote = null, params string[] sourceInfoMessages) =>
        Create(DiagnosticSeverity.Warning, message, footNote, sourceInfoMessages);
    public DiagnosticType Information(string message, string? footNote = null, params string[] sourceInfoMessages) =>
        Create(DiagnosticSeverity.Info, message, footNote, sourceInfoMessages);
    public DiagnosticType Create(DiagnosticSeverity severity, string message, string? footNote = null, params string[] sourceInfoMessages)
    {
        var type = new DiagnosticType(this, types.Count + 1, severity, message, sourceInfoMessages, footNote);
        types.Add(type);
        return type;
    }

    internal DiagnosticType CreateWithFootNote(DiagnosticSeverity severity, string message, string footNote, params string[] sourceInfoMessages)
    {
        var type = new DiagnosticType(this, types.Count + 1, severity, message, sourceInfoMessages, footNote);
        types.Add(type);
        return type;
    }
}

public class DiagnosticType
{
    public DiagnosticCategory Category { get; }
    public int CodeNumber { get; }
    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public IReadOnlyList<string?> SourceInfoMessages { get; }
    public string? FootNote { get; }
    public string Code => $"{Category.Name}{CodeNumber:D3}";

    public DiagnosticType(
        DiagnosticCategory category,
        int codeNumber,
        DiagnosticSeverity severity,
        string message,
        IReadOnlyList<string?> sourceInfoMessages,
        string? footNote)
    {
        Category = category;
        CodeNumber = codeNumber;
        Severity = severity;
        Message = message;
        SourceInfoMessages = sourceInfoMessages;
        FootNote = footNote;
    }

    public Diagnostic Create(string[]? messageParams = null, DiagnosticLocation[]? sourceInfos = null) =>
        new(this, messageParams ?? [], sourceInfos ?? []);
}

public readonly record struct DiagnosticLocation(
    string Resource,
    int? LineStart = null,
    int? LineEnd = null,
    int? ColumnStart = null,
    int? ColumnEnd = null) : IComparable<DiagnosticLocation>
{
    public int CompareTo(DiagnosticLocation other)
    {
        var result = Resource.CompareTo(other.Resource);
        if (result != 0)
            return result;

        var myLineStart = LineStart ?? -1;
        var otherLineStart = other.LineStart ?? -1;
        if (myLineStart != otherLineStart)
            return myLineStart - otherLineStart;

        var myColumn = ColumnStart ?? -1;
        var otherColumn = other.ColumnStart ?? -1;
        if (myColumn != otherColumn)
            return myColumn - otherColumn;
        return 0;
    }

    public readonly override string ToString()
    {
        var builder = new StringBuilder();
        WriteTo(builder);
        return builder.ToString();
    }

    public readonly void WriteTo(StringBuilder builder)
    {
        builder.Append(Resource);
        if (LineStart is null)
            return;
        builder.Append($"({LineStart}");
        if (ColumnStart is not null)
            builder.Append($":{ColumnStart}");
        if (LineEnd is not null)
        {
            builder.Append($"->{LineEnd}");
            if (ColumnEnd is not null)
                builder.Append($":{ColumnEnd}");
        }
        builder.Append(')');
    }
    
    public static bool operator <(DiagnosticLocation left, DiagnosticLocation right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(DiagnosticLocation left, DiagnosticLocation right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(DiagnosticLocation left, DiagnosticLocation right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(DiagnosticLocation left, DiagnosticLocation right)
    {
        return left.CompareTo(right) >= 0;
    }
}

public readonly record struct Diagnostic(
    DiagnosticType Type,
    IReadOnlyList<string> MessageParams,
    IReadOnlyList<DiagnosticLocation> SourceInfos)
    : IComparable<Diagnostic>
{
    public DiagnosticCategory Category => Type.Category;
    public DiagnosticSeverity Severity => Type.Severity;
    public string FormattedMessage => string.Format(Type.Message, MessageParams.ToArray());
    public string? FormattedFootNote => Type.FootNote == null ? null : string.Format(Type.FootNote, MessageParams.ToArray());

    public void Write(ILogger logger)
    {
        var sourceInfo = SourceInfos.Any() ? SourceInfos.First().ToString() + ": " : "";
        logger.Write(SeverityToSerilog(Type.Severity), sourceInfo + Type.Message, MessageParams);
        if (Type.FootNote is not null)
            logger.Information(sourceInfo + Type.Message, MessageParams);
    }

    public void WriteToConsole()
    {
        var prevBackground = Console.BackgroundColor;
        Console.BackgroundColor = SeverityToConsoleColor(Type.Severity);
        Console.Write(Type.Severity switch
        {
            DiagnosticSeverity.Info => "INFO  ",
            DiagnosticSeverity.Warning => "WARN  ",
            DiagnosticSeverity.Error => "ERROR ",
            DiagnosticSeverity.InternalError => "INTERR",
            _ => "????? "
        });
        Console.BackgroundColor = prevBackground;

        if (SourceInfos.Any())
        {
            Console.Write(SourceInfos.First());
            Console.Write(": ");
        }
        Console.WriteLine(FormattedMessage);

        if (Type.FootNote is not null)
        {
            Console.Write("NOTE  ");
            Console.WriteLine(FormattedFootNote);
        }
    }

    private static Serilog.Events.LogEventLevel SeverityToSerilog(DiagnosticSeverity s) => s switch
    {
        DiagnosticSeverity.Info => Serilog.Events.LogEventLevel.Information,
        DiagnosticSeverity.Warning => Serilog.Events.LogEventLevel.Warning,
        DiagnosticSeverity.Error => Serilog.Events.LogEventLevel.Error,
        DiagnosticSeverity.InternalError => Serilog.Events.LogEventLevel.Fatal,
        _ => throw new NotImplementedException($"Unimplemented severity: {s}")
    };

    private static ConsoleColor SeverityToConsoleColor(DiagnosticSeverity s) => s switch
    {
        DiagnosticSeverity.Info => ConsoleColor.DarkGray,
        DiagnosticSeverity.Warning => ConsoleColor.DarkYellow,
        DiagnosticSeverity.Error => ConsoleColor.DarkRed,
        DiagnosticSeverity.InternalError => ConsoleColor.DarkMagenta,
        _ => throw new NotImplementedException($"Unimplemented severity: {s}")
    };

    public int CompareTo(Diagnostic other)
    {
        var aLoc = SourceInfos.Any() ? SourceInfos.First() : null as DiagnosticLocation?;
        var bLoc = other.SourceInfos.Any() ? other.SourceInfos.First() : null as DiagnosticLocation?;
        if (aLoc is null && bLoc is not null)
            return 1;
        if (aLoc is not null && bLoc is null)
            return -1;
        if (aLoc is not null && bLoc is not null)
        {
            foreach (var (a, b) in SourceInfos.Zip(other.SourceInfos))
            {
                var locResult = a.CompareTo(b);
                if (locResult != 0)
                    return locResult;
            }
        }
        var typeResult = Type.Code.CompareTo(other.Type.Code);
        if (typeResult != 0)
            return typeResult;
        return -1;
    }

    public static bool operator <(Diagnostic left, Diagnostic right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Diagnostic left, Diagnostic right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Diagnostic left, Diagnostic right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Diagnostic left, Diagnostic right)
    {
        return left.CompareTo(right) >= 0;
    }
}