using BinStorage.Exceptions;
using BinStorage.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

namespace BinStorage.Index
{
    public class PersistentIndex : IIndex
    {
        private readonly string _indexFilePath;
        private readonly RootNode _rootNode;
        private long _capacity;
        private long _indexCursor;
        private long _cursorPositionBeforeAction;
        private MemoryMappedFile _indexFile;
        private bool _disposed;

        public PersistentIndex(string indexFilePath, long capacity = DefaultSizes.DefaultIndexBinCapacity)
        {
            _indexFilePath = indexFilePath;
            _capacity = capacity + DefaultSizes.CursorHolderSize;
            _rootNode = new RootNode();

            InitializeFile();
        }

        public void Add(string key, IndexData indexData)
        {
            ValidateAddParameters(key, indexData);

            if (Contains(key))
            {
                throw new DuplicateException(nameof(key));
            }

            CreateNewNode(key, indexData);
        }

        public bool Contains(string key)
        {
            return _rootNode.Nodes.ContainsKey(key);
        }

        public IndexData Get(string key)
        {
            ValidateKey(key);

            try
            {
                var keyInfo = _rootNode.Nodes[key].KeyInfo;
                return ReadIndexDataFromFile(keyInfo);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
        }

        private void ValidateAddParameters(string key, IndexData indexData)
        {
            ValidateKey(key);
            if (indexData == null)
            {
                throw new ArgumentNullException(nameof(indexData));
            }
            EnsureNotDisposed();
        }

        private void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        private void CreateNewNode(string key, IndexData indexData)
        {
            var keyBuffer = Encoding.UTF8.GetBytes(key);
            var newNodeSize = DefaultSizes.DefaultNodeSize + keyBuffer.Length;

            EnsureCapacity(_indexCursor, newNodeSize);

            WriteNodeToFile(key, keyBuffer, indexData, newNodeSize);
            AddNodeToRoot(key, keyBuffer, indexData);

            Commit();
        }

        private void WriteNodeToFile(string key, byte[] keyBuffer, IndexData indexData, int newNodeSize)
        {
            using (var writer = _indexFile.CreateViewStream(_indexCursor, newNodeSize, MemoryMappedFileAccess.Write))
            {
                var keyOffset = _indexCursor + DefaultSizes.DefaultNodeSize;

                writer.WriteLong(keyOffset);
                writer.WriteInt(keyBuffer.Length);
                WriteIndexDataToStream(writer, indexData);
                writer.Write(keyBuffer, 0, keyBuffer.Length);

                _indexCursor += writer.Position;
            }
        }

        private void AddNodeToRoot(string key, byte[] keyBuffer, IndexData indexData)
        {
            var keyInfo = new KeyInfo
            {
                Key = key,
                Offset = _indexCursor + DefaultSizes.DefaultNodeSize,
                Size = keyBuffer.Length
            };

            var node = new Node
            {
                KeyInfo = keyInfo,
                IndexData = indexData
            };

            _rootNode.Nodes.Add(key, node);
        }

        private static void WriteIndexDataToStream(Stream writer, IndexData data)
        {
            writer.WriteLong(data.Size);
            writer.WriteLong(data.Offset);
            writer.Write(data.Md5Hash, 0, DefaultSizes.Md5HashSize);
        }

        private void EnsureCapacity(long cursor, int length)
        {
            var requiredCapacity = cursor + length;
            if (requiredCapacity <= _capacity)
            {
                return;
            }

            ReleaseFile();
            _capacity = CalculateNewCapacity(requiredCapacity);
            InitializeFile();
        }

        private long CalculateNewCapacity(long requiredCapacity)
        {
            var newCapacity = _capacity << 1;
            return requiredCapacity > newCapacity ? requiredCapacity : newCapacity;
        }

        private void EnsureDiskSpace()
        {
            if (GetFreeSpace() < _capacity)
            {
                throw new NotEnoughDiskSpaceException("There is not enough space on the disk.");
            }
        }

        private long GetFreeSpace()
        {
            var pathRoot = Path.GetPathRoot(_indexFilePath);
            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && string.Equals(drive.Name, pathRoot, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.TotalFreeSpace)
                .First();
        }

        private void ReleaseFile(bool disposeFile = true)
        {
            using (var writer = _indexFile.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                writer.Write(DefaultSizes.CursorHolderOffset, _indexCursor);
            }

            if (disposeFile)
            {
                _indexFile.Dispose();
            }
        }

        private void InitializeFile()
        {
            EnsureDiskSpace();
            AdjustCapacityToFileSize();

            _indexFile = MemoryMappedFile.CreateFromFile(
                _indexFilePath,
                FileMode.OpenOrCreate,
                null,
                _capacity,
                MemoryMappedFileAccess.ReadWrite);

            InitializeRootAndCursor();
        }

        private void AdjustCapacityToFileSize()
        {
            if (File.Exists(_indexFilePath))
            {
                var fileLength = new FileInfo(_indexFilePath).Length;
                if (_capacity < fileLength)
                {
                    _capacity = fileLength;
                }
            }
        }

        private void InitializeRootAndCursor()
        {
            using (var reader = _indexFile.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Read))
            {
                _indexCursor = reader.ReadInt64(0);
                if (_indexCursor == 0)
                {
                    _indexCursor = DefaultSizes.CursorHolderSize;
                }
                else
                {
                    LoadExistingNodes();
                }
                _cursorPositionBeforeAction = _indexCursor;
            }
        }

