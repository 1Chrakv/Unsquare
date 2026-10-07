using System;
using System.Security.Cryptography;
using System.Text;

namespace Unsquare.Core.Naming
{
    /// <summary>MD5 of a UTF-8 seed: identical across sessions, machines and runtimes.</summary>
    public readonly struct StableHash
    {
        private readonly byte[] _bytes;

        private StableHash(byte[] bytes)
        {
            _bytes = bytes;
        }

        public static StableHash Compute(string seed)
        {
            if (seed == null)
                throw new ArgumentNullException(nameof(seed));
            using var md5 = MD5.Create();
            return new StableHash(md5.ComputeHash(Encoding.UTF8.GetBytes(seed)));
        }

        /// <summary>One of the four little-endian 32-bit words (e.g. for a Colossal.Hash128).</summary>
        public uint Word(int index)
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index));
            return BitConverter.ToUInt32(_bytes, index * 4);
        }

        /// <summary>Lowercase hex of the first <paramref name="byteCount"/> bytes.</summary>
        public string ToHex(int byteCount)
        {
            if (byteCount < 1 || byteCount > 16)
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            var sb = new StringBuilder(byteCount * 2);
            for (int i = 0; i < byteCount; i++)
                sb.Append(_bytes[i].ToString("x2"));
            return sb.ToString();
        }

        internal static bool IsLowerHex(string text, int start, int length)
        {
            if (text.Length - start != length)
                return false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    return false;
            }
            return true;
        }
    }
}
