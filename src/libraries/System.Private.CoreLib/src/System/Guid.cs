// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Unicode;

namespace System
{
    // Represents a Globally Unique Identifier.
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    [NonVersionable] // This only applies to field layout
    [TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public readonly partial struct Guid
        : ISpanFormattable,
          IComparable,
          IComparable<Guid>,
          IEquatable<Guid>,
          ISpanParsable<Guid>,
          IUtf8SpanFormattable,
          IUtf8SpanParsable<Guid>
    {
        private const byte Variant10xxMask = 0xC0;
        private const byte Variant10xxValue = 0x80;

        private const ushort VersionMask = 0xF000;
        private const ushort Version4Value = 0x4000;
        private const ushort Version7Value = 0x7000;

        public static readonly Guid Empty;

        /// <summary>Gets a <see cref="Guid" /> where all bits are set.</summary>
        /// <remarks>This returns the value: FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF</remarks>
        public static Guid AllBitsSet => new Guid(uint.MaxValue, ushort.MaxValue, ushort.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        private readonly int _a;   // Do not rename (binary serialization)
        private readonly short _b; // Do not rename (binary serialization)
        private readonly short _c; // Do not rename (binary serialization)
        private readonly byte _d;  // Do not rename (binary serialization)
        private readonly byte _e;  // Do not rename (binary serialization)
        private readonly byte _f;  // Do not rename (binary serialization)
        private readonly byte _g;  // Do not rename (binary serialization)
        private readonly byte _h;  // Do not rename (binary serialization)
        private readonly byte _i;  // Do not rename (binary serialization)
        private readonly byte _j;  // Do not rename (binary serialization)
        private readonly byte _k;  // Do not rename (binary serialization)

        // Creates a new guid from an array of bytes.
        public Guid(byte[] b) :
            this(new ReadOnlySpan<byte>(b ?? throw new ArgumentNullException(nameof(b))))
        {
        }

        // Creates a new guid from a read-only span.
        public Guid(ReadOnlySpan<byte> b)
        {
            if (b.Length != 16)
            {
                ThrowGuidArrayCtorArgumentException();
            }

            this = MemoryMarshal.Read<Guid>(b);

            if (!BitConverter.IsLittleEndian)
            {
                _a = BinaryPrimitives.ReverseEndianness(_a);
                _b = BinaryPrimitives.ReverseEndianness(_b);
                _c = BinaryPrimitives.ReverseEndianness(_c);
            }
        }

        public Guid(ReadOnlySpan<byte> b, bool bigEndian)
        {
            if (b.Length != 16)
            {
                ThrowGuidArrayCtorArgumentException();
            }

            this = MemoryMarshal.Read<Guid>(b);

            if (BitConverter.IsLittleEndian == bigEndian)
            {
                _a = BinaryPrimitives.ReverseEndianness(_a);
                _b = BinaryPrimitives.ReverseEndianness(_b);
                _c = BinaryPrimitives.ReverseEndianness(_c);
            }
        }

        [DoesNotReturn]
        [StackTraceHidden]
        private static void ThrowGuidArrayCtorArgumentException()
        {
            throw new ArgumentException(SR.Format(SR.Arg_GuidArrayCtor, "16"), "b");
        }

        [CLSCompliant(false)]
        public Guid(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
        {
            _a = (int)a;
            _b = (short)b;
            _c = (short)c;
            _d = d;
            _e = e;
            _f = f;
            _g = g;
            _h = h;
            _i = i;
            _j = j;
            _k = k;
        }

        // Creates a new GUID initialized to the value represented by the arguments.
        public Guid(int a, short b, short c, byte[] d)
        {
            ArgumentNullException.ThrowIfNull(d);

            if (d.Length != 8)
            {
                throw new ArgumentException(SR.Format(SR.Arg_GuidArrayCtor, "8"), nameof(d));
            }

            _a = a;
            _b = b;
            _c = c;
            _d = d[0];
            _e = d[1];
            _f = d[2];
            _g = d[3];
            _h = d[4];
            _i = d[5];
            _j = d[6];
            _k = d[7];
        }

        // Creates a new GUID initialized to the value represented by the
        // arguments.  The bytes are specified like this to avoid endianness issues.
        public Guid(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
        {
            _a = a;
            _b = b;
            _c = c;
            _d = d;
            _e = e;
            _f = f;
            _g = g;
            _h = h;
            _i = i;
            _j = j;
            _k = k;
        }

        private enum GuidParseThrowStyle : byte
        {
            None = 0,
            All = 1,
            AllButOverflow = 2
        }

        private enum ParseFailure
        {
            Format_ExtraJunkAtEnd,
            Format_GuidBraceAfterLastNumber,
            Format_GuidBrace,
            Format_GuidComma,
            Format_GuidDashes,
            Format_GuidEndBrace,
            Format_GuidHexPrefix,
            Format_GuidInvalidChar,
            Format_GuidInvLen,
            Format_GuidUnrecognized,
            Overflow_Byte,
            Overflow_UInt32,
        }

        // Reports a parse failure. Always returns false so that callers can `return SetFailure(...);`,
        // and throws instead when the caller asked for exceptions rather than a bool.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool SetFailure(GuidParseThrowStyle throwStyle, ParseFailure failureKind, out Guid result)
        {
            result = default;

            if (throwStyle == GuidParseThrowStyle.None)
            {
                return false;
            }

            if (failureKind == ParseFailure.Overflow_UInt32 && throwStyle == GuidParseThrowStyle.All)
            {
                throw new OverflowException(SR.Overflow_UInt32);
            }

            throw new FormatException(failureKind switch
            {
                ParseFailure.Format_ExtraJunkAtEnd => SR.Format_ExtraJunkAtEnd,
                ParseFailure.Format_GuidBraceAfterLastNumber => SR.Format_GuidBraceAfterLastNumber,
                ParseFailure.Format_GuidBrace => SR.Format_GuidBrace,
                ParseFailure.Format_GuidComma => SR.Format_GuidComma,
                ParseFailure.Format_GuidDashes => SR.Format_GuidDashes,
                ParseFailure.Format_GuidEndBrace => SR.Format_GuidEndBrace,
                ParseFailure.Format_GuidHexPrefix => SR.Format_GuidHexPrefix,
                ParseFailure.Format_GuidInvalidChar => SR.Format_GuidInvalidChar,
                ParseFailure.Format_GuidInvLen => SR.Format_GuidInvLen,
                _ => SR.Format_GuidUnrecognized
            });
        }

        // Creates a new guid based on the value in the string.  The value is made up
        // of hex digits speared by the dash ("-"). The string may begin and end with
        // brackets ("{", "}").
        //
        // The string must be of the form dddddddd-dddd-dddd-dddd-dddddddddddd. where
        // d is a hex digit. (That is 8 hex digits, followed by 4, then 4, then 4,
        // then 12) such as: "CA761232-ED42-11CE-BACD-00AA0057B223"
        public Guid(string g)
        {
            ArgumentNullException.ThrowIfNull(g);

            bool success = TryParseGuid(g.AsSpan(), GuidParseThrowStyle.All, out Guid result);
            Debug.Assert(success, "GuidParseThrowStyle.All means throw on all failures");

            this = result;
        }

        /// <summary>Gets the value of the variant field for the <see cref="Guid" />.</summary>
        /// <remarks>
        ///     <para>This returns all 4 bits as is, some users may only care about fewer bits of the variant field and should refer to RFC 9562 for how to interpret the result.</para>
        ///     <para>For example, UUIDv7 may only want to consider the 2 most significant bits of the field as the least 2 significant bits are documented as "don't-care".</para>
        /// </remarks>
        public int Variant => _d >> 4;

        /// <summary>Gets the value of the version field for the <see cref="Guid" />.</summary>
        /// <remarks>
        ///     <para>This corresponds to the most significant 4 bits of the 6th byte: 00000000-0000-F000-0000-000000000000.</para>
        ///     <para>See RFC 9562 for more information on how to interpret this value.</para>
        /// </remarks>
        public int Version => (ushort)_c >>> 12;

        /// <summary>Creates a new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</summary>
        /// <returns>A new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</returns>
        /// <remarks>
        ///     <para>This uses <see cref="DateTimeOffset.UtcNow" /> to determine the Unix Epoch timestamp source.</para>
        ///     <para>This seeds the rand_a and rand_b sub-fields with random data.</para>
        /// </remarks>
        public static Guid CreateVersion7() => CreateVersion7(DateTimeOffset.UtcNow);

        /// <summary>Creates a new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</summary>
        /// <param name="timestamp">The date time offset used to determine the Unix Epoch timestamp.</param>
        /// <returns>A new <see cref="Guid" /> according to RFC 9562, following the Version 7 format.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timestamp" /> represents an offset prior to <see cref="DateTimeOffset.UnixEpoch" />.</exception>
        /// <remarks>
        ///     <para>This seeds the rand_a and rand_b sub-fields with random data.</para>
        /// </remarks>
        public static Guid CreateVersion7(DateTimeOffset timestamp)
        {
            // NewGuid uses CoCreateGuid on Windows and Interop.GetCryptographicallySecureRandomBytes on Unix to get
            // cryptographically-secure random bytes. We could use Interop.BCrypt.BCryptGenRandom to generate the random
            // bytes on Windows, as is done in RandomNumberGenerator, but that's measurably slower than using CoCreateGuid.
            // And while CoCreateGuid only generates 122 bits of randomness, the other 6 bits being for the version / variant
            // fields, this method also needs those bits to be non-random, so we can just use NewGuid for efficiency.
            Guid result = NewGuid();

            // 2^48 is roughly 8925.5 years, which from the Unix Epoch means we won't
            // overflow until around July of 10,895. So there isn't any need to handle
            // it given that DateTimeOffset.MaxValue is December 31, 9999. However, we
            // can't represent timestamps prior to the Unix Epoch since UUIDv7 explicitly
            // stores a 48-bit unsigned value, so we do need to throw if one is passed in.

            long unix_ts_ms = timestamp.ToUnixTimeMilliseconds();
            ArgumentOutOfRangeException.ThrowIfNegative(unix_ts_ms, nameof(timestamp));

            Unsafe.AsRef(in result._a) = (int)(unix_ts_ms >> 16);
            Unsafe.AsRef(in result._b) = (short)(unix_ts_ms);

            Unsafe.AsRef(in result._c) = (short)((result._c & ~VersionMask) | Version7Value);
            Unsafe.AsRef(in result._d) = (byte)((result._d & ~Variant10xxMask) | Variant10xxValue);

            return result;
        }

        public static Guid Parse(string input)
        {
            ArgumentNullException.ThrowIfNull(input);
            return Parse((ReadOnlySpan<char>)input);
        }

        public static Guid Parse(ReadOnlySpan<char> input)
        {
            bool success = TryParseGuid(input, GuidParseThrowStyle.AllButOverflow, out Guid result);
            Debug.Assert(success, "GuidParseThrowStyle.AllButOverflow means throw on all failures");

            return result;
        }

        /// <summary>
        /// Parses the specified sequence of UTF-8 encoded bytes and returns a new <see cref="Guid"/>.
        /// </summary>
        /// <param name="utf8Text">A span containing the UTF-8 encoded representation of the GUID to parse.</param>
        /// <returns>The parsed <see cref="Guid"/>.</returns>
        public static Guid Parse(ReadOnlySpan<byte> utf8Text)
        {
            bool success = TryParseGuid(utf8Text, GuidParseThrowStyle.AllButOverflow, out Guid result);
            Debug.Assert(success, "GuidParseThrowStyle.AllButOverflow means throw on all failures");

            return result;
        }

        public static bool TryParse([NotNullWhen(true)] string? input, out Guid result)
        {
            if (input == null)
            {
                result = default;
                return false;
            }

            return TryParse((ReadOnlySpan<char>)input, out result);
        }

        public static bool TryParse(ReadOnlySpan<char> input, out Guid result) =>
            TryParseGuid(input, GuidParseThrowStyle.None, out result);

        /// <summary>
        /// Tries to parse the specified sequence of UTF-8 encoded bytes as a GUID.
        /// </summary>
        /// <param name="utf8Text">A span containing the UTF-8 encoded representation of the GUID to parse.</param>
        /// <param name="result">When this method returns, contains the parsed <see cref="Guid"/>, if the parse succeeded; otherwise, the default value.</param>
        /// <returns><see langword="true"/> if the parse operation succeeded; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Guid result) =>
            TryParseGuid(utf8Text, GuidParseThrowStyle.None, out result);

        public static Guid ParseExact(string input, [StringSyntax(StringSyntaxAttribute.GuidFormat)] string format)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(format);

            return ParseExact((ReadOnlySpan<char>)input, (ReadOnlySpan<char>)format);
        }

        public static Guid ParseExact(ReadOnlySpan<char> input, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format)
        {
            if (format.Length != 1)
            {
                // all acceptable format strings are of length 1
                ThrowBadGuidFormatSpecification();
            }

            input = input.Trim();

            const GuidParseThrowStyle ThrowStyle = GuidParseThrowStyle.AllButOverflow;
            bool success;
            Guid result;
            switch ((char)(format[0] | 0x20))
            {
                case 'd': success = TryParseExactD(input, ThrowStyle, out result); break;
                case 'n': success = TryParseExactN(input, ThrowStyle, out result); break;
                case 'b': success = TryParseExactB(input, ThrowStyle, out result); break;
                case 'p': success = TryParseExactP(input, ThrowStyle, out result); break;
                case 'x': success = TryParseExactX(input, ThrowStyle, out result); break;
                default: throw new FormatException(SR.Format_InvalidGuidFormatSpecification);
            }
            Debug.Assert(success, "GuidParseThrowStyle.AllButOverflow means throw on all failures");
            return result;
        }

        public static bool TryParseExact([NotNullWhen(true)] string? input, [NotNullWhen(true), StringSyntax(StringSyntaxAttribute.GuidFormat)] string? format, out Guid result)
        {
            if (input == null)
            {
                result = default;
                return false;
            }

            return TryParseExact((ReadOnlySpan<char>)input, format, out result);
        }

        public static bool TryParseExact(ReadOnlySpan<char> input, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format, out Guid result)
        {
            if (format.Length != 1 || input.Length < 32) // Minimal length we can parse ('N' format)
            {
                result = default;
                return false;
            }

            input = input.Trim();

            const GuidParseThrowStyle ThrowStyle = GuidParseThrowStyle.None;
            switch (format[0] | 0x20)
            {
                case 'd': return TryParseExactD(input, ThrowStyle, out result);
                case 'n': return TryParseExactN(input, ThrowStyle, out result);
                case 'b': return TryParseExactB(input, ThrowStyle, out result);
                case 'p': return TryParseExactP(input, ThrowStyle, out result);
                case 'x': return TryParseExactX(input, ThrowStyle, out result);
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryParseGuid<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            guidString = Number.SpanTrim(guidString); // Remove whitespace from beginning and end

            if (guidString.Length < 32) // Minimal length we can parse ('N' format)
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidUnrecognized, out result);
            }

            return TChar.CastToUInt32(guidString[0]) switch
            {
                '(' => TryParseExactP(guidString, throwStyle, out result),
                '{' => guidString[9] == TChar.CastFrom('-') ?
                        TryParseExactB(guidString, throwStyle, out result) :
                        TryParseExactX(guidString, throwStyle, out result),
                _ => guidString[8] == TChar.CastFrom('-') ?
                        TryParseExactD(guidString, throwStyle, out result) :
                        TryParseExactN(guidString, throwStyle, out result),
            };
        }

        private static bool TryParseExactB<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // e.g. "{d85b1407-351d-4694-9392-03acc5870eb1}"

            if (guidString.Length != 38 || guidString[0] != TChar.CastFrom('{') || guidString[37] != TChar.CastFrom('}'))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidInvLen, out result);
            }

            return TryParseExactD(guidString.Slice(1, 36), throwStyle, out result);
        }

