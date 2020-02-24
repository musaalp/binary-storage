using BinStorage.Index;

namespace BinStorage.Test.Index
{
    public abstract class IndexTest
    {
        public const int IndexBinTestCapacity = Sizes.Size1Kb;
        public const int IndexBinDefaultNodeSize = DefaultSizes.DefaultNodeSize;
        public const int MultiThreadIndexBinTestCapacity = Sizes.Size10Kb;

        protected abstract IIndex Create(long capacity = IndexBinTestCapacity);
    }
}
