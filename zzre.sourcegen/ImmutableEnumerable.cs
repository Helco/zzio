// from https://github.com/dotnet/runtime/issues/77183#issuecomment-1287470085
// adapted GetHashCode for compatibility

using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;

public sealed class ImmutableEnumerable<T> : IEnumerable<T>, IEquatable<ImmutableEnumerable<T>>
{
    // This type is owner of the contained collection; no one can change this from outside.
    // Use normal array for performance and memory footprint reasons; could also be an ImmutableArray<T> to prevent changing items by mistake once it has been created.
    private readonly T[] items;

    private ImmutableEnumerable(T[] items)
    {
        this.items = items;
    }

    public static ImmutableEnumerable<T> Create(IEnumerable<T> items)
        => new(items.ToArray());

    // ToDo: Provide implicit type conversion, if required

    public static bool operator ==(ImmutableEnumerable<T> left, ImmutableEnumerable<T> right)
        => Equals(left, right);

    public static bool operator !=(ImmutableEnumerable<T> left, ImmutableEnumerable<T> right)
        => !(left == right);

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = -1817952719;
            foreach (var item in this.items)
            {
                hashCode *= -1521134295;
                hashCode += EqualityComparer<T>.Default.GetHashCode(item);
            }
            return hashCode;
        }
    }

    public override bool Equals(object? obj)
        => this.Equals(obj as ImmutableEnumerable<T>);

    public bool Equals(ImmutableEnumerable<T>? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.items.SequenceEqual(other.items);
    }

    public IEnumerator<T> GetEnumerator()
        => this.items.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => this.GetEnumerator();
}
