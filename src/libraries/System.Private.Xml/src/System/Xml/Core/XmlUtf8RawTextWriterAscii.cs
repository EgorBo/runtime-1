// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace System.Xml
{
    internal sealed class XmlUtf8RawTextWriterAscii : XmlUtf8RawTextWriter
    {
        private static readonly SearchValues<char> s_asciiElementTextChars =
            SearchValues.Create(" !\"#$%'()*+,-./0123456789:;=?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

        public XmlUtf8RawTextWriterAscii(Stream stream, XmlWriterSettings settings)
            : base(stream, settings)
        {
        }

        public override unsafe void WriteString(string? text)
        {
            Debug.Assert(text != null);

            if (_inAttributeValue || text.Length == 0 || !IsSafeAscii(text[0]))
            {
                base.WriteString(text);
                return;
            }

            int unsafeIndex = text.AsSpan(1).IndexOfAnyExcept(s_asciiElementTextChars);
            unsafeIndex = unsafeIndex < 0 ? text.Length : unsafeIndex + 1;

            int index = 0;
            while (index < unsafeIndex)
            {
                if (_bufPos >= _bufLen)
                {
                    FlushBuffer();
                }

                index = WriteAsciiTextBlock(text, index, unsafeIndex);
            }

            if (unsafeIndex < text.Length)
            {
                fixed (char* pSrc = text)
                {
                    WriteElementTextBlock(pSrc + unsafeIndex, pSrc + text.Length);
                }
            }
            else
            {
                CompleteWriteString();
            }
        }

        public override Task WriteStringAsync(string? text)
        {
            CheckAsyncCall();
            Debug.Assert(text != null);

            if (_inAttributeValue || text.Length == 0 || !IsSafeAscii(text[0]))
            {
                return base.WriteStringAsync(text);
            }

            int unsafeIndex = text.AsSpan(1).IndexOfAnyExcept(s_asciiElementTextChars);
            unsafeIndex = unsafeIndex < 0 ? text.Length : unsafeIndex + 1;

            int index = _bufPos < _bufLen ? WriteAsciiTextBlock(text, 0, unsafeIndex) : 0;
            if (index < unsafeIndex)
            {
                return WriteAsciiTextAsync(text, index, unsafeIndex);
            }

            if (unsafeIndex < text.Length)
            {
                return WriteElementTextAsync(text, unsafeIndex);
            }

            CompleteWriteString();
            return Task.CompletedTask;
        }

        private static bool IsSafeAscii(char value) =>
            (uint)(value - ' ') <= '~' - ' ' && value is not '&' and not '<' and not '>';

        private int WriteAsciiTextBlock(string text, int index, int endIndex)
        {
            int count = Math.Min(endIndex - index, _bufLen - _bufPos);
            OperationStatus status = Ascii.FromUtf16(
                text.AsSpan(index, count),
                _bufBytes.AsSpan(_bufPos, count),
                out int bytesWritten);

            Debug.Assert(status == OperationStatus.Done);
            Debug.Assert(bytesWritten == count);

            _bufPos += bytesWritten;
            return index + count;
        }

        private async Task WriteAsciiTextAsync(string text, int index, int unsafeIndex)
        {
            do
            {
                await FlushBufferAsync().ConfigureAwait(false);
                index = WriteAsciiTextBlock(text, index, unsafeIndex);
            }
            while (index < unsafeIndex);

            if (unsafeIndex < text.Length)
            {
                await WriteElementTextAsync(text, unsafeIndex).ConfigureAwait(false);
            }
            else
            {
                CompleteWriteString();
            }
        }

        private Task WriteElementTextAsync(string text, int index)
        {
            int count = text.Length - index;
            int writeLength = WriteElementTextBlockNoFlush(text, index, count, out bool needWriteNewLine);
            if (writeLength < 0)
            {
                Debug.Assert(!needWriteNewLine);
                return Task.CompletedTask;
            }

            return WriteElementTextAsync(text, index + writeLength, count - writeLength, needWriteNewLine);
        }

        private async Task WriteElementTextAsync(string text, int index, int count, bool needWriteNewLine)
        {
            while (true)
            {
                if (needWriteNewLine)
                {
                    await RawTextAsync(_newLineChars).ConfigureAwait(false);
                    index++;
                    count--;
                }
                else
                {
                    await FlushBufferAsync().ConfigureAwait(false);
                }

                int writeLength = WriteElementTextBlockNoFlush(text, index, count, out needWriteNewLine);
                if (writeLength < 0)
                {
                    Debug.Assert(!needWriteNewLine);
                    return;
                }

                index += writeLength;
                count -= writeLength;
            }
        }

        private void CompleteWriteString()
        {
            _textPos = _bufPos;
            _contentPos = 0;
        }
    }
}
