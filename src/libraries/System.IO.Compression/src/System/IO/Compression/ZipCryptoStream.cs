// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Compression
{
    internal sealed partial class ZipCryptoStream : Stream
    {
        private const int EncryptionBufferSize = 4096;

        private readonly bool _encrypting;
        private readonly Stream _base;
        private readonly bool _leaveOpen;
        private bool _headerWritten;
        private bool _disposed;
        private readonly ushort _verifierLow2Bytes;       // (DOS time low word when streaming)
        private readonly uint? _crc32ForHeader;           // (CRC-based header when not streaming)

        private uint _key0;
        private uint _key1;
        private uint _key2;

        // Reusable work buffer for write operations, lazily allocated on first write
        private byte[]? _writeWorkBuffer;

        // Standard reflected CRC-32 lookup table (polynomial 0xEDB88320).
        private static ReadOnlySpan<uint> Crc2Table =>
        [
            0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
            0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
            0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
            0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
            0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
            0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
            0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
            0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
            0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
            0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
            0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
            0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
            0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
            0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
            0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
            0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
            0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
            0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
            0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
            0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
            0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
            0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
            0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
            0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
            0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
            0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
            0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
            0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
            0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
            0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
            0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
            0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D,
        ];

        private static uint Crc32Update(uint crc, byte b) => Crc2Table[(int)((crc ^ b) & 0xFF)] ^ (crc >> 8);

        // Private decryption constructor - use Create/CreateAsync factory methods instead.
        // Keys must already be validated before calling this constructor.
        private ZipCryptoStream(Stream baseStream, uint key0, uint key1, uint key2, bool leaveOpen = false)
        {
            _base = baseStream;
            _key0 = key0;
            _key1 = key1;
            _key2 = key2;
            _encrypting = false;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a ZipCryptoStream for decryption. Reads and validates the 12-byte header synchronously.
        /// </summary>
        internal static ZipCryptoStream Create(Stream baseStream, ZipCryptoKeys keys, byte expectedCheckByte, bool encrypting, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(baseStream);
            Debug.Assert(!encrypting, "Use the overload with passwordVerifierLow2Bytes for encryption.");

            (uint key0, uint key1, uint key2) = ReadAndValidateHeaderCore(isAsync: false, baseStream, keys, expectedCheckByte, CancellationToken.None).GetAwaiter().GetResult();
            return new ZipCryptoStream(baseStream, key0, key1, key2, leaveOpen);
        }

        /// <summary>
        /// Creates a ZipCryptoStream for decryption. Reads and validates the 12-byte header asynchronously.
        /// </summary>
        internal static async Task<ZipCryptoStream> CreateAsync(Stream baseStream, ZipCryptoKeys keys, byte expectedCheckByte, bool encrypting, CancellationToken cancellationToken = default, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(baseStream);
            Debug.Assert(!encrypting, "Use the overload with passwordVerifierLow2Bytes for encryption.");

            (uint key0, uint key1, uint key2) = await ReadAndValidateHeaderCore(isAsync: true, baseStream, keys, expectedCheckByte, cancellationToken).ConfigureAwait(false);
            return new ZipCryptoStream(baseStream, key0, key1, key2, leaveOpen);
        }

        /// <summary>
        /// Creates a ZipCryptoStream for encryption. Only synchronous creation is needed since no I/O is performed here.
        /// </summary>
        internal static ZipCryptoStream Create(Stream baseStream,
                                             ZipCryptoKeys keys,
                                             ushort passwordVerifierLow2Bytes,
                                             bool encrypting,
                                             uint? crc32 = null,
                                             bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(baseStream);
            Debug.Assert(encrypting, "Use the overload with expectedCheckByte for decryption.");

            return new ZipCryptoStream(baseStream, keys, passwordVerifierLow2Bytes, crc32, leaveOpen);
        }

        // Encryption constructor
        private ZipCryptoStream(Stream baseStream,
                               ZipCryptoKeys keys,
                               ushort passwordVerifierLow2Bytes,
                               uint? crc32,
                               bool leaveOpen)
        {
            _base = baseStream;
            _encrypting = true;
            _leaveOpen = leaveOpen;
            _verifierLow2Bytes = passwordVerifierLow2Bytes;
            _crc32ForHeader = crc32;
            _key0 = keys.Key0;
            _key1 = keys.Key1;
            _key2 = keys.Key2;
        }

        // Creates the persisted key material from a password.
        // Returns a struct of 3 integers to keep the key off the heap.
        internal static ZipCryptoKeys CreateKey(ReadOnlySpan<char> password)
        {
            // Initialize keys with standard ZipCrypto initial values
            uint key0 = 305419896;
            uint key1 = 591751049;
            uint key2 = 878082192;

            // Feed the password's UTF-8 bytes into the key schedule. UTF-8 (rather than ASCII)
            // preserves non-ASCII passwords; interop with tools that assume a different code page
            // is documented as a caveat.
            byte[] passwordBytes = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(password.Length));
            try
            {
                int byteCount = Encoding.UTF8.GetBytes(password, passwordBytes);
                for (int i = 0; i < byteCount; i++)
                {
                    UpdateKeys(ref key0, ref key1, ref key2, passwordBytes[i]);
                }
            }
            finally
            {
                ClearSensitiveData(passwordBytes);
                ArrayPool<byte>.Shared.Return(passwordBytes);
            }

            return new ZipCryptoKeys(key0, key1, key2);
        }

        private void CalculateHeader(Span<byte> header)
        {
            Debug.Assert(header.Length == 12);

            // bytes 0..9 random
            FillHeaderRandomBytes(header);

            // bytes 10..11 verifier
            if (_crc32ForHeader.HasValue)
            {
                uint crc = _crc32ForHeader.Value;
                BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(10), (ushort)(crc >> 16));
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(10), _verifierLow2Bytes);
            }

            // encrypt in place
            for (int i = 0; i < header.Length; i++)
            {
                byte p = header[i];
                byte ks = DecryptByte(_key2);
                header[i] = (byte)(p ^ ks);

                // keys updated with PLAINTEXT per ZIP spec
                UpdateKeys(ref _key0, ref _key1, ref _key2, p);
            }
        }

        private unsafe void EnsureHeader()
        {
            if (!_encrypting || _headerWritten)
            {
                return;
            }

            Span<byte> header = stackalloc byte[12];
            CalculateHeader(header);
            _base.Write(header);
            _headerWritten = true;
        }

        private async ValueTask EnsureHeaderAsync(CancellationToken cancellationToken)
        {
            if (!_encrypting || _headerWritten)
            {
                return;
            }

            byte[] header = new byte[12];
            CalculateHeader(header);
            await _base.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            _headerWritten = true;
        }

        private static async Task<(uint key0, uint key1, uint key2)> ReadAndValidateHeaderCore(bool isAsync, Stream baseStream, ZipCryptoKeys keys, byte expectedCheckByte, CancellationToken cancellationToken)
        {
            // Initialize keys from input
            uint key0 = keys.Key0;
            uint key1 = keys.Key1;
            uint key2 = keys.Key2;

            byte[] hdr = new byte[12];

            try
            {
                if (isAsync)
                {
                    await baseStream.ReadExactlyAsync(hdr, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    baseStream.ReadExactly(hdr);
                }
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException(SR.TruncatedZipCryptoHeader);
            }

            // Decrypt header and update keys
            for (int i = 0; i < hdr.Length; i++)
            {
                byte m = DecryptByte(key2);
                byte plain = (byte)(hdr[i] ^ m);
                UpdateKeys(ref key0, ref key1, ref key2, plain);
                hdr[i] = plain;
            }

            if (hdr[11] != expectedCheckByte)
            {
                throw new InvalidDataException(SR.InvalidPassword);
            }

            return (key0, key1, key2);
        }

        private static void UpdateKeys(ref uint key0, ref uint key1, ref uint key2, byte b)
        {
            key0 = Crc32Update(key0, b);
            key1 += (key0 & 0xFF);
            key1 = key1 * 134775813 + 1;
            key2 = Crc32Update(key2, (byte)(key1 >> 24));
        }

        private static byte DecryptByte(uint key2)
        {
            uint temp = key2 | 2;
            return (byte)((temp * (temp ^ 1)) >> 8);
        }

        private byte DecryptAndUpdateKeys(byte ciph)
        {
            byte m = DecryptByte(_key2);
            byte plain = (byte)(ciph ^ m);
            UpdateKeys(ref _key0, ref _key1, ref _key2, plain);
            return plain;
        }

        public override bool CanRead => !_disposed && !_encrypting;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed && _encrypting;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _base.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_encrypting)
            {
                throw new NotSupportedException(SR.ReadingNotSupported);
            }
            int n = _base.Read(destination);
            for (int i = 0; i < n; i++)
                destination[i] = DecryptAndUpdateKeys(destination[i]);
            return n;

        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_encrypting)
            {
                throw new NotSupportedException(SR.WritingNotSupported);
            }

            EnsureHeader();

            byte[] workBuffer = GetWriteWorkBuffer();

            while (!buffer.IsEmpty)
            {
                int chunkSize = Math.Min(buffer.Length, workBuffer.Length);

                for (int i = 0; i < chunkSize; i++)
                {
                    byte ks = DecryptByte(_key2);
                    byte p = buffer[i];
                    workBuffer[i] = (byte)(p ^ ks);
                    UpdateKeys(ref _key0, ref _key1, ref _key2, p);
                }

                _base.Write(workBuffer, 0, chunkSize);
                buffer = buffer[chunkSize..];
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (disposing)
            {
                // If encrypted empty entry (no payload written), still must emit 12-byte header:
                if (_encrypting)
                {
                    EnsureHeader();
                }

                if (!_leaveOpen)
                {
                    _base.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            // If encrypted empty entry (no payload written), still must emit 12-byte header:
            if (_encrypting)
            {
                await EnsureHeaderAsync(CancellationToken.None).ConfigureAwait(false);
            }
            if (!_leaveOpen)
            {
                await _base.DisposeAsync().ConfigureAwait(false);
            }

            GC.SuppressFinalize(this);

            // Don't call base.DisposeAsync() as it would call Dispose() synchronously,
            // which could fail on async-only streams. We've already handled all cleanup.
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_encrypting)
            {
                throw new NotSupportedException(SR.ReadingNotSupported);
            }

            cancellationToken.ThrowIfCancellationRequested();
            int n = await _base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Span<byte> span = buffer.Span;

            for (int i = 0; i < n; i++)
            {
                span[i] = DecryptAndUpdateKeys(span[i]);
            }

            return n;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_encrypting)
            {
                throw new NotSupportedException(SR.WritingNotSupported);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await EnsureHeaderAsync(cancellationToken).ConfigureAwait(false);

            byte[] workBuffer = GetWriteWorkBuffer();

            while (!buffer.IsEmpty)
            {
                int chunkSize = Math.Min(buffer.Length, workBuffer.Length);
                ReadOnlySpan<byte> chunk = buffer.Span[..chunkSize];

                for (int i = 0; i < chunkSize; i++)
                {
                    byte ks = DecryptByte(_key2);
                    byte p = chunk[i];
                    workBuffer[i] = (byte)(p ^ ks);
                    UpdateKeys(ref _key0, ref _key1, ref _key2, p);
                }

                await _base.WriteAsync(workBuffer.AsMemory(0, chunkSize), cancellationToken).ConfigureAwait(false);
                buffer = buffer[chunkSize..];
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _base.FlushAsync(cancellationToken);
        }

        private byte[] GetWriteWorkBuffer() => _writeWorkBuffer ??= new byte[EncryptionBufferSize];
    }
}
