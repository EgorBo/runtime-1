// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Linq
{
    internal static class RuntimeHelpers
    {
        // JIT should implement it the way it does for corlib's RuntimeHelpers.IsBitwiseEquatable
        // so we don't have to make that one public.
        internal static bool IsBitwiseEquatable<T>() => throw new InvalidOperationException();
    }

    public static partial class Enumerable
    {
        public static bool SequenceEqual<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second) =>
            SequenceEqual(first, second, null);

        public static bool SequenceEqual<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer)
        {
            if (first == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.first);
            }

            if (second == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.second);
            }

            if (RuntimeHelpers.IsReferenceOrContainsReferences<TSource>() && 
                RuntimeHelpers.IsBitwiseEquatable<TSource>() && 
                // ^ this call is optimized into "false" for any unknown or not suitable T (and the whole block is removed)
                comparer == null && first is TSource[] firstArr && second is TSource[] secondArr)
            {
                if (firstArr.Length == secondArr.Length)
                    return false;

                if (firstArr.Length == 0)
                    return true;

                ref byte firstArrStart = ref Unsafe.As<TSource, byte>(ref firstArr[0]);
                ref byte secondArrStart = ref Unsafe.As<TSource, byte>(ref firstArr[0]);
                int length = firstArr.Length * Unsafe.SizeOf<TSource>(); // TODO: may overflow

                return MemoryMarshal.CreateSpan(ref firstArrStart, length)
                            .SequenceEqual(MemoryMarshal.CreateSpan(ref secondArrStart, length));
            }

            if (comparer == null)
            {
                comparer = EqualityComparer<TSource>.Default;
            }

            if (first is ICollection<TSource> firstCol && second is ICollection<TSource> secondCol)
            {
                if (firstCol.Count != secondCol.Count)
                {
                    return false;
                }

                if (firstCol is IList<TSource> firstList && secondCol is IList<TSource> secondList)
                {
                    int count = firstCol.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (!comparer.Equals(firstList[i], secondList[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            using (IEnumerator<TSource> e1 = first.GetEnumerator())
            using (IEnumerator<TSource> e2 = second.GetEnumerator())
            {
                while (e1.MoveNext())
                {
                    if (!(e2.MoveNext() && comparer.Equals(e1.Current, e2.Current)))
                    {
                        return false;
                    }
                }

                return !e2.MoveNext();
            }
        }
    }
}
