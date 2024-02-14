using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using zzio;

namespace zzio.script;

public class InvalidInstructionException : Exception { }

public class RawInstruction
{
    public string Command { get; }
    public string[] Arguments { get; }

    private const string RegexArgumentSource =
        @"\.(-?\w+)|" + // simple case

        // string constant
        @"\.\""" +
            // between the quotes
            "((" +
                @"[^\\\""\n]|" +   // any old character except
                @"\\[\w\\\""]" + // escape sequences
            ")*)" +
        @"\""";
    private static Regex RegexArgument => new(RegexArgumentSource);
    private static Regex RegexInstruction => new(
        @"^(.\w*)((" +                   // command
            RegexArgumentSource + ")*" + // arguments
        ")$"
    );

    public RawInstruction(string instruction)
    {
        var fullMatch = RegexInstruction.Match(instruction.Trim());
        if (!fullMatch.Success)
            throw new ArgumentException("Failed to parse instruction", nameof(instruction));
        Command = fullMatch.Groups[1].Value;

        if (fullMatch.Groups[2].Length == 0)
        {
            Arguments = [];
            return;
        }
        var argumentMatchesColl = RegexArgument.Matches(fullMatch.Groups[2].Value);
        var argumentMatches = new Match[argumentMatchesColl.Count]; // copy manually to be able to use LINQ, remove with .NET Standard 2.1
        argumentMatchesColl.CopyTo(argumentMatches, 0);

        if (!argumentMatches.Any() || argumentMatches.Any(m => !m.Success))
            throw new ArgumentException("Failed to parse instruction arguments");
        Arguments = argumentMatches
            .Select(m => m.Groups[1].Success
                ? m.Groups[1].Value
                : StringUtils.Unescape(m.Groups[2].Value))
            .ToArray();
    }

    public RawInstruction(string command, string[] arguments)
    {
        Command = command;
        Arguments = [.. arguments];
    }

    public static bool TryParse(string line, [NotNullWhen(true)] out RawInstruction? parsed)
    {
        try
        {
            parsed = new RawInstruction(line);
            return true;
        }
        catch (Exception)
        {
            parsed = null;
            return false;
        }
    }

    public override string ToString()
    {
        if (Arguments.Length == 0)
            return Command;
        return Command + "." + string.Join(".", Arguments);
    }
}
