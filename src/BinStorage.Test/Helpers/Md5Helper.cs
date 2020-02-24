using System.IO;
using System.Security.Cryptography;

namespace BinStorage.Test.Helpers
{
    public class Md5Helper
    {
        public static void ComputeHashes(byte[] sourceStream, Stream resultStream, out byte[] hash1, out byte[] hash2)
        {
            using (var md5 = MD5.Create())
            {
                hash1 = md5.ComputeHash(sourceStream);

                md5.Initialize();
                hash2 = md5.ComputeHash(resultStream);
            }
        }
    }
}
