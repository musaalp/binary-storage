namespace BinStorage
{
    public class DefaultSizes
    {
        public const int CursorHolderOffset = 0;
        public const int CursorHolderSize = sizeof(long);
        public const int Md5HashSize = 16;
        public const int IndexDataSize = Md5HashSize + sizeof(long) + sizeof(long);
        public const int KeyOffsetSize = sizeof(long);
        public const int KeySizeSize = sizeof(int);
        public const int DefaultNodeSize = KeyOffsetSize + KeySizeSize + IndexDataSize;

        public const int DefaultReadBufferSize = Sizes.Size16Kb;
        public const long DefaultStorageBinCapacity = Sizes.Size1Gb;
        public const long DefaultIndexBinCapacity = Sizes.Size16Mb;
    }
}
