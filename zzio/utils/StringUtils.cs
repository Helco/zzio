using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace zzio;

public static class StringUtils
{
    private static readonly Dictionary<char, char> escapes
        = new()
        {
            { '\a', 'a' },
            { '\b', 'b' },
            { '\f', 'f' },
            { '\n', 'n' },
            { '\r', 'r' },
            { '\t', 't' },
            { '\v', 'v' },
            { '\\', '\\' },
            { '\'', '\'' },
            { '\"', '\"' },
            { '\0', '0' }
        };
    private static readonly Dictionary<char, char> unescapes
        = escapes.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>Escapes a string using common escape sequences</summary>
    public static string Escape(string unescaped)
    {
        using StringReader reader = new(unescaped);
        StringBuilder writer = new();
        int ch;

        while ((ch = reader.Read()) >= 0)
        {
            if (escapes.TryGetValue((char)ch, out var escapedChar))
            {
                writer.Append('\\');
                writer.Append(escapedChar);
            }
            else if (ch >= 0x20 && ch < 0x7f)
            {
                writer.Append((char)ch);
            }
            else
            {
                writer.Append("\\x");
                writer.Append(ch.ToString("X").PadLeft(2, '0'));
            }
        }

        return writer.ToString();
    }

    private static int readHexByte(StringReader reader)
    {
        int firstNibble = reader.Read();
        int secondNibble = reader.Read();

        string hexByte = "" +
            (firstNibble < 0 ? '0' : (char)firstNibble) +
            (secondNibble < 0 ? '0' : (char)secondNibble);

        return firstNibble < 0 && secondNibble < 0
            ? -1
            : Convert.ToInt32(hexByte, 16);
    }

    /// <summary>Unescapes a string using common escape sequences</summary>
    public static string Unescape(string escaped)
    {
        using StringReader reader = new(escaped);
        StringBuilder writer = new();
        int ch;

        while ((ch = reader.Read()) >= 0)
        {
            if (ch != '\\')
            {
                writer.Append((char)ch);
                continue;
            }

            ch = reader.Read(); // sequence specifier
            if (unescapes.TryGetValue((char)ch, out var unescapedChar))
            {
                writer.Append(unescapedChar);
            }
            else if (ch == 'x')
            {
                int hexByte = readHexByte(reader);
                writer.Append(hexByte < 0 ? '\\' : (char)hexByte);
            }
            else
            {
                writer.Append('\\');
                writer.Append(ch);
            }
        }

        return writer.ToString();
    }
}
