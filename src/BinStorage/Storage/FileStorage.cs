using BinStorage.Exceptions;
using BinStorage.Index;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BinStorage.Storage
{
    public class FileStorage : IStorage
    {
        private readonly ReaderWriterLockSlim _lock;
        private readonly int _readBufferSize;
        private readonly string _storageFilePath;
        private long _capacity;
        private long _cursor;
        private long _cursorPositionBeforeAction;
        private MemoryMappedFile _file;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="storageFilePath">Path to storage file</param>
        /// <param name="capacity">
        ///     Initial capacity of the file
        ///     Capacity automatically increased to file size, or twice on overflow
        /// </param>
        /// <param name="readBufferSize">Buffer size for reading from append stream</param>
        public FileStorage(string storageFilePath, long capacity = DefaultSizes.DefaultStorageBinCapacity, int readBufferSize = DefaultSizes.DefaultReadBufferSize)
        {
            _storageFilePath = storageFilePath;
            _readBufferSize = readBufferSize;
            _capacity = capacity + DefaultSizes.CursorHolderSize;
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

            InitFile();
        }

        public IndexData Append(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            CheckDisposed();

            var length = input.Length;
            var cursor = Interlocked.Add(ref _cursor, length) - length;

            EnsureCapacity(cursor, length);

            var indexData = new IndexData
            {
                Offset = cursor,
                Size = length
            };

            var buffer = new byte[_readBufferSize];
            var count = input.Read(buffer, 0, _readBufferSize);
            var prevCount = count;
            var prevBuffer = Interlocked.Exchange(ref buffer, new byte[_readBufferSize]);

            using (var hashAlgorithm = MD5.Create())
            {
                do
                {
                    _lock.EnterReadLock();
                    try
                    {
                        if (prevCount > 0)
                        {
                            using (var writer = _file.CreateViewStream(cursor, prevCount, MemoryMappedFileAccess.Write))
                            {
                                writer.Write(prevBuffer, 0, prevCount);
                                cursor += prevCount;
                            }
                        }
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }

                    count = input.Read(buffer, 0, _readBufferSize);
                    if (count > 0)
                    {
                        hashAlgorithm.TransformBlock(prevBuffer, 0, prevCount, null, 0);
                        prevCount = count;
                        prevBuffer = Interlocked.Exchange(ref buffer, prevBuffer);
                    }
                    else
                    {
                        hashAlgorithm.TransformFinalBlock(prevBuffer, 0, prevCount);
                        break;
                    }
                } while (true);

                indexData.Md5Hash = hashAlgorithm.Hash;
            }

            return indexData;
        }

        public Stream Get(IndexData indexData)
        {
            if (indexData == null)
            {
                throw new ArgumentNullException(nameof(indexData));
            }

            CheckDisposed();

            if (indexData.Size == 0)
            {
                return Stream.Null;
            }

            try
            {
                _lock.EnterReadLock();
                using (var reader = _file.CreateViewStream(indexData.Offset, indexData.Size, MemoryMappedFileAccess.Read))
                {
                    var buffer = new byte[indexData.Size];
                    reader.Read(buffer, 0, buffer.Length);

                    return new MemoryStream(buffer);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void EnsureCapacity(long cursor, long length)
        {
            try
            {
                _lock.EnterWriteLock();

                var required = cursor + length;
                if (required <= _capacity)
                {
                    return;
                }

                //New file may need to be created with new capacity, for this reason exist _file must be released because previous process is using it. 
                ReleaseFile();

                _capacity <<= 1;
                if (required > _capacity)
                {
                    _capacity = required;
                }

                InitFile();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void CheckSpace()
        {
            if (GetFreeSpace() >= _capacity)
            {
                return;
            }

            throw new NotEnoughDiskSpaceException("There is not enough space on the disk.");
        }

        private long GetFreeSpace()
        {
            var pathRoot = Path.GetPathRoot(_storageFilePath);

            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && string.Equals(drive.Name, pathRoot, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.TotalFreeSpace)
                .First();
        }

        private void InitFile()
        {
            CheckSpace();

            if (File.Exists(_storageFilePath))
            {
                var length = new FileInfo(_storageFilePath).Length;
                if (_capacity < length)
                {
                    _capacity = length;
                }
            }

            _file = MemoryMappedFile.CreateFromFile(
                _storageFilePath,
                FileMode.OpenOrCreate,
                null,
                _capacity,
                MemoryMappedFileAccess.ReadWrite);

            using (var cursorHolder = _file.CreateViewAccessor(0, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Read))
            {
                _cursor = cursorHolder.ReadInt64(0);
            }
            if (_cursor == 0)
            {
                _cursor = DefaultSizes.CursorHolderSize;
            }

            _cursorPositionBeforeAction = _cursor;
        }

        private void ReleaseFile(bool disposeFile = true)
        {
            using (var cursorHolder = _file.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                cursorHolder.Write(0, _cursor);
            }

            if (disposeFile)
            {
                _file.Dispose();
            }
        }

        public void Commit()
        {
            using (var cursorHolder = _file.CreateViewAccessor(0, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                cursorHolder.Write(0, _cursor);
            }
        }

        public void Rollback()
        {
            //update cursor position value with previous value
            using (var cursorHolder = _file.CreateViewAccessor(0, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                cursorHolder.Write(0, _cursorPositionBeforeAction);
            }

            //flush the stream from last successfully write operation to end of the file
            var size = _capacity - _cursorPositionBeforeAction;
            using (var writer = _file.CreateViewStream(_cursorPositionBeforeAction, size, MemoryMappedFileAccess.Write))
            {
                writer.Flush();
            }
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _file?.Dispose();

            _disposed = true;
        }

        private bool _disposed;

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Binary storage is disposed");
            }
        }

        ~FileStorage()
        {
            Dispose(false);
        }

        #endregion
    }
}
