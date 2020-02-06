using System;

namespace BinStorage.Index
{
    [Serializable]
    public class IndexData
    {
        public long Size { get; set; }

        public long Offset { get; set; }

        public byte[] Md5Hash { get; set; }
    }
}
