using System;
using System.Collections.Generic;

namespace zzio.cli
{
    public enum ParameterType
    {
        Boolean,
        Number,
        Text
    }

    public struct ParameterInfo
    {
        public char shortName;
        public string longName;
        public string description;
        public ParameterType[] values;
        public object defaultValue;

        public ParameterInfo(char sh, string l, string desc, object defVal)
        {
            shortName = sh;
            longName = l;
            description = desc;
            values = new ParameterType[0];
            defaultValue = defVal;
        }

        public ParameterInfo(char sh, string l, string desc, ParameterType val, object defVal)
        {
            shortName = sh;
            longName = l;
            description = desc;
            values = new ParameterType[] { val };
            defaultValue = defVal;
        }

        public ParameterInfo(char sh, string l, string desc, ParameterType[] val, object defVal)
        {
            shortName = sh;
            longName = l;
            description = desc;
            values = val;
            defaultValue = defVal;
        }
    }

    public class ParameterException : Exception
    {
        public ParameterException(string m) : base(m) { }
    }

    public class ParameterParser
    {
        private readonly ParameterInfo[] classes;
        private readonly Dictionary<string, object> values;

        public ParameterParser(CommandLine args, ParameterInfo[] classes)
        {
            this.classes = classes;
            values = new Dictionary<string, object>();
            foreach (ParameterInfo info in classes)
                values[info.longName] = info.defaultValue;

            for (int i = 1; i < args.Length; i++) //skip over executable path
            {
                string argFull = args.Text[i].ToLowerInvariant();
                if (args[i] != CommandLineToken.LongArg && args[i] != CommandLineToken.ShortArg)
                    throw new ParameterException("Expected option for parameter " + i);

                //count parameter values
                int paramCount = 0;
                for (int j = i + 1; j < args.Length; j++)
                {
                    if (args[j] == CommandLineToken.LongArg || args[j] == CommandLineToken.ShortArg)
                        break;
                    paramCount++;
                }

                //find out what parameter it is
                ParameterInfo paramInfo = new('\0', null, null, null);
                bool success = false;
                foreach (ParameterInfo info in classes)
                {
                    if (args[i] == CommandLineToken.ShortArg)
                    {
                        if ("-" + info.shortName == argFull)
                        {
                            paramInfo = info;
                            success = true;
                            break;
                        }
                        else if ("-d" + info.shortName == argFull)
                        {
                            if (info.values.Length == 1 && info.values[0] == ParameterType.Boolean)
                            {
                                paramInfo = info;
                                success = true;
                                break;
                            }
                            else
                                throw new ParameterException("Parameter \"" + info.longName + "\" expected a value");
                        }
                    }
                    else
                    {
                        if ("--" + info.longName == argFull)
                        {
                            paramInfo = info;
                            success = true;
                            break;
                        }
                        else if ("--disable-" + info.longName == argFull)
                        {
                            if (info.values.Length == 1 && info.values[0] == ParameterType.Boolean)
                            {
                                paramInfo = info;
                                success = true;
                                break;
                            }
                            else
                                throw new ParameterException("Parameter \"" + info.longName + "\" expected a value");
                        }
                    }
                }
                if (!success)
                    throw new ParameterException("Unknown parameter \"" + argFull + "\"");

                //evaluate parameter values
                if (paramInfo.values.Length == 1 && paramInfo.values[0] == ParameterType.Boolean) //boolean is a special case
                {
                    if (argFull.StartsWith("-d") || argFull.StartsWith("--disable-"))
                        values[paramInfo.longName] = false;
                    else
                        values[paramInfo.longName] = true;
                }
                else if (paramInfo.values.Length < paramCount)
                    throw new ParameterException("Unexpected values after parameter \"" + paramInfo.longName + "\"");
                else if (paramInfo.values.Length != paramCount)
                    throw new ParameterException("Too few values after parameter \"" + paramInfo.longName + "\"");
                else if (paramCount == 0)
                    values[paramInfo.longName] = new object(); //not null anymore
                else
                {
                    object[] valueArr = new object[paramCount];
                    for (int a = 0; a < paramCount; a++)
                    {
                        if (paramInfo.values[a] == ParameterType.Text)
                        {
                            if (args[i + 1 + a] == CommandLineToken.String)
                                valueArr[a] = args.Text[i + 1 + a];
                            else
                                throw new ParameterException("Parameter \"" + paramInfo.longName + "\" expected a string for value " + (a + 1));
                        }
                        else if (paramInfo.values[a] == ParameterType.Number)
                        {
                            if (args[i + 1 + a] == CommandLineToken.Number)
                                valueArr[a] = args.Number[i + 1 + a];
                            else
                                throw new ParameterException("Parameter \"" + paramInfo.longName + "\" expected a number for value " + (a + 1));
                        }
                        else
                            throw new Exception();
                    }
                    if (values[paramInfo.longName] is not List<object> valueList)
                        values[paramInfo.longName] = valueList = new List<object>();
                    valueList.Add(valueArr.Length == 1 ? valueArr[0] : valueArr);
                    i += paramCount;
                }
            }
        }

        public object this[string name]
        {
            get
            {
                if (values.ContainsKey(name))
                {
                    object val = values[name];
                    if (val is List<object>)
                    {
                        List<object> l = val as List<object>;
                        return l.Count == 0 ? null : l[l.Count - 1];
                    }
                    else
                        return val;
                }
                else
                    return null;
            }
        }

        public object this[string name, bool asList]
        {
            get
            {
                if (!asList)
                    return this[name];
                else if (values.ContainsKey(name))
                    return values[name];
                else
                    return values[name];
            }
        }
    }
}