        private static bool TryParseExactD<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // e.g. "d85b1407-351d-4694-9392-03acc5870eb1"

            if (guidString.Length != 36)
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidInvLen, out result);
            }

            if (guidString[8] != TChar.CastFrom('-') || guidString[13] != TChar.CastFrom('-') || guidString[18] != TChar.CastFrom('-') || guidString[23] != TChar.CastFrom('-'))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidDashes, out result);
            }

            return TryDecodeD(guidString, out result) || TryCompatParsingOrFail(guidString, throwStyle, out result);
        }

        // The 'D' format has some undesirable behavior leftover from its original implementation:
        // - Components may begin with "0x" and/or "+", but the expected length of each component
        //   needs to include those prefixes, e.g. a four digit component could be "1234" or
        //   "0x34" or "+0x4" or "+234", but not "0x1234" nor "+1234" nor "+0x1234".
        // - "0X" is valid instead of "0x"
        // We continue to support these but expect them to be incredibly rare.  As such, we
        // optimize for correctly formed strings where all the digits are valid hex, and only
        // fall back to supporting these other forms if parsing fails.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryCompatParsingOrFail<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            Debug.Assert(guidString.Length == 36);

            if (guidString.ContainsAny(TChar.CastFrom('X'), TChar.CastFrom('x'), TChar.CastFrom('+')) &&
                TryParseHex(guidString.Slice(0, 8), out uint a) && // _a
                TryParseHex(guidString.Slice(9, 4), out uint b) && // _b
                TryParseHex(guidString.Slice(14, 4), out uint c) && // _c
                TryParseHex(guidString.Slice(19, 4), out uint de) && // _d, _e
                TryParseHex(guidString.Slice(24, 4), out uint fg) && // _f, _g
                // Unlike the other components, this one never allowed 0x or +, so we can parse it as straight hex.
                Number.TryParseBinaryIntegerHexOrBinaryNumberStyle<TChar, uint, Number.HexParser<uint>>(guidString.Slice(28, 8), NumberStyles.AllowHexSpecifier, out uint hijk, out _) == Number.ParsingStatus.OK) // _h, _i, _j, _k
            {
                result = new Guid(a, (ushort)b, (ushort)c,
                    (byte)(de >> 8), (byte)de,
                    (byte)(fg >> 8), (byte)fg,
                    (byte)(hijk >> 24), (byte)(hijk >> 16), (byte)(hijk >> 8), (byte)hijk);
                return true;
            }

            return SetFailure(throwStyle, ParseFailure.Format_GuidInvalidChar, out result);
        }

        private static bool TryParseExactN<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // e.g. "d85b1407351d4694939203acc5870eb1"

            if (guidString.Length == 32 && TryDecodeN(guidString, out result))
            {
                return true;
            }

            return SetFailure(throwStyle, guidString.Length != 32 ? ParseFailure.Format_GuidInvLen : ParseFailure.Format_GuidInvalidChar, out result);
        }

        private static bool TryParseExactP<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // e.g. "(d85b1407-351d-4694-9392-03acc5870eb1)"

            if (guidString.Length != 38 || guidString[0] != TChar.CastFrom('(') || guidString[37] != TChar.CastFrom(')'))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidInvLen, out result);
            }

            return TryParseExactD(guidString.Slice(1, 36), throwStyle, out result);
        }

        private static bool TryParseExactX<TChar>(ReadOnlySpan<TChar> guidString, GuidParseThrowStyle throwStyle, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // e.g. "{0xd85b1407,0x351d,0x4694,{0x93,0x92,0x03,0xac,0xc5,0x87,0x0e,0xb1}}"

            // Compat notes due to the previous implementation's implementation details.
            // - Each component need not be the full expected number of digits.
            // - Each component may contain any number of leading 0s
            // - The "short" components are parsed as 32-bits and only considered to overflow if they'd overflow 32 bits.
            // - The "byte" components are parsed as 32-bits and are considered to overflow if they'd overflow 8 bits,
            //   but for the Guid ctor, whether they overflow 8 bits or 32 bits results in differing exceptions.
            // - Components may begin with "0x", "0x+", even "0x+0x".
            // - "0X" is valid instead of "0x"

            // Eat all of the whitespace.  Unlike the other forms, X allows for any amount of whitespace
            // anywhere, not just at the beginning and end.
            if (!TryEatAllWhitespace(guidString, out guidString))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidInvalidChar, out result);
            }

            // Check for leading '{'
            if (guidString.Length == 0 || guidString[0] != TChar.CastFrom('{'))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidBrace, out result);
            }

            // Check for '0x'
            if (!IsHexPrefix(guidString, 1))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidHexPrefix, out result);
            }

            // Find the end of this hex number (since it is not fixed length)
            int numStart = 3;
            int numLen = guidString.Slice(numStart).IndexOf(TChar.CastFrom(','));
            if (numLen <= 0)
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidComma, out result);
            }

            bool overflow = false;
            if (!TryParseHex(guidString.Slice(numStart, numLen), out uint a, ref overflow) || overflow)
            {
                return SetFailure(throwStyle, overflow ? ParseFailure.Overflow_UInt32 : ParseFailure.Format_GuidInvalidChar, out result);
            }

            // Check for '0x'
            if (!IsHexPrefix(guidString, numStart + numLen + 1))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidHexPrefix, out result);
            }
            // +3 to get by ',0x'
            numStart = numStart + numLen + 3;
            numLen = guidString.Slice(numStart).IndexOf(TChar.CastFrom(','));
            if (numLen <= 0)
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidComma, out result);
            }

            // Read in the number
            if (!TryParseHex(guidString.Slice(numStart, numLen), out ushort b, ref overflow) || overflow)
            {
                return SetFailure(throwStyle, overflow ? ParseFailure.Overflow_UInt32 : ParseFailure.Format_GuidInvalidChar, out result);
            }

            // Check for '0x'
            if (!IsHexPrefix(guidString, numStart + numLen + 1))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidHexPrefix, out result);
            }
            // +3 to get by ',0x'
            numStart = numStart + numLen + 3;
            numLen = guidString.Slice(numStart).IndexOf(TChar.CastFrom(','));
            if (numLen <= 0)
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidComma, out result);
            }

            // Read in the number
            if (!TryParseHex(guidString.Slice(numStart, numLen), out ushort c, ref overflow) || overflow)
            {
                return SetFailure(throwStyle, overflow ? ParseFailure.Overflow_UInt32 : ParseFailure.Format_GuidInvalidChar, out result);
            }

            // Check for '{'
            if ((uint)guidString.Length <= (uint)(numStart + numLen + 1) || guidString[numStart + numLen + 1] != TChar.CastFrom('{'))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidBrace, out result);
            }

            // Prepare for loop
            Span<byte> remaining = stackalloc byte[8];
            numLen++;
            for (int i = 0; i < 8; i++)
            {
                // Check for '0x'
                if (!IsHexPrefix(guidString, numStart + numLen + 1))
                {
                    return SetFailure(throwStyle, ParseFailure.Format_GuidHexPrefix, out result);
                }

                // +3 to get by ',0x' or '{0x' for first case
                numStart = numStart + numLen + 3;

                // Calculate number length
                if (i < 7)  // first 7 cases
                {
                    numLen = guidString.Slice(numStart).IndexOf(TChar.CastFrom(','));
                    if (numLen <= 0)
                    {
                        return SetFailure(throwStyle, ParseFailure.Format_GuidComma, out result);
                    }
                }
                else // last case ends with '}', not ','
                {
                    numLen = guidString.Slice(numStart).IndexOf(TChar.CastFrom('}'));
                    if (numLen <= 0)
                    {
                        return SetFailure(throwStyle, ParseFailure.Format_GuidBraceAfterLastNumber, out result);
                    }
                }

                // Read in the number
                if (!TryParseHex(guidString.Slice(numStart, numLen), out uint byteVal, ref overflow) || overflow || byteVal > byte.MaxValue)
                {
                    // The previous implementation had some odd inconsistencies, which are carried forward here.
                    // The byte values in the X format are treated as integers with regards to overflow, so
                    // a "byte" value like 0xddd in Guid's ctor results in a FormatException but 0xddddddddd results
                    // in OverflowException.
                    return SetFailure(throwStyle,
                        overflow ? ParseFailure.Overflow_UInt32 :
                        byteVal > byte.MaxValue ? ParseFailure.Overflow_Byte :
                        ParseFailure.Format_GuidInvalidChar, out result);
                }
                remaining[i] = (byte)byteVal;
            }

            // Check for last '}'
            if (numStart + numLen + 1 >= guidString.Length || guidString[numStart + numLen + 1] != TChar.CastFrom('}'))
            {
                return SetFailure(throwStyle, ParseFailure.Format_GuidEndBrace, out result);
            }

            // Check if we have extra characters at the end
            if (numStart + numLen + 1 != guidString.Length - 1)
            {
                return SetFailure(throwStyle, ParseFailure.Format_ExtraJunkAtEnd, out result);
            }

            result = new Guid(a, b, c,
                remaining[0], remaining[1], remaining[2], remaining[3],
                remaining[4], remaining[5], remaining[6], remaining[7]);
            return true;
        }

        /// <summary>
        /// Decodes the 32 hex digits of an 'N'-formatted GUID (e.g. "d85b1407351d4694939203acc5870eb1").
        /// </summary>
        private static bool TryDecodeN<TChar>(ReadOnlySpan<TChar> guidString, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // Redundant, but it lets the JIT fold the bounds checks of the loads below.
            if (guidString.Length != 32)
            {
                result = default;
                return false;
            }

            if ((Ssse3.IsSupported || AdvSimd.Arm64.IsSupported || PackedSimd.IsSupported) && BitConverter.IsLittleEndian)
            {
                Vector128<ushort> nonAscii = Vector128<ushort>.Zero;
                Vector128<byte> lower = LoadAscii16(guidString, 0, ref nonAscii);
                Vector128<byte> upper = LoadAscii16(guidString, 16, ref nonAscii);
                return TryDecodeAscii32<TChar>(lower, upper, nonAscii, out result);
            }

            Span<byte> bytes = stackalloc byte[16];
            if (TryDecodeHexScalar(guidString, bytes))
            {
                result = new Guid(bytes, bigEndian: true);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Decodes the 32 hex digits of a 'D'-formatted GUID (e.g. "d85b1407-351d-4694-9392-03acc5870eb1").
        /// The four dashes must already have been validated by the caller.
        /// </summary>
        private static bool TryDecodeD<TChar>(ReadOnlySpan<TChar> guidString, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            // Redundant, but it lets the JIT fold the bounds checks of the loads below.
            if (guidString.Length != 36)
            {
                result = default;
                return false;
            }

            if ((Ssse3.IsSupported || AdvSimd.Arm64.IsSupported || PackedSimd.IsSupported) && BitConverter.IsLittleEndian)
            {
                // The 32 hex digits live at 0-7, 9-12, 14-17, 19-22 and 24-35. Read three overlapping
                // 16-character windows covering 0-15, 16-31 and 20-35, then squeeze the dashes out.
                Vector128<ushort> nonAscii = Vector128<ushort>.Zero;
                Vector128<byte> a = LoadAscii16(guidString, 0, ref nonAscii);
                Vector128<byte> b = LoadAscii16(guidString, 16, ref nonAscii);
                Vector128<byte> c = LoadAscii16(guidString, 20, ref nonAscii);

                Vector128<byte> lower, upper;
                if (AdvSimd.Arm64.IsSupported)
                {
                    // Arm64 can index a 32-byte table made of two registers, which halves the shuffle count.
                    lower = AdvSimd.Arm64.VectorTableLookup((a, b), Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 14, 15, 16, 17));
                    upper = AdvSimd.Arm64.VectorTableLookup((b, c), Vector128.Create((byte)3, 4, 5, 6, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31));
                }
                else
                {
                    // 0xFF selects nothing, leaving a zero behind for the other half of the pair to fill in.
                    lower = Vector128.Shuffle(a, Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 14, 15, 0xFF, 0xFF)) |
                            Vector128.Shuffle(b, Vector128.Create((byte)0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0, 1));
                    upper = Vector128.Shuffle(b, Vector128.Create((byte)3, 4, 5, 6, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF)) |
                            Vector128.Shuffle(c, Vector128.Create((byte)0xFF, 0xFF, 0xFF, 0xFF, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15));
                }

                return TryDecodeAscii32<TChar>(lower, upper, nonAscii, out result);
            }

            Span<byte> bytes = stackalloc byte[16];
            if (TryDecodeHexScalar(guidString.Slice(0, 8), bytes.Slice(0, 4)) &&
                TryDecodeHexScalar(guidString.Slice(9, 4), bytes.Slice(4, 2)) &&
                TryDecodeHexScalar(guidString.Slice(14, 4), bytes.Slice(6, 2)) &&
                TryDecodeHexScalar(guidString.Slice(19, 4), bytes.Slice(8, 2)) &&
                TryDecodeHexScalar(guidString.Slice(24, 12), bytes.Slice(10, 6)))
            {
                result = new Guid(bytes, bigEndian: true);
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Loads the 16 characters starting at <paramref name="offset"/> as ASCII bytes. UTF-16 code units
        /// above U+00FF are truncated, so their original values are accumulated into <paramref name="nonAscii"/>
        /// for the caller to reject; every valid hex digit is below U+0080.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> LoadAscii16<TChar>(ReadOnlySpan<TChar> guidString, int offset, ref Vector128<ushort> nonAscii) where TChar : unmanaged, IUtfChar<TChar>
        {
            if (typeof(TChar) == typeof(byte))
            {
                return Vector128.Create(MemoryMarshal.Cast<TChar, byte>(guidString.Slice(offset, 16)));
            }

            Debug.Assert(typeof(TChar) == typeof(char));
            ReadOnlySpan<ushort> utf16 = MemoryMarshal.Cast<TChar, ushort>(guidString.Slice(offset, 16));
            Vector128<ushort> first = Vector128.Create(utf16);
            Vector128<ushort> second = Vector128.Create(utf16.Slice(8));
            nonAscii |= first | second;
            return Ascii.ExtractAsciiVector(first, second);
        }

        /// <summary>
        /// Converts 32 ASCII hex digits, in the order they appear in the string, into the <see cref="Guid"/>
        /// they represent. Returns false if any character is not a hex digit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryDecodeAscii32<TChar>(Vector128<byte> lower, Vector128<byte> upper, Vector128<ushort> nonAscii, out Guid result) where TChar : unmanaged, IUtfChar<TChar>
        {
            Debug.Assert(BitConverter.IsLittleEndian);

            Vector128<byte> lowerNibbles = AsciiToNibbles(lower);
            Vector128<byte> upperNibbles = AsciiToNibbles(upper);

            // A nibble above 0x0F means the character wasn't a hex digit. Checking the sign bit after a
            // saturating add tests all 32 of them at once.
            if (Vector128.AddSaturate(Vector128.Max(lowerNibbles, upperNibbles), Vector128.Create((byte)(127 - 15))).ExtractMostSignificantBits() != 0 ||
                (typeof(TChar) != typeof(byte) && !Utf16Utility.AllCharsInVectorAreAscii(nonAscii)))
            {
                result = default;
                return false;
            }

            // The bytes come out in string order, i.e. big-endian, while _a, _b and _c are little-endian.
            result = Unsafe.BitCast<Vector128<byte>, Guid>(Vector128.Shuffle(
                PackNibbles(lowerNibbles, upperNibbles),
                Vector128.Create((byte)3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15)));
            return true;
        }

        /// <summary>
        /// Converts 16 ASCII characters into the hex digits they represent. Characters that are not hex
        /// digits produce a value greater than 0x0F.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> AsciiToNibbles(Vector128<byte> ascii)
        {
            // Based on "Algorithm #3" https://github.com/WojciechMula/toys/blob/master/simd-parse-hex/geoff_algorithm.cpp
            // by Geoff Langdale and Wojciech Mula, as also used by HexConverter.

            // Move digits '0'..'9' into range 0xf6..0xff, then correct the range to 0xf0..0xf9.
            // All other bytes become less than 0xf0.
            Vector128<byte> digits = Vector128.SubtractSaturate(ascii + Vector128.Create((byte)(0xFF - '9')), Vector128.Create((byte)6));

            // Convert 'a'..'f' to 'A'..'F' and move hex letters 'A'..'F' into range 0..5, then correct the
            // range into 10..15. Bytes that aren't hex letters become greater than 0x0f.
            Vector128<byte> letters = Vector128.AddSaturate((ascii & Vector128.Create((byte)0xDF)) - Vector128.Create((byte)'A'), Vector128.Create((byte)10));

            // Whichever of the two interpretations produced something in range wins; if neither did, the
            // result stays above 0x0f and the caller rejects the input.
            return Vector128.Min(digits - Vector128.Create((byte)0xF0), letters);
        }

        /// <summary>Combines 32 hex digits into the 16 bytes they encode, most significant digit first.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> PackNibbles(Vector128<byte> lower, Vector128<byte> upper)
        {
            if (Ssse3.IsSupported)
            {
                // Multiplies each even/odd pair by 16/1 and adds them, producing 8 bytes' worth of values
                // per input vector, each already zero-extended into a 16-bit lane.
                Vector128<short> multiplier = Vector128.Create((short)0x0110);
                return Sse2.PackUnsignedSaturate(
                    Ssse3.MultiplyAddAdjacent(lower, multiplier.AsSByte()),
                    Ssse3.MultiplyAddAdjacent(upper, multiplier.AsSByte()));
            }

            if (AdvSimd.Arm64.IsSupported)
            {
                // uzp1/uzp2 split the 32 digits into the 16 leading and the 16 trailing ones.
                return (AdvSimd.Arm64.UnzipEven(lower, upper) << 4) | AdvSimd.Arm64.UnzipOdd(lower, upper);
            }

            // Each 16-bit lane holds two adjacent digits, the leading one in its low byte.
            Vector128<ushort> first = lower.AsUInt16();
            Vector128<ushort> second = upper.AsUInt16();
            return Vector128.Narrow(
                (first << 4) | Vector128.ShiftRightLogical(first, 8),
                (second << 4) | Vector128.ShiftRightLogical(second, 8));
        }

        /// <summary>Decodes <paramref name="guidString"/> as pairs of hex digits into <paramref name="destination"/>.</summary>
        private static bool TryDecodeHexScalar<TChar>(ReadOnlySpan<TChar> guidString, Span<byte> destination) where TChar : unmanaged, IUtfChar<TChar>
        {
            Debug.Assert(guidString.Length == destination.Length * 2);

            int invalidIfNegative = 0;
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = DecodeByte(guidString[i * 2], guidString[(i * 2) + 1], ref invalidIfNegative);
            }

            return invalidIfNegative >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte DecodeByte<TChar>(TChar ch1, TChar ch2, ref int invalidIfNegative) where TChar : unmanaged, IUtfChar<TChar>
        {
            ReadOnlySpan<byte> lookup = HexConverter.CharToHexLookup;
            Debug.Assert(lookup.Length == 256);

            uint c1 = typeof(TChar) == typeof(byte) ? TChar.CastToUInt32(ch1) : Math.Min(TChar.CastToUInt32(ch1), 0x7F);
            uint c2 = typeof(TChar) == typeof(byte) ? TChar.CastToUInt32(ch2) : Math.Min(TChar.CastToUInt32(ch2), 0x7F);
            int upper = (sbyte)lookup[(int)c1];
            int lower = (sbyte)lookup[(int)c2];

            int result = (upper << 4) | lower;
            invalidIfNegative |= result;
            return (byte)result;
        }

        private static bool TryParseHex<TChar>(ReadOnlySpan<TChar> guidString, out ushort result, ref bool overflow) where TChar : unmanaged, IUtfChar<TChar>
        {
            bool success = TryParseHex(guidString, out uint tmp, ref overflow);
            result = (ushort)tmp;
            return success;
        }

        private static bool TryParseHex<TChar>(ReadOnlySpan<TChar> guidString, out uint result) where TChar : unmanaged, IUtfChar<TChar>
        {
            bool overflowIgnored = false;
            return TryParseHex(guidString, out result, ref overflowIgnored);
        }

        private static bool TryParseHex<TChar>(ReadOnlySpan<TChar> guidString, out uint result, ref bool overflow) where TChar : unmanaged, IUtfChar<TChar>
        {
            if (guidString.Length > 0)
            {
                if (guidString[0] == TChar.CastFrom('+'))
                {
                    guidString = guidString.Slice(1);
                }

                if (guidString.Length > 1 && guidString[0] == TChar.CastFrom('0') && (guidString[1] | TChar.CastFrom(0x20)) == TChar.CastFrom('x'))
                {
                    guidString = guidString.Slice(2);
                }
            }

            // Skip past leading 0s.
            int i = 0;
            for (; i < guidString.Length && guidString[i] == TChar.CastFrom('0'); i++) ;

            int processedDigits = 0;
            uint tmp = 0;
            for (; i < guidString.Length; i++)
            {
                int c = int.CreateTruncating(guidString[i]);
                int numValue = HexConverter.FromChar(c);
                if (numValue == 0xFF)
                {
                    if (processedDigits > 8) overflow = true;
                    result = 0;
                    return false;
                }
                tmp = (tmp * 16) + (uint)numValue;
                processedDigits++;
            }

            if (processedDigits > 8) overflow = true;
            result = tmp;
            return true;
        }

        /// <summary>
        /// Removes every whitespace character from <paramref name="str"/>. Returns false only when
        /// <typeparamref name="TChar"/> is <see cref="byte"/> and the input isn't well-formed UTF-8.
        /// </summary>
        private static bool TryEatAllWhitespace<TChar>(ReadOnlySpan<TChar> str, out ReadOnlySpan<TChar> result) where TChar : unmanaged, IUtfChar<TChar>
        {
            if (typeof(TChar) == typeof(char))
            {
                ReadOnlySpan<char> charSpan = Unsafe.BitCast<ReadOnlySpan<TChar>, ReadOnlySpan<char>>(str);

                // Find the first whitespace character. If there is none, just return the input.
                int i = charSpan.IndexOfAnyWhiteSpace();
                if (i < 0)
                {
                    result = str;
                    return true;
                }

                // There was at least one whitespace. Copy over everything prior to it to a new array.
                var chArr = new char[charSpan.Length];
                charSpan.Slice(0, i).CopyTo(chArr);
                int newLength = i;

                // Loop through the remaining chars, copying over non-whitespace.
                for (; i < charSpan.Length; i++)
                {
                    char c = charSpan[i];
                    if (!char.IsWhiteSpace(c))
                    {
                        chArr[newLength++] = c;
                    }
                }

                // Return the string with the whitespace removed.
                result = Unsafe.BitCast<ReadOnlySpan<char>, ReadOnlySpan<TChar>>(new ReadOnlySpan<char>(chArr, 0, newLength));
                return true;
            }
            else
            {
                Debug.Assert(typeof(TChar) == typeof(byte));

                ReadOnlySpan<byte> srcUtf8Span = Unsafe.BitCast<ReadOnlySpan<TChar>, ReadOnlySpan<byte>>(str);

                // Every byte in [0x21, 0x7F] is a complete, non-whitespace scalar, so an input made up
                // only of those needs neither a scan for whitespace nor a UTF-8 well-formedness check.
                if (!srcUtf8Span.ContainsAnyExceptInRange((byte)0x21, (byte)0x7F))
                {
                    result = str;
                    return true;
                }

                // Otherwise decode scalar by scalar, copying over everything that isn't whitespace.
                Span<byte> destUtf8Span = new byte[srcUtf8Span.Length];
                int newLength = 0;
                int i = 0;
                while (i < srcUtf8Span.Length)
                {
                    if (Rune.DecodeFromUtf8(srcUtf8Span.Slice(i), out Rune current, out int bytesConsumed) != Buffers.OperationStatus.Done)
                    {
                        result = default;
                        return false;
                    }

                    if (!Rune.IsWhiteSpace(current))
                    {
                        srcUtf8Span.Slice(i, bytesConsumed).CopyTo(destUtf8Span.Slice(newLength));
                        newLength += bytesConsumed;
                    }

                    i += bytesConsumed;
                }

                result = Unsafe.BitCast<ReadOnlySpan<byte>, ReadOnlySpan<TChar>>(destUtf8Span.Slice(0, newLength));
                return true;
            }
        }

        private static bool IsHexPrefix<TChar>(ReadOnlySpan<TChar> str, int i) where TChar : unmanaged, IUtfChar<TChar> =>
            i + 1 < str.Length &&
            str[i] == TChar.CastFrom('0') &&
            (str[i + 1] | TChar.CastFrom(0x20)) == TChar.CastFrom('x');

        // Returns an unsigned byte array containing the GUID.
        public byte[] ToByteArray()
        {
            var g = new byte[16];
            if (BitConverter.IsLittleEndian)
            {
                MemoryMarshal.Write(g, in this);
            }
            else
            {
                // slower path for BigEndian
                Guid guid = new Guid(MemoryMarshal.AsBytes(new ReadOnlySpan<Guid>(in this)), false);
                MemoryMarshal.Write(g, in guid);
            }
            return g;
        }


        // Returns an unsigned byte array containing the GUID.
        public byte[] ToByteArray(bool bigEndian)
        {
            var g = new byte[16];
            if (BitConverter.IsLittleEndian != bigEndian)
            {
                MemoryMarshal.Write(g, in this);
            }
            else
            {
                // slower path for Reverse
                Guid guid = new Guid(MemoryMarshal.AsBytes(new ReadOnlySpan<Guid>(in this)), bigEndian);
                MemoryMarshal.Write(g, in guid);
            }
            return g;
        }

        // Returns whether bytes are successfully written to given span.
        public bool TryWriteBytes(Span<byte> destination)
        {
            if (destination.Length < 16)
                return false;

            if (BitConverter.IsLittleEndian)
            {
                MemoryMarshal.Write(destination, in this);
            }
            else
            {
                // slower path for BigEndian
                Guid guid = new Guid(MemoryMarshal.AsBytes(new ReadOnlySpan<Guid>(in this)), false);
                MemoryMarshal.Write(destination, in guid);
            }
            return true;
        }

        // Returns whether bytes are successfully written to given span.
        public bool TryWriteBytes(Span<byte> destination, bool bigEndian, out int bytesWritten)
        {
            if (destination.Length < 16)
            {
                bytesWritten = 0;
                return false;
            }

            if (BitConverter.IsLittleEndian != bigEndian)
            {
                MemoryMarshal.Write(destination, in this);
            }
            else
            {
                // slower path for Reverse
                Guid guid = new Guid(MemoryMarshal.AsBytes(new ReadOnlySpan<Guid>(in this)), bigEndian);
                MemoryMarshal.Write(destination, in guid);
            }
            bytesWritten = 16;
            return true;
        }

        public override int GetHashCode()
        {
            // Simply XOR all the bits of the GUID 32 bits at a time.
            ref int r = ref Unsafe.AsRef(in _a);
            return r ^ Unsafe.Add(ref r, 1) ^ Unsafe.Add(ref r, 2) ^ Unsafe.Add(ref r, 3);
        }

        // Returns true if and only if the guid represented
        //  by o is the same as this instance.
        public override bool Equals([NotNullWhen(true)] object? o) => o is Guid g && EqualsCore(this, g);

        public bool Equals(Guid g) => EqualsCore(this, g);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EqualsCore(in Guid left, in Guid right)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                return Unsafe.BitCast<Guid, Vector128<byte>>(left) == Unsafe.BitCast<Guid, Vector128<byte>>(right);
            }

            ref int rA = ref Unsafe.AsRef(in left._a);
            ref int rB = ref Unsafe.AsRef(in right._a);

            // Compare each element

            return rA == rB
                && Unsafe.Add(ref rA, 1) == Unsafe.Add(ref rB, 1)
                && Unsafe.Add(ref rA, 2) == Unsafe.Add(ref rB, 2)
                && Unsafe.Add(ref rA, 3) == Unsafe.Add(ref rB, 3);
        }

        private static int GetResult(uint me, uint them) => me < them ? -1 : 1;

        public int CompareTo(object? value)
        {
            if (value == null)
            {
                return 1;
            }
            if (value is not Guid other)
            {
                throw new ArgumentException(SR.Arg_MustBeGuid, nameof(value));
            }
            return CompareTo(other);
        }

        public int CompareTo(Guid value)
        {
            if (value._a != _a)
            {
                return GetResult((uint)_a, (uint)value._a);
            }

            if (value._b != _b)
            {
                return GetResult((uint)_b, (uint)value._b);
            }

            if (value._c != _c)
            {
                return GetResult((uint)_c, (uint)value._c);
            }

            if (value._d != _d)
            {
                return GetResult(_d, value._d);
            }

            if (value._e != _e)
            {
                return GetResult(_e, value._e);
            }

            if (value._f != _f)
            {
                return GetResult(_f, value._f);
            }

            if (value._g != _g)
            {
                return GetResult(_g, value._g);
            }

            if (value._h != _h)
            {
                return GetResult(_h, value._h);
            }

            if (value._i != _i)
            {
                return GetResult(_i, value._i);
            }

            if (value._j != _j)
            {
                return GetResult(_j, value._j);
            }

            if (value._k != _k)
            {
                return GetResult(_k, value._k);
            }

            return 0;
        }

        public static bool operator ==(Guid a, Guid b) => EqualsCore(a, b);

        public static bool operator !=(Guid a, Guid b) => !EqualsCore(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HexsToChars<TChar>(Span<TChar> destination, int a, int b) where TChar : unmanaged, IUtfChar<TChar>
        {
            destination[0] = TChar.CastFrom(HexConverter.ToCharLower(a >> 4));
            destination[1] = TChar.CastFrom(HexConverter.ToCharLower(a));

            destination[2] = TChar.CastFrom(HexConverter.ToCharLower(b >> 4));
            destination[3] = TChar.CastFrom(HexConverter.ToCharLower(b));
        }

        // Returns the guid in "registry" format.
        public override string ToString() => ToString("d", null);

        public string ToString([StringSyntax(StringSyntaxAttribute.GuidFormat)] string? format)
        {
            return ToString(format, null);
        }

        // IFormattable interface
        // We currently ignore provider
        public string ToString([StringSyntax(StringSyntaxAttribute.GuidFormat)] string? format, IFormatProvider? provider)
        {
            int guidSize;
            if (string.IsNullOrEmpty(format))
            {
                guidSize = 36;
            }
            else
            {
                // all acceptable format strings are of length 1
                if (format.Length != 1)
                {
                    ThrowBadGuidFormatSpecification();
                }

                switch (format[0] | 0x20)
                {
                    case 'd':
                        guidSize = 36;
                        break;

                    case 'n':
                        guidSize = 32;
                        break;

                    case 'b' or 'p':
                        guidSize = 38;
                        break;

                    case 'x':
                        guidSize = 68;
                        break;

                    default:
                        guidSize = 0;
                        ThrowBadGuidFormatSpecification();
                        break;
                };
            }

            string guidString = string.FastAllocateString(guidSize);

            bool result = TryFormatCore(new Span<char>(ref guidString.GetRawStringData(), guidString.Length), out int bytesWritten, format);
            Debug.Assert(result && bytesWritten == guidString.Length, "Formatting guid should have succeeded.");

            return guidString;
        }

        public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format = default) =>
            TryFormatCore(destination, out charsWritten, format);

        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format, IFormatProvider? provider) =>
            // Provider is ignored.
            TryFormatCore(destination, out charsWritten, format);

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format = default) =>
            TryFormatCore(utf8Destination, out bytesWritten, format);

        bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format, IFormatProvider? provider) =>
            // Provider is ignored.
            TryFormatCore(utf8Destination, out bytesWritten, format);

        // TryFormatCore accepts an `int flags` composed of:
        // - Lowest byte: required length
        // - Second byte: opening brace char, or 0 if no braces
        // - Third byte: closing brace char, or 0 if no braces
        // - Highest bit: 1 if use dashes, else 0
        internal const int TryFormatFlags_UseDashes = unchecked((int)0x80000000);
        internal const int TryFormatFlags_CurlyBraces = ('}' << 16) | ('{' << 8);
        internal const int TryFormatFlags_Parens = (')' << 16) | ('(' << 8);

        private bool TryFormatCore<TChar>(Span<TChar> destination, out int charsWritten, ReadOnlySpan<char> format) where TChar : unmanaged, IUtfChar<TChar>
        {
            int flags;

            if (format.Length == 0)
            {
                flags = 36 + TryFormatFlags_UseDashes;
            }
            else
            {
                if (format.Length != 1)
                {
                    ThrowBadGuidFormatSpecification();
                }

                switch (format[0] | 0x20)
                {
                    case 'd':
                        flags = 36 + TryFormatFlags_UseDashes;
                        break;

                    case 'p':
                        flags = 38 + TryFormatFlags_UseDashes + TryFormatFlags_Parens;
                        break;

                    case 'b':
                        flags = 38 + TryFormatFlags_UseDashes + TryFormatFlags_CurlyBraces;
                        break;

                    case 'n':
                        flags = 32;
                        break;

                    case 'x':
                        return TryFormatX(destination, out charsWritten);

                    default:
                        flags = 0;
                        ThrowBadGuidFormatSpecification();
                        break;
                }
            }

            return TryFormatCore(destination, out charsWritten, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // only used from two callers
        internal bool TryFormatCore<TChar>(Span<TChar> destination, out int charsWritten, int flags) where TChar : unmanaged, IUtfChar<TChar>
        {
            // The low byte of flags contains the required length, the second and third its optional
            // opening and closing brace, and the highest bit whether dashes are used.
            int length = (byte)flags;
            if (length > destination.Length)
            {
                charsWritten = 0;
                return false;
            }
            charsWritten = length;

            bool dashes = flags < 0;
            Span<TChar> body;
            if ((byte)(flags >> 8) != 0)
            {
                destination[0] = TChar.CastFrom((byte)(flags >> 8));
                destination[length - 1] = TChar.CastFrom((byte)(flags >> 16));
                body = destination.Slice(1, length - 2);
            }
            else
            {
                body = destination.Slice(0, length);
            }
            Debug.Assert(body.Length == (dashes ? 36 : 32));

            // [{|(]dddddddd[-]dddd[-]dddd[-]dddd[-]dddddddddddd[}|)]
            if ((Ssse3.IsSupported || AdvSimd.Arm64.IsSupported || PackedSimd.IsSupported) && BitConverter.IsLittleEndian)
            {
                WriteHexVector128(this, body, dashes);
            }
            else
            {
                WriteHexScalar(this, body, dashes);
            }

            return true;
        }

        /// <summary>Writes the 32 hex digits, and the dashes if asked for, of <paramref name="value"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CompExactlyDependsOn(typeof(Ssse3))]
        [CompExactlyDependsOn(typeof(AdvSimd.Arm64))]
        [CompExactlyDependsOn(typeof(PackedSimd))]
        private static void WriteHexVector128<TChar>(Guid value, Span<TChar> destination, bool dashes) where TChar : unmanaged, IUtfChar<TChar>
        {
            Debug.Assert(destination.Length == (dashes ? 36 : 32));

            (Vector128<byte> vecX, Vector128<byte> vecY, Vector128<byte> vecZ) = FormatGuidVector128Utf8(value, dashes);

            if (typeof(TChar) == typeof(byte))
            {
                Span<byte> utf8 = MemoryMarshal.Cast<TChar, byte>(destination);
                if (dashes)
                {
                    // The three vectors overlap and must be written in this order, "z" last:
                    // xxxxxxxxxxxxxxxx
                    //                     yyyyyyyyyyyyyyyy
                    //         zzzzzzzzzzzzzzzz
                    utf8 = utf8.Slice(0, 36);
                    vecX.CopyTo(utf8);
                    vecY.CopyTo(utf8.Slice(20));
                    vecZ.CopyTo(utf8.Slice(8));
                }
                else
                {
                    // xxxxxxxxxxxxxxxxyyyyyyyyyyyyyyyy
                    utf8 = utf8.Slice(0, 32);
                    vecX.CopyTo(utf8);
                    vecY.CopyTo(utf8.Slice(16));
                }
            }
            else
            {
                // Expand to UTF-16, preserving the same store order.
                Span<ushort> utf16 = MemoryMarshal.Cast<TChar, ushort>(destination);

                // The result is the same either way; Avx2 just gets there with fewer instructions.
#pragma warning disable IntrinsicsInSystemPrivateCoreLibAttributeNotSpecificEnough
                if (Avx2.IsSupported)
                {
                    // A single vpmovzxbw widens all 16 bytes at once, which both halves the widening
                    // work and cuts the number of stores from five to three.
                    Vector256<ushort> x = Avx2.ConvertToVector256Int16(vecX).AsUInt16();
                    Vector256<ushort> y = Avx2.ConvertToVector256Int16(vecY).AsUInt16();
                    if (dashes)
                    {
                        utf16 = utf16.Slice(0, 36);
                        x.CopyTo(utf16);
                        y.CopyTo(utf16.Slice(20));
                        Avx2.ConvertToVector256Int16(vecZ).AsUInt16().CopyTo(utf16.Slice(8));
                    }
                    else
                    {
                        utf16 = utf16.Slice(0, 32);
                        x.CopyTo(utf16);
                        y.CopyTo(utf16.Slice(16));
                    }
                }
                else
#pragma warning restore IntrinsicsInSystemPrivateCoreLibAttributeNotSpecificEnough
                {
                    (Vector128<ushort> x0, Vector128<ushort> x1) = Vector128.Widen(vecX);
                    (Vector128<ushort> y0, Vector128<ushort> y1) = Vector128.Widen(vecY);
                    if (dashes)
                    {
                        (Vector128<ushort> z0, Vector128<ushort> z1) = Vector128.Widen(vecZ);

                        utf16 = utf16.Slice(0, 36);
                        x0.CopyTo(utf16);
                        y0.CopyTo(utf16.Slice(20));
                        y1.CopyTo(utf16.Slice(28));
                        z0.CopyTo(utf16.Slice(8));  // overlaps x1
                        z1.CopyTo(utf16.Slice(16)); // overlaps y0
                    }
                    else
                    {
                        utf16 = utf16.Slice(0, 32);
                        x0.CopyTo(utf16);
                        x1.CopyTo(utf16.Slice(8));
                        y0.CopyTo(utf16.Slice(16));
                        y1.CopyTo(utf16.Slice(24));
                    }
                }
            }
        }

        /// <summary>Non-vectorized equivalent of <see cref="WriteHexVector128"/>.</summary>
        private static void WriteHexScalar<TChar>(Guid value, Span<TChar> destination, bool dashes) where TChar : unmanaged, IUtfChar<TChar>
        {
            Debug.Assert(destination.Length == (dashes ? 36 : 32));

            int pos = 0;
            HexsToChars(destination.Slice(pos), value._a >> 24, value._a >> 16); pos += 4;
            HexsToChars(destination.Slice(pos), value._a >> 8, value._a); pos += 4;
            if (dashes)
            {
                destination[pos++] = TChar.CastFrom('-');
            }
            HexsToChars(destination.Slice(pos), value._b >> 8, value._b); pos += 4;
            if (dashes)
            {
                destination[pos++] = TChar.CastFrom('-');
            }
            HexsToChars(destination.Slice(pos), value._c >> 8, value._c); pos += 4;
            if (dashes)
            {
                destination[pos++] = TChar.CastFrom('-');
            }
            HexsToChars(destination.Slice(pos), value._d, value._e); pos += 4;
            if (dashes)
            {
                destination[pos++] = TChar.CastFrom('-');
            }
            HexsToChars(destination.Slice(pos), value._f, value._g); pos += 4;
            HexsToChars(destination.Slice(pos), value._h, value._i); pos += 4;
            HexsToChars(destination.Slice(pos), value._j, value._k); pos += 4;

            Debug.Assert(pos == destination.Length);
        }

        private bool TryFormatX<TChar>(Span<TChar> dest, out int charsWritten) where TChar : unmanaged, IUtfChar<TChar>
        {
            if (dest.Length < 68)
            {
                charsWritten = 0;
                return false;
            }

            // {0xdddddddd,0xdddd,0xdddd,{0xdd,0xdd,0xdd,0xdd,0xdd,0xdd,0xdd,0xdd}}
            dest[0]  = TChar.CastFrom('{');
            dest[1]  = TChar.CastFrom('0');
            dest[2]  = TChar.CastFrom('x');
            dest[3]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 28));
            dest[4]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 24));
            dest[5]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 20));
            dest[6]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 16));
            dest[7]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 12));
            dest[8]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 8));
            dest[9]  = TChar.CastFrom(HexConverter.ToCharLower(_a >> 4));
            dest[10] = TChar.CastFrom(HexConverter.ToCharLower(_a));
            dest[11] = TChar.CastFrom(',');
            dest[12] = TChar.CastFrom('0');
            dest[13] = TChar.CastFrom('x');
            dest[14] = TChar.CastFrom(HexConverter.ToCharLower(_b >> 12));
            dest[15] = TChar.CastFrom(HexConverter.ToCharLower(_b >> 8));
            dest[16] = TChar.CastFrom(HexConverter.ToCharLower(_b >> 4));
            dest[17] = TChar.CastFrom(HexConverter.ToCharLower(_b));
            dest[18] = TChar.CastFrom(',');
            dest[19] = TChar.CastFrom('0');
            dest[20] = TChar.CastFrom('x');
            dest[21] = TChar.CastFrom(HexConverter.ToCharLower(_c >> 12));
            dest[22] = TChar.CastFrom(HexConverter.ToCharLower(_c >> 8));
            dest[23] = TChar.CastFrom(HexConverter.ToCharLower(_c >> 4));
            dest[24] = TChar.CastFrom(HexConverter.ToCharLower(_c));
            dest[25] = TChar.CastFrom(',');
            dest[26] = TChar.CastFrom('{');
            WriteHex(dest, 27, _d);
            WriteHex(dest, 32, _e);
            WriteHex(dest, 37, _f);
            WriteHex(dest, 42, _g);
            WriteHex(dest, 47, _h);
            WriteHex(dest, 52, _i);
            WriteHex(dest, 57, _j);
            WriteHex(dest, 62, _k, appendComma: false);
            dest[66] = TChar.CastFrom('}');
            dest[67] = TChar.CastFrom('}');
            charsWritten = 68;
            return true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void WriteHex(Span<TChar> dest, int offset, int val, bool appendComma = true)
            {
                dest[offset + 0] = TChar.CastFrom('0');
                dest[offset + 1] = TChar.CastFrom('x');
                dest[offset + 2] = TChar.CastFrom(HexConverter.ToCharLower(val >> 4));
                dest[offset + 3] = TChar.CastFrom(HexConverter.ToCharLower(val));
                if (appendComma)
                {
                    dest[offset + 4] = TChar.CastFrom(',');
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CompExactlyDependsOn(typeof(Ssse3))]
        [CompExactlyDependsOn(typeof(AdvSimd.Arm64))]
        [CompExactlyDependsOn(typeof(PackedSimd))]
        private static (Vector128<byte>, Vector128<byte>, Vector128<byte>) FormatGuidVector128Utf8(Guid value, bool useDashes)
        {
            Debug.Assert((Ssse3.IsSupported || AdvSimd.Arm64.IsSupported || PackedSimd.IsSupported) && BitConverter.IsLittleEndian);
            // Vectorized implementation for D, N, P and B formats:
            // [{|(]dddddddd[-]dddd[-]dddd[-]dddd[-]dddddddddddd[}|)]

            Vector128<byte> hexMap = Vector128.Create(
                (byte)'0', (byte)'1', (byte)'2', (byte)'3',
                (byte)'4', (byte)'5', (byte)'6', (byte)'7',
                (byte)'8', (byte)'9', (byte)'a', (byte)'b',
                (byte)'c', (byte)'d', (byte)'e', (byte)'f');

            Vector128<byte> srcVec = Unsafe.BitCast<Guid, Vector128<byte>>(value);
            (Vector128<byte> hexLow, Vector128<byte> hexHigh) =
                HexConverter.AsciiToHexVector128(srcVec, hexMap);

            // because of Guid's layout (int _a, short _b, _c, <8 byte fields>)
            // we have to shuffle some bytes for _a, _b and _c
            hexLow = Vector128.Shuffle(hexLow.AsInt16(), Vector128.Create(3, 2, 1, 0, 5, 4, 7, 6)).AsByte();

            if (useDashes)
            {
                // We divide 32 bytes into 3 x Vector128<byte>:
                //
                // ________-____-____-____-____________
                // xxxxxxxxxxxxxxxx
                //                     yyyyyyyyyyyyyyyy
                //         zzzzzzzzzzzzzzzz
                //
                // Vector "x" - just one dash, shift all elements after it.
                Vector128<byte> vecX = Vector128.Shuffle(hexLow,
                    Vector128.Create(0x706050403020100, 0xD0CFF0B0A0908FF).AsByte());

                // Vector "y" - same here.
                Vector128<byte> vecY = Vector128.Shuffle(hexHigh,
                    Vector128.Create(0x7060504FF030201, 0xF0E0D0C0B0A0908).AsByte());

                // Vector "z" - we need to merge some elements of hexLow with hexHigh and add 4 dashes.
                Vector128<byte> vecZ;
                Vector128<byte> dashesMask = Vector128.Create(0x00002D000000002D, 0x2D000000002D0000).AsByte();
                if (AdvSimd.Arm64.IsSupported)
                {
                    // Arm64 allows shuffling values using a 32-byte wide look-up table consisting of two 128-bit registers.
                    // Each byte in the second arg represents a value between 0 to 31 that acts as an index in the look-up table.
                    // Now we can create a "z" vector by selecting 12 values starting from the 9th element (index 0x08) and
                    // leaving gaps for dashes. Thus, the wider look-up table allows combining two shuffles, as used in the
                    // generic else-case, into a single instruction on Arm64.
                    Vector128<byte> mid = AdvSimd.Arm64.VectorTableLookup((hexLow, hexHigh),
                        Vector128.Create(0x0D0CFF0B0A0908FF, 0xFF13121110FF0F0E).AsByte());
                    vecZ = (mid | dashesMask);
                }
                else
                {
                    Vector128<byte> mid1 = Vector128.Shuffle(hexLow,
                        Vector128.Create(0x0D0CFF0B0A0908FF, 0xFFFFFFFFFFFF0F0E).AsByte());
                    Vector128<byte> mid2 = Vector128.Shuffle(hexHigh,
                        Vector128.Create(0xFFFFFFFFFFFFFFFF, 0xFF03020100FFFFFF).AsByte());
                    vecZ = (mid1 | mid2 | dashesMask);
                }

                return (vecX, vecY, vecZ);
            }

            // N format - no dashes.
            return (hexLow, hexHigh, default);
        }

        //
        // IComparisonOperators
        //

        /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
        public static bool operator <(Guid left, Guid right)
        {
            if (left._a != right._a)
            {
                return (uint)left._a < (uint)right._a;
            }

            if (left._b != right._b)
            {
                return (uint)left._b < (uint)right._b;
            }

            if (left._c != right._c)
            {
                return (uint)left._c < (uint)right._c;
            }

            if (left._d != right._d)
            {
                return left._d < right._d;
            }

            if (left._e != right._e)
            {
                return left._e < right._e;
            }

            if (left._f != right._f)
            {
                return left._f < right._f;
            }

            if (left._g != right._g)
            {
                return left._g < right._g;
            }

            if (left._h != right._h)
            {
                return left._h < right._h;
            }

            if (left._i != right._i)
            {
                return left._i < right._i;
            }

            if (left._j != right._j)
            {
                return left._j < right._j;
            }

            if (left._k != right._k)
            {
                return left._k < right._k;
            }

            return false;
        }

        /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
        public static bool operator <=(Guid left, Guid right)
        {
            if (left._a != right._a)
            {
                return (uint)left._a < (uint)right._a;
            }

            if (left._b != right._b)
            {
                return (uint)left._b < (uint)right._b;
            }

            if (left._c != right._c)
            {
                return (uint)left._c < (uint)right._c;
            }

            if (left._d != right._d)
            {
                return left._d < right._d;
            }

            if (left._e != right._e)
            {
                return left._e < right._e;
            }

            if (left._f != right._f)
            {
                return left._f < right._f;
            }

            if (left._g != right._g)
            {
                return left._g < right._g;
            }

            if (left._h != right._h)
            {
                return left._h < right._h;
            }

            if (left._i != right._i)
            {
                return left._i < right._i;
            }

            if (left._j != right._j)
            {
                return left._j < right._j;
            }

            if (left._k != right._k)
            {
                return left._k < right._k;
            }

            return true;
        }

        /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
        public static bool operator >(Guid left, Guid right)
        {
            if (left._a != right._a)
            {
                return (uint)left._a > (uint)right._a;
            }

            if (left._b != right._b)
            {
                return (uint)left._b > (uint)right._b;
            }

            if (left._c != right._c)
            {
                return (uint)left._c > (uint)right._c;
            }

            if (left._d != right._d)
            {
                return left._d > right._d;
            }

            if (left._e != right._e)
            {
                return left._e > right._e;
            }

            if (left._f != right._f)
            {
                return left._f > right._f;
            }

            if (left._g != right._g)
            {
                return left._g > right._g;
            }

            if (left._h != right._h)
            {
                return left._h > right._h;
            }

            if (left._i != right._i)
            {
                return left._i > right._i;
            }

            if (left._j != right._j)
            {
                return left._j > right._j;
            }

            if (left._k != right._k)
            {
                return left._k > right._k;
            }

            return false;
        }

        /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
        public static bool operator >=(Guid left, Guid right)
        {
            if (left._a != right._a)
            {
                return (uint)left._a > (uint)right._a;
            }

            if (left._b != right._b)
            {
                return (uint)left._b > (uint)right._b;
            }

            if (left._c != right._c)
            {
                return (uint)left._c > (uint)right._c;
            }

            if (left._d != right._d)
            {
                return left._d > right._d;
            }

            if (left._e != right._e)
            {
                return left._e > right._e;
            }

            if (left._f != right._f)
            {
                return left._f > right._f;
            }

            if (left._g != right._g)
            {
                return left._g > right._g;
            }

            if (left._h != right._h)
            {
                return left._h > right._h;
            }

            if (left._i != right._i)
            {
                return left._i > right._i;
            }

            if (left._j != right._j)
            {
                return left._j > right._j;
            }

            if (left._k != right._k)
            {
                return left._k > right._k;
            }

            return true;
        }

        //
        // IParsable
        //

        /// <inheritdoc cref="IParsable{TSelf}.Parse(string, IFormatProvider?)" />
        public static Guid Parse(string s, IFormatProvider? provider) => Parse(s);

        /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)" />
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Guid result) => TryParse(s, out result);

        //
        // ISpanParsable
        //

        /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)" />
        public static Guid Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);

        /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)" />
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Guid result) => TryParse(s, out result);

        [DoesNotReturn]
        private static void ThrowBadGuidFormatSpecification() =>
            throw new FormatException(SR.Format_InvalidGuidFormatSpecification);

        //
        // IUtf8SpanParsable
        //

        /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.Parse(ReadOnlySpan{byte}, IFormatProvider?)" />
        public static Guid Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);

        /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.TryParse(ReadOnlySpan{byte}, IFormatProvider?, out TSelf)" />
        public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Guid result) => TryParse(utf8Text, out result);
    }
}
