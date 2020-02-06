using System;
using System.IO;

namespace BinStorage.Extensions
{
    public static class BitHelper
    {
        public static int ReadInt(this Stream reader)
        {
            var buffer = new byte[sizeof(int)];
            reader.Read(buffer, 0, buffer.Length);
            return BitConverter.ToInt32(buffer, 0);
        }

        public static long ReadLong(this Stream reader)
        {
            var buffer = new byte[sizeof(long)];
            reader.Read(buffer, 0, buffer.Length);
            return BitConverter.ToInt64(buffer, 0);
        }

        public static void WriteInt(this Stream writer, int value)
        {
            var buffer = BitConverter.GetBytes(value);
            writer.Write(buffer, 0, buffer.Length);
        }

        public static void WriteLong(this Stream writer, long value)
        {
            var buffer = BitConverter.GetBytes(value);
            writer.Write(buffer, 0, buffer.Length);
        }
    }
}
