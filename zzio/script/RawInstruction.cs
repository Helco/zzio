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

    private string regexArgumentSource =>
        @"\.(-?\w+)|" + // simple case

        // string constant
        @"\.\""" +
            // between the quotes
            "((" +
                @"[^\\\""\n]|" +   // any old character except
                @"\\[\w\\\""]" + // escape sequences
            ")*)" +
        @"\""";
    private Regex regexArgument => new(regexArgumentSource);
    private Regex regexInstruction => new(
        @"^(.\w*)((" +                   // command
            regexArgumentSource + ")*" + // arguments
        ")$"
    );

    public RawInstruction(string instruction)
    {
        var fullMatch = regexInstruction.Match(instruction.Trim());
        if (!fullMatch.Success)
            throw new InvalidInstructionException();
        Command = fullMatch.Groups[1].Value;

        if (fullMatch.Groups[2].Length == 0)
        {
            Arguments = Array.Empty<string>();
            return;
        }
        var argumentMatchesColl = regexArgument.Matches(fullMatch.Groups[2].Value);
        var argumentMatches = new Match[argumentMatchesColl.Count]; // copy manually to be able to use LINQ, remove with .NET Standard 2.1
        argumentMatchesColl.CopyTo(argumentMatches, 0);

        if (!argumentMatches.Any() || argumentMatches.Any(m => !m.Success))
            throw new Exception("Assertion failed during syntax error");
        Arguments = argumentMatches
            .Select(m => m.Groups[1].Success
                ? m.Groups[1].Value
                : StringUtils.Unescape(m.Groups[2].Value))
            .ToArray();
    }

    public RawInstruction(string command, string[] arguments)
    {
        Command = command;
        Arguments = arguments.ToArray();
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
