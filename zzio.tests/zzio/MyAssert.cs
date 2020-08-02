using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace zzio.tests
{
    internal static class MyAssert
    {
        public static void ContainsExactly<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = EqualityComparer<T>.Default;

            if (expected.Count() != actual.Count())
                throw new AssertionException($"Sets differ in length, expected {expected.Count()} but got {actual.Count()}");
            var missing = expected.Where(e => !actual.Contains(e, comparer)).ToArray();
            if (missing.Length > 0)
                throw new AssertionException($"Expected objects are not in actual set: \"{string.Join("\", \"", missing)}\"");
        }

        public static void Equals(string expected, Stream? stream)
        {
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] actualBytes = new byte[expectedBytes.Length];
            Assert.NotNull(stream);
            Assert.AreEqual(actualBytes.Length, stream.Read(actualBytes, 0, actualBytes.Length));
            Assert.AreEqual(expectedBytes, actualBytes);
            Assert.AreEqual(-1, stream.ReadByte());
            stream.Close();
        }
    }
}
