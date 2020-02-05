using System;
using System.IO;

namespace BinStorage
{
    public interface IBinaryStorage : IDisposable
    {
        void Add(string key, Stream data);

        Stream Get(string key);

        bool Contains(string key);
    }
}
