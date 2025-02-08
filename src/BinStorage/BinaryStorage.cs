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
        private bool _isDisposed;

        public BinaryStorage(StorageConfiguration configuration)
        {
            ValidateConfiguration(configuration);

            var storageFilePath = CreateStoragePath(configuration);
            var indexFilePath = CreateIndexPath(configuration);

            _index = CreateThreadSafeIndex(indexFilePath, configuration.IndexTimeout);
            _storage = new FileStorage(storageFilePath);
        }

        private static void ValidateConfiguration(StorageConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrEmpty(configuration.WorkingFolder))
                throw new ArgumentException("Working folder cannot be empty", nameof(configuration));
        }

        private static string CreateStoragePath(StorageConfiguration configuration)
            => Path.Combine(configuration.WorkingFolder, configuration.StorageFileName);

        private static string CreateIndexPath(StorageConfiguration configuration)
            => Path.Combine(configuration.WorkingFolder, configuration.IndexFileName);

        private static IIndex CreateThreadSafeIndex(string indexFilePath, TimeSpan indexTimeout)
            => new ThreadSafeIndex(new PersistentIndex(indexFilePath), indexTimeout);

        public void Add(string key, Stream data)
        {
            ValidateAddParameters(key, data);
            EnsureNotDisposed();
            
            try
            {
                AddToStorage(key, data);
            }
            catch
            {
                Rollback();
                throw;
            }
        }

        private void ValidateAddParameters(string key, Stream data)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
        }

        private void AddToStorage(string key, Stream data)
        {
            var indexData = _storage.Append(data);
            _index.Add(key, indexData);
        }

        public Stream Get(string key)
        {
            ValidateKey(key);
            EnsureNotDisposed();

            var indexData = _index.Get(key);
            return _storage.Get(indexData);
        }

        private void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
        }

        public bool Contains(string key)
        {
            ValidateKey(key);
            EnsureNotDisposed();

            return _index.Contains(key);
        }

        public void Commit()
        {
            EnsureNotDisposed();
            _storage.Commit();
            _index.Commit();
        }

        public void Rollback()
        {
            EnsureNotDisposed();
            _storage.Rollback();
            _index.Rollback();
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(BinaryStorage));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                _index.Dispose();
                _storage.Dispose();
            }

            _isDisposed = true;
        }

        ~BinaryStorage()
        {
            Dispose(false);
        }
    }
}
