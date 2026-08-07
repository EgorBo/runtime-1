// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace System
{
    public static partial class MemoryExtensions
    {
        /// <summary>
        /// Returns true when <paramref name="value"/> is a complete UTF-8 scalar that is never whitespace,
        /// i.e. an ASCII byte greater than U+0020. Continuation and lead bytes (>= 0x80) return false.
        /// </summary>
        private static bool IsAsciiNonWhiteSpace(byte value) => (uint)(value - 0x21) <= (0x7F - 0x21);

        internal static ReadOnlySpan<byte> TrimStartUtf8(this ReadOnlySpan<byte> span)
        {
            if (span.Length != 0 && IsAsciiNonWhiteSpace(span[0]))
            {
                return span;
            }

            // Since `DecodeFromUtf8` returns `Rune.ReplacementChar` on failure and that is not
            // whitespace, we can safely treat it as no trimming and leave failure handling up to
            // the caller instead.

            Debug.Assert(!Rune.IsWhiteSpace(Rune.ReplacementChar));

            while (span.Length != 0)
            {
                _ = Rune.DecodeFromUtf8(span, out Rune current, out int bytesConsumed);

                if (!Rune.IsWhiteSpace(current))
                {
                    break;
                }

                span = span[bytesConsumed..];
            }

            return span;
        }

        internal static ReadOnlySpan<byte> TrimUtf8(this ReadOnlySpan<byte> span)
        {
            // Assume that in most cases input doesn't need trimming
            //
            // Since `DecodeFromUtf8` and `DecodeLastFromUtf8` return `Rune.ReplacementChar`
            // on failure and that is not whitespace, we can safely treat it as no trimming
            // and leave failure handling up to the caller instead

            Debug.Assert(!Rune.IsWhiteSpace(Rune.ReplacementChar));

            if (span.Length == 0)
            {
                return span;
            }

            // Every byte in [0x21, 0x7F] is a complete scalar that is never whitespace, so when both
            // ends are one of those there is nothing to trim and no scalar needs to be decoded.
            if (IsAsciiNonWhiteSpace(span[0]) && IsAsciiNonWhiteSpace(span[^1]))
            {
                return span;
            }

            _ = Rune.DecodeFromUtf8(span, out Rune first, out int firstBytesConsumed);

            if (Rune.IsWhiteSpace(first))
            {
                span = span[firstBytesConsumed..];
                return TrimFallback(span);
            }

            _ = Rune.DecodeLastFromUtf8(span, out Rune last, out int lastBytesConsumed);

            if (Rune.IsWhiteSpace(last))
            {
                span = span[..^lastBytesConsumed];
                return TrimFallback(span);
            }

            return span;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static ReadOnlySpan<byte> TrimFallback(ReadOnlySpan<byte> span)
            {
                while (span.Length != 0)
                {
                    _ = Rune.DecodeFromUtf8(span, out Rune current, out int bytesConsumed);

                    if (!Rune.IsWhiteSpace(current))
                    {
                        break;
                    }

                    span = span[bytesConsumed..];
                }

                while (span.Length != 0)
                {
                    _ = Rune.DecodeLastFromUtf8(span, out Rune current, out int bytesConsumed);

                    if (!Rune.IsWhiteSpace(current))
                    {
                        break;
                    }

                    span = span[..^bytesConsumed];
                }

                return span;
            }
        }
    }
}