        private void LoadExistingNodes()
        {
            var offset = DefaultSizes.CursorHolderSize;
            var size = _capacity - offset;

            using (var reader = _indexFile.CreateViewStream(offset, size, MemoryMappedFileAccess.Read))
            {
                while (true)
                {
                    var keyOffset = reader.ReadLong();
                    if (keyOffset == 0)
                    {
                        break;
                    }

                    var keySize = reader.ReadInt();
                    var keyInfo = new KeyInfo { Offset = keyOffset, Size = keySize };
                    var indexData = ReadIndexDataFromFile(keyInfo);
                    var key = ReadKeyFromFile(keyInfo);

                    reader.Position += DefaultSizes.IndexDataSize + key.Length;

                    AddNodeToRoot(key, Encoding.UTF8.GetBytes(key), indexData);
                }
            }
        }

        private string ReadKeyFromFile(KeyInfo keyInfo)
        {
            using (var reader = _indexFile.CreateViewAccessor(keyInfo.Offset, keyInfo.Size, MemoryMappedFileAccess.Read))
            {
                var buffer = new byte[keyInfo.Size];
                reader.ReadArray(0, buffer, 0, keyInfo.Size);
                return Encoding.UTF8.GetString(buffer);
            }
        }

        private IndexData ReadIndexDataFromFile(KeyInfo keyInfo)
        {
            var offset = keyInfo.Offset - DefaultSizes.IndexDataSize;
            using (var reader = _indexFile.CreateViewStream(offset, DefaultSizes.IndexDataSize, MemoryMappedFileAccess.Read))
            {
                return new IndexData
                {
                    Size = reader.ReadLong(),
                    Offset = reader.ReadLong(),
                    Md5Hash = ReadMd5Hash(reader)
                };
            }
        }

        private static byte[] ReadMd5Hash(Stream reader)
        {
            var md5Hash = new byte[DefaultSizes.Md5HashSize];
            reader.Read(md5Hash, 0, DefaultSizes.Md5HashSize);
            return md5Hash;
        }

        public void Commit()
        {
            using (var writer = _indexFile.CreateViewAccessor(DefaultSizes.CursorHolderOffset, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                writer.Write(DefaultSizes.CursorHolderOffset, _indexCursor);
            }
        }

        public void Rollback()
        {
            ResetCursorPosition();
            FlushStreamFromLastWrite();
        }

        private void ResetCursorPosition()
        {
            using (var cursorHolder = _indexFile.CreateViewAccessor(0, DefaultSizes.CursorHolderSize, MemoryMappedFileAccess.Write))
            {
                cursorHolder.Write(0, _cursorPositionBeforeAction);
            }
        }

        private void FlushStreamFromLastWrite()
        {
            var size = _capacity - _cursorPositionBeforeAction;
            using (var writer = _indexFile.CreateViewStream(_cursorPositionBeforeAction, size, MemoryMappedFileAccess.Write))
            {
                writer.Flush();
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
            {
                return;
            }

            if (disposing)
            {
                _indexFile?.Dispose();
            }

            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Index storage is disposed");
            }
        }

        ~PersistentIndex()
        {
            Dispose(false);
        }
    }
}
