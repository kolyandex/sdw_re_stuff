using System;

namespace SdwEditor
{
    internal static class Crc32Sdw
    {
        private static readonly uint[] Table = Build();

        private static uint[] Build()
        {
            uint[] t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                }
                t[i] = c;
            }
            return t;
        }

        /// <summary>CRC32 IEEE без финального XOR — как в .WAR/.SND игры.</summary>
        public static uint ComputeSkipHeader(byte[] data, int skip)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = skip; i < data.Length; i++)
            {
                crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc;
        }

        public static void Patch(byte[] data)
        {
            uint crc = ComputeSkipHeader(data, 4);
            BitConverter.GetBytes(crc).CopyTo(data, 0);
        }
    }
}
