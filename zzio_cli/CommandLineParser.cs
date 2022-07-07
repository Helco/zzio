using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace zzio
{
    namespace cli
    {
        public enum CommandLineToken
        {
            ShortArg,
            LongArg,
            Number,
            String
        }

        public class CommandLine
        {
            private readonly string[] argText;
            private readonly double[] argNumbers;
            private readonly CommandLineToken[] argTokens;

            public CommandLine() : this(Environment.CommandLine) { }

            public CommandLine(string line)
            {
                //first extract the explicit strings (as they like to mess things up a bit)
                var args = new List<string>();
                var curArg = "";
                var isString = false;
                foreach (string part in line.Split('"'))
                {
                    if (isString)
                    {

                        if (part.EndsWith("\\"))
                        {
                            curArg += part.Substring(0, part.Length - 1);
                            curArg += '"';
                        }
                        else
                        {
                            args.Add("\"" + curArg + part);
                            curArg = "";
                            isString = false;
                        }
                    }
                    else
                    {
                        var tpart = part.Trim();
                        if (tpart.Length > 0)
                            args.AddRange(tpart.Split(' '));
                        isString = !isString;
                    }
                }
                if (curArg.Length > 0)
                    args.Add(curArg);

                //then extract the actual args and specify their token
                var tokens = new List<CommandLineToken>();
                var numbers = new List<double>();
                for (int i = 0; i < args.Count; i++)
                {
                    var matchesNumber = Regex.Match(args[i], @"^[-+]?\d+(\.\d+)?$").Success;
                    if (args[i].StartsWith("\""))
                    {
                        args[i] = args[i].Substring(1);
                        numbers.Add(double.NaN);
                        tokens.Add(CommandLineToken.String);
                    }
                    else if (args[i].StartsWith("-") && !matchesNumber)
                    {
                        numbers.Add(double.NaN);
                        if (args[i].StartsWith("--"))
                            tokens.Add(args[i] == "--" ? CommandLineToken.String : CommandLineToken.LongArg);
                        else
                            tokens.Add(CommandLineToken.ShortArg);
                    }
                    else if (matchesNumber)
                    {
                        tokens.Add(CommandLineToken.Number);
                        numbers.Add(double.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        tokens.Add(CommandLineToken.String);
                        numbers.Add(double.NaN);
                    }
                }

                argText = args.ToArray();
                argNumbers = numbers.ToArray();
                argTokens = tokens.ToArray();
            }

            public int Length { get { return argTokens.Length; } }

            public CommandLineToken this[int i]
            {
                get
                {
                    if (i < 0 || i >= argTokens.Length)
                        throw new IndexOutOfRangeException();
                    return argTokens[i];
                }
            }

            public IReadOnlyList<string> Text
            {
                get { return argText; }
            }

            public IReadOnlyList<double> Number
            {
                get { return argNumbers; }
            }
        }
    }
}