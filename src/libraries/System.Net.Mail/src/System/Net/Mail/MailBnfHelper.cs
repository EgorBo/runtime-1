// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace System.Net.Mime
{
    internal static class MailBnfHelper
    {
        private static readonly SearchValues<char> s_charactersAllowedInHeaderNames =
            // ftext = %d33-57 / %d59-126
            SearchValues.Create("!\"#$%&'()*+,-./0123456789;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

        private static readonly SearchValues<char> s_charactersAllowedInTokens =
            // ttext = %d33-126 except '()<>@,;:\"/[]?='
            SearchValues.Create("!#$%&'*+-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ^_`abcdefghijklmnopqrstuvwxyz{|}~");

        internal const char Quote = '\"';
        internal const char Space = ' ';
        internal const char Tab = '\t';
        internal const char CR = '\r';
        internal const char LF = '\n';
        internal const char StartComment = '(';
        internal const char EndComment = ')';
        internal const char Backslash = '\\';
        internal const char At = '@';
        internal const char EndAngleBracket = '>';
        internal const char StartAngleBracket = '<';
        internal const char StartSquareBracket = '[';
        internal const char EndSquareBracket = ']';
        internal const char Comma = ',';
        internal const char Dot = '.';
        internal const string ConsecutiveDots = "..";

        // NOTE: See RFC 2822 for more detail.  Each table is indexed by ASCII value and only
        // those values which are allowed in that particular set are true.  The numbers
        // annotating each definition below are the range of ASCII values which are allowed in that definition.

        // characters allowed in atoms
        // atext = ALPHA / DIGIT / "!" / "#" / "$" / "%" / "&" / "'" / "*" / "+" / "-" / "/" / "=" / "?" / "^" / "_" / "`" / "{" / "|" / "}" / "~"
        internal static ReadOnlySpan<bool> Atext =>
        [
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 0x00-0x0F
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, // 0x10-0x1F
            false, true, false, true, true, true, true, true, false, false, true, true, false, true, false, true, // 0x20-0x2F
            true, true, true, true, true, true, true, true, true, true, false, false, false, true, false, true, // 0x30-0x3F
            false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x40-0x4F
            true, true, true, true, true, true, true, true, true, true, true, false, false, false, true, true, // 0x50-0x5F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x60-0x6F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, false // 0x70-0x7F
        ];

        // characters allowed in quoted strings (not including Unicode)
        // fqtext = %d1-9 / %d11 / %d12 / %d14-33 / %d35-91 / %d93-127
        internal static ReadOnlySpan<bool> Qtext =>
        [
            false, true, true, true, true, true, true, true, true, true, false, true, true, false, true, true, // 0x00-0x0F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x10-0x1F
            true, true, false, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x20-0x2F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x30-0x3F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x40-0x4F
            true, true, true, true, true, true, true, true, true, true, true, true, false, true, true, true, // 0x50-0x5F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x60-0x6F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true // 0x70-0x7F
        ];

        // characters allowed in domain literals
        // fdtext = %d1-8 / %d11 / %d12 / %d14-31 / %d33-90 / %d94-127
        internal static ReadOnlySpan<bool> Dtext =>
        [
            false, true, true, true, true, true, true, true, true, false, false, true, true, false, true, true, // 0x00-0x0F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x10-0x1F
            false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x20-0x2F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x30-0x3F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x40-0x4F
            true, true, true, true, true, true, true, true, true, true, true, false, false, false, true, true, // 0x50-0x5F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x60-0x6F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true // 0x70-0x7F
        ];

        // characters allowed inside of comments
        // ctext- %d1-8 / %d11 / %d12 / %d14-31 / %33-39 / %42-91 / %93-127
        internal static ReadOnlySpan<bool> Ctext =>
        [
            false, true, true, true, true, true, true, true, true, false, false, true, true, false, true, true, // 0x00-0x0F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x10-0x1F
            false, true, true, true, true, true, true, true, false, false, true, true, true, true, true, true, // 0x20-0x2F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x30-0x3F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x40-0x4F
            true, true, true, true, true, true, true, true, true, true, true, true, false, true, true, true, // 0x50-0x5F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, // 0x60-0x6F
            true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true // 0x70-0x7F
        ];

        internal static bool SkipCFWS(string data, ref int offset)
        {
            int comments = 0;
            for (; offset < data.Length; offset++)
            {
                if (data[offset] > 127)
                    throw new FormatException(SR.Format(SR.MailHeaderFieldInvalidCharacter, data[offset]));
                else if (data[offset] == '\\' && comments > 0)
                    offset += 2;
                else if (data[offset] == '(')
                    comments++;
                else if (data[offset] == ')')
                    comments--;
                else if (data[offset] != ' ' && data[offset] != '\t' && comments == 0)
                    return true;

                if (comments < 0)
                {
                    throw new FormatException(SR.Format(SR.MailHeaderFieldInvalidCharacter, data[offset]));
                }
            }

            //returns false if end of string
            return false;
        }

        internal static void ValidateHeaderName(string data)
        {
            if (data.Length == 0 || data.AsSpan().ContainsAnyExcept(s_charactersAllowedInHeaderNames))
            {
                throw new FormatException(SR.InvalidHeaderName);
            }
        }

        internal static string? ReadQuotedString(string data, ref int offset, StringBuilder? builder)
        {
            return ReadQuotedString(data, ref offset, builder, false, false);
        }

        internal static string? ReadQuotedString(string data, ref int offset, StringBuilder? builder, bool doesntRequireQuotes, bool permitUnicodeInDisplayName)
        {
            // assume first char is the opening quote
            if (!doesntRequireQuotes)
            {
                ++offset;
            }
            int start = offset;
            StringBuilder localBuilder = builder ?? new StringBuilder();
            for (; offset < data.Length; offset++)
            {
                if (data[offset] == '\\')
                {
                    localBuilder.Append(data, start, offset - start);
                    start = ++offset;
                }
                else if (data[offset] == '"')
                {
                    localBuilder.Append(data, start, offset - start);
                    offset++;
                    return (builder != null ? null : localBuilder.ToString());
                }
                else if (data[offset] == '=' &&
                    data.Length > offset + 3 &&
                    data[offset + 1] == '\r' &&
                    data[offset + 2] == '\n' &&
                    (data[offset + 3] == ' ' || data[offset + 3] == '\t'))
                {
                    //it's a soft crlf so it's ok
                    offset += 3;
                }
                else if (permitUnicodeInDisplayName)
                {
                    //if data contains Unicode and Unicode is permitted, then
                    //it is valid in a quoted string in a header.
                    if (Ascii.IsValid(data[offset]) && !Qtext[data[offset]])
                        throw new FormatException(SR.Format(SR.MailHeaderFieldInvalidCharacter, data[offset]));
                }
                //not permitting Unicode, in which case Unicode is a formatting error
                else if (!Ascii.IsValid(data[offset]) || !Qtext[data[offset]])
                {
                    throw new FormatException(SR.Format(SR.MailHeaderFieldInvalidCharacter, data[offset]));
                }
            }
            if (doesntRequireQuotes)
            {
                localBuilder.Append(data, start, offset - start);
                return (builder != null ? null : localBuilder.ToString());
            }
            throw new FormatException(SR.MailHeaderFieldMalformedHeader);
        }

        internal static string? ReadParameterAttribute(string data, ref int offset)
        {
            if (!SkipCFWS(data, ref offset))
                return null; //

            return ReadToken(data, ref offset);
        }

        internal static string ReadToken(string data, ref int offset)
        {
            int start = offset;

            if (start >= data.Length)
            {
                return string.Empty;
            }

            ReadOnlySpan<char> span = data.AsSpan(start);
            int i = span.IndexOfAnyExcept(s_charactersAllowedInTokens);
            if (i >= 0)
            {
                if (i == 0 || !Ascii.IsValid(span[i]))
                {
                    throw new FormatException(SR.Format(SR.MailHeaderFieldInvalidCharacter, span[i]));
                }
            }
            else
            {
                i = span.Length;
            }

            offset += i;
            return data.Substring(start, i);
        }

        private static readonly string?[] s_months = new string?[] { null, "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        internal static string? GetDateTimeString(DateTime value, StringBuilder? builder)
        {
            StringBuilder localBuilder = builder ?? new StringBuilder();
            localBuilder.Append(value.Day);
            localBuilder.Append(' ');
            localBuilder.Append(s_months[value.Month]);
            localBuilder.Append(' ');
            localBuilder.Append(value.Year);
            localBuilder.Append(' ');
            if (value.Hour <= 9)
            {
                localBuilder.Append('0');
            }
            localBuilder.Append(value.Hour);
            localBuilder.Append(':');
            if (value.Minute <= 9)
            {
                localBuilder.Append('0');
            }
            localBuilder.Append(value.Minute);
            localBuilder.Append(':');
            if (value.Second <= 9)
            {
                localBuilder.Append('0');
            }
            localBuilder.Append(value.Second);

            string offset = TimeZoneInfo.Local.GetUtcOffset(value).ToString();
            if (offset[0] != '-')
            {
                localBuilder.Append(" +");
            }
            else
            {
                localBuilder.Append(' ');
            }

            string[] offsetFields = offset.Split(':');
            localBuilder.Append(offsetFields[0]);
            localBuilder.Append(offsetFields[1]);
            return (builder != null ? null : localBuilder.ToString());
        }

        internal static void GetTokenOrQuotedString(string data, StringBuilder builder, bool allowUnicode)
        {
            int offset = 0, start = 0;
            for (; offset < data.Length; offset++)
            {
                if (CheckForUnicode(data[offset], allowUnicode))
                {
                    continue;
                }

                if (!s_charactersAllowedInTokens.Contains(data[offset]) || data[offset] == ' ')
                {
                    builder.Append('"');
                    for (; offset < data.Length; offset++)
                    {
                        if (CheckForUnicode(data[offset], allowUnicode))
                        {
                            continue;
                        }
                        else if (IsFWSAt(data, offset)) // Allow FWS == "\r\n "
                        {
                            // No-op, skip these three chars
                            offset += 2;
                        }
                        else if (!Qtext[data[offset]])
                        {
                            builder.Append(data, start, offset - start);
                            builder.Append('\\');
                            start = offset;
                        }
                    }
                    builder.Append(data, start, offset - start);
                    builder.Append('"');
                    return;
                }
            }

            //always a quoted string if it was empty.
            if (data.Length == 0)
            {
                builder.Append("\"\"");
            }
            // Token, no quotes needed
            builder.Append(data);
        }

        private static bool CheckForUnicode(char ch, bool allowUnicode)
        {
            if (Ascii.IsValid(ch))
            {
                return false;
            }

            if (!allowUnicode)
            {
                throw new FormatException(SR.Format(SR.MailHeaderFieldInvalidCharacter, ch));
            }
            return true;
        }

        internal static bool IsAllowedWhiteSpace(char c) =>
            // all allowed whitespace characters
            c == Tab || c == Space || c == CR || c == LF;

        internal static bool HasCROrLF(string data) =>
            data.AsSpan().ContainsAny(CR, LF);

        // Is there a FWS ("\r\n " or "\r\n\t") starting at the given index?
        internal static bool IsFWSAt(string data, int index)
        {
            Debug.Assert(index >= 0);
            Debug.Assert(index < data.Length);

            return (data[index] == MailBnfHelper.CR
                    && index + 2 < data.Length
                    && data[index + 1] == MailBnfHelper.LF
                    && (data[index + 2] == MailBnfHelper.Space
                        || data[index + 2] == MailBnfHelper.Tab));
        }
    }
}
