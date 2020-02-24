using BinStorage.Index;
using BinStorage.Storage;
using System;
using System.IO;

namespace BinStorage
{
    public class BinaryStorage : IBinaryStorage, ITransaction
    {
        private readonly IIndex _index;
        private readonly IStorage _storage;

        public BinaryStorage(StorageConfiguration configuration)
        {
            var storageFilePath = Path.Combine(configuration.WorkingFolder, configuration.StorageFileName);
            var indexFilePath = Path.Combine(configuration.WorkingFolder, configuration.IndexFileName);

            _index = new ThreadSafeIndex(new PersistentIndex(indexFilePath), configuration.IndexTimeout);
            _storage = new FileStorage(storageFilePath);
        }

        public void Add(string key, Stream data)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            CheckDisposed();

            TryAdd(key, data);
        }

        //try add if any error happen, then rollback
        private void TryAdd(string key, Stream data)
        {
            var indexData = _storage.Append(data);

            _index.Add(key, indexData);
        }

        public void Commit()
        {
            _storage.Commit();
            _index.Commit();
        }

        //rollback from last successfully point, it takes cursor position as reference
        public void Rollback()
        {
            _storage.Rollback();
            _index.Rollback();
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            CheckDisposed();

            return _index.Contains(key);
        }

        public Stream Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            CheckDisposed();

            var indexData = _index.Get(key);

            return _storage.Get(indexData);
        }

        #region IDisposable

        private bool _disposed;

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Binary storage is disposed");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _index.Dispose();
            _storage.Dispose();

            _disposed = true;
        }

        ~BinaryStorage()
        {
            Dispose(false);
        }

        #endregion
    }
}
