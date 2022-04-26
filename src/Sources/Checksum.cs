using System.Collections.Generic;

namespace LiveSharp.ServerClient
{
    public static class Checksum
    {
        public static ushort Fletcher16(IReadOnlyList<byte> data)
        {
            ushort sum1 = 0;
            ushort sum2 = 0;

            for (var index = 0; index < data.Count; ++index) {
                sum1 = (ushort)((sum1 + data[index]) % 255);
                sum2 = (ushort)((sum2 + sum1) % 255);
            }

            return (ushort)((sum2 << 8) | sum1);
        }
    }
}