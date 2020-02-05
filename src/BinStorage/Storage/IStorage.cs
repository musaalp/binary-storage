using BinStorage.Index;
using System;
using System.IO;

namespace BinStorage.Storage
{
    public interface IStorage : IDisposable
    {
        IndexData Append(Stream input);

        Stream Get(IndexData indexData);
    }
}
