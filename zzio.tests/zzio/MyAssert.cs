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
        Assert.NotNull(stream);
        Assert.AreEqual(actualBytes.Length, stream!.Read(actualBytes, 0, actualBytes.Length));
        Assert.AreEqual(expectedBytes, actualBytes);
        Assert.AreEqual(-1, stream.ReadByte());
        stream.Close();
    }
}
