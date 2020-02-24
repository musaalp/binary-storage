using BinStorage.Index;
using System;
using System.IO;

namespace BinStorage.Storage
{
    public interface IStorage : ITransaction, IDisposable
    {
        IndexData Append(Stream input);

        Stream Get(IndexData indexData);
    }
}
