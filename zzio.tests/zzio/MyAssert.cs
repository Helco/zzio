using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace zzio.tests;

internal static class MyAssert
{
    public static void ContainsExactly<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T>? comparer = null)
    {
        if (comparer == null)
            comparer = EqualityComparer<T>.Default;

        Assert.That(actual, Is.EquivalentTo(expected).Using(comparer));
    }

    public static void Equals(string expected, Stream? stream)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = new byte[expectedBytes.Length];
        Assert.That(stream, Is.Not.Null);
        Assert.That(stream!.Read(actualBytes, 0, actualBytes.Length), Is.EqualTo(actualBytes.Length));
        Assert.That(actualBytes, Is.EqualTo(expectedBytes));
        Assert.That(stream.ReadByte(), Is.EqualTo(-1));
        stream.Close();
    }
}
