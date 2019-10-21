using System;
using System.Text.RegularExpressions;
using System.Linq;
using zzio.utils;

namespace zzio.script
{
    public class InvalidInstructionException : Exception {}

    public class RawInstruction
    {
        public string Command { get; }
        public string[] Arguments { get; }

        private string regexArgumentSource =>
            @"\.(\w+)|" + // simple case

            // string constant
            @"\.\""" +
                // between the quotes
                "((" +
                    @"[^\\\""\n]|" +   // any old character except
                    @"\\[\w\\\""]" + // escape sequences
                ")*)" +
            @"\""";
        private Regex regexArgument => new Regex(regexArgumentSource);
        private Regex regexInstruction => new Regex(
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
                Arguments = new string[0];
                return;
            }
            var argumentMatches = regexArgument.Matches(fullMatch.Groups[2].Value);
            if (!argumentMatches.Any() || argumentMatches.Any(m => !m.Success))
                throw new Exception("Assertion failed during syntax error");
            Arguments = argumentMatches
                .Select(m => m.Groups[1].Success
                    ? m.Groups[1].Value
                    : StringUtils.Unescape(m.Groups[2].Value))
                .ToArray();
        }

        public static bool TryParse(string line, out RawInstruction parsed)
        {
            try
            {
                parsed = new RawInstruction(line);
                return true;
            }
            catch(Exception)
            {
                parsed = null;
                return false;
            }
        }
    }
}
